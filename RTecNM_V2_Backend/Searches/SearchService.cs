using System.Data;
using Microsoft.EntityFrameworkCore;
using TecNM.Residency.Advisors;
using TecNM.Residency.Auth;
using TecNM.Residency.Common;
using TecNM.Residency.Searches.Dtos;

namespace TecNM.Residency.Searches;

public class SearchService : ISearchService
{
    private readonly AppDbContext _context;
    private readonly SearchRegistry _registry;
    private readonly ICurrentUserService _currentUser;
    private readonly IAdvisorRepository _advisorRepository;

    private static bool _viewsCreated = false;
    private static readonly SemaphoreSlim _viewLock = new SemaphoreSlim(1, 1);

    public SearchService(
        AppDbContext context,
        SearchRegistry registry,
        ICurrentUserService currentUser,
        IAdvisorRepository advisorRepository)
    {
        _context = context;
        _registry = registry;
        _currentUser = currentUser;
        _advisorRepository = advisorRepository;
    }

    public List<SearchSourceMetadataDto> GetAvailableSources()
    {
        var all = _registry.GetAllMetadata();
        if (_currentUser.IsInRole(UserRole.Advisor) &&
            !_currentUser.IsInRole(UserRole.Admin) &&
            !_currentUser.IsInRole(UserRole.DepartmentHead))
        {
            return all.Where(s => s.Key.Equals("PROJECTS", StringComparison.OrdinalIgnoreCase)).ToList();
        }
        return all;
    }

    public async Task<PagedSearchResponseDto> SearchPagedAsync(PagedSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        await EnsureViewsExistAsync(cancellationToken);

        var sourceConfig = _registry.GetSource(request.SourceKey);
        if (sourceConfig == null)
        {
            throw new ArgumentException($"La fuente de búsqueda '{request.SourceKey}' no existe o no está registrada.");
        }

        // Whitelist validation for SearchColumn
        var searchCol = sourceConfig.Columns.FirstOrDefault(c => c.Name.Equals(request.SearchColumn, StringComparison.OrdinalIgnoreCase));
        if (searchCol == null || !searchCol.IsSearchable)
        {
            searchCol = sourceConfig.Columns.FirstOrDefault(c => c.IsSearchable) ?? sourceConfig.Columns.First();
        }

        // Whitelist validation for SortColumn
        var sortCol = sourceConfig.Columns.FirstOrDefault(c => c.Name.Equals(request.SortColumn, StringComparison.OrdinalIgnoreCase))?.Name ?? sourceConfig.KeyColumn;
        var sortDir = request.SortDirection.Equals("DESC", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";

        // Pagination clamping
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);

        // Pattern matching parameter
        string matchPattern = request.MatchOption switch
        {
            "StartsWith" => $"{request.SearchText}%",
            "EndsWith" => $"%{request.SearchText}",
            "Exact" => request.SearchText,
            _ => $"%{request.SearchText}%"
        };

        var operatorClause = request.MatchOption == "Exact" ? "=" : "ILIKE";
        var hasSearchText = !string.IsNullOrWhiteSpace(request.SearchText);

        var statusCondition = request.StatusFilter?.ToLower() switch
        {
            "inactive" => "\"is_active\" = FALSE",
            "all" => null,
            _ => "\"is_active\" = TRUE"
        };

        var whereConditions = new List<string>();
        if (!string.IsNullOrEmpty(statusCondition))
        {
            whereConditions.Add(statusCondition);
        }
        if (hasSearchText)
        {
            whereConditions.Add($"\"{searchCol.Name}\"::text {operatorClause} @searchPattern");
        }

        // Seguridad RBAC: Si el usuario es Asesor, limitar estrictamente sus búsquedas a sus anteproyectos asignados.
        if (_currentUser.IsInRole(UserRole.Advisor) &&
            !_currentUser.IsInRole(UserRole.Admin) &&
            !_currentUser.IsInRole(UserRole.DepartmentHead))
        {
            var advisorProfile = await _advisorRepository.GetByUserIdAsync(_currentUser.UserId);
            if (advisorProfile == null)
            {
                return new PagedSearchResponseDto
                {
                    Source = new SearchSourceMetadataDto { Key = request.SourceKey },
                    Pagination = PaginatedResult<Dictionary<string, object?>>.Create(new List<Dictionary<string, object?>>(), 0, request.PageNumber, request.PageSize),
                    Rows = new()
                };
            }

            if (request.SourceKey.Equals("PROJECTS", StringComparison.OrdinalIgnoreCase))
            {
                whereConditions.Add($"\"advisor_id\" = {advisorProfile.Id}");
            }
            else
            {
                return new PagedSearchResponseDto
                {
                    Source = new SearchSourceMetadataDto { Key = request.SourceKey },
                    Pagination = PaginatedResult<Dictionary<string, object?>>.Create(new List<Dictionary<string, object?>>(), 0, request.PageNumber, request.PageSize),
                    Rows = new()
                };
            }
        }

        var whereClause = whereConditions.Count > 0 ? "WHERE " + string.Join(" AND ", whereConditions) : "";

        var countSql = $"SELECT COUNT(*) FROM \"{sourceConfig.ViewName}\" {whereClause};";
        var querySql = $@"
            SELECT * FROM ""{sourceConfig.ViewName}""
            {whereClause}
            ORDER BY ""{sortCol}"" {sortDir}
            OFFSET @offset LIMIT @limit;";

        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        int totalRows = 0;
        var rows = new List<Dictionary<string, object?>>();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = countSql;
            if (hasSearchText)
            {
                var param = command.CreateParameter();
                param.ParameterName = "@searchPattern";
                param.Value = matchPattern;
                command.Parameters.Add(param);
            }

            var result = await command.ExecuteScalarAsync(cancellationToken);
            totalRows = Convert.ToInt32(result);
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = querySql;
            if (hasSearchText)
            {
                var param = command.CreateParameter();
                param.ParameterName = "@searchPattern";
                param.Value = matchPattern;
                command.Parameters.Add(param);
            }

            var offsetParam = command.CreateParameter();
            offsetParam.ParameterName = "@offset";
            offsetParam.Value = (pageNumber - 1) * pageSize;
            command.Parameters.Add(offsetParam);

            var limitParam = command.CreateParameter();
            limitParam.ParameterName = "@limit";
            limitParam.Value = pageSize;
            command.Parameters.Add(limitParam);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var colName = reader.GetName(i);
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    row[colName] = value;
                }
                rows.Add(row);
            }
        }

        var sourceMetadata = new SearchSourceMetadataDto
        {
            Key = sourceConfig.Key,
            DisplayName = sourceConfig.DisplayName,
            KeyColumn = sourceConfig.KeyColumn,
            Columns = sourceConfig.Columns
        };

        var pagination = PaginatedResult<Dictionary<string, object?>>.Create(rows, totalRows, pageNumber, pageSize);

        return new PagedSearchResponseDto
        {
            Source = sourceMetadata,
            Pagination = pagination,
            Rows = rows
        };
    }

    private async Task EnsureViewsExistAsync(CancellationToken cancellationToken)
    {
        if (_viewsCreated) return;

        await _viewLock.WaitAsync(cancellationToken);
        try
        {
            if (_viewsCreated) return;

            var viewStatements = new string[]
            {
                "DROP VIEW IF EXISTS vw_search_students CASCADE;",
                @"CREATE OR REPLACE VIEW vw_search_students AS
                SELECT 
                    s.id AS id,
                    s.control_number AS control_number,
                    CONCAT(s.first_name, ' ', s.last_name_1, COALESCE(' ' || s.last_name_2, '')) AS full_name,
                    COALESCE(u.email, '') AS email,
                    COALESCE(s.curp, '') AS curp,
                    s.career_id AS career_id,
                    s.is_active AS is_active
                FROM students s
                LEFT JOIN users u ON s.user_id = u.id;",

                "DROP VIEW IF EXISTS vw_search_advisors CASCADE;",
                @"CREATE OR REPLACE VIEW vw_search_advisors AS
                SELECT 
                    a.id AS id,
                    a.full_name AS full_name,
                    COALESCE(a.title, '') AS title,
                    a.advisor_type::text AS advisor_type,
                    a.department_id AS department_id,
                    COALESCE(u.email, '') AS email,
                    COALESCE(a.phone, '') AS phone,
                    a.is_active AS is_active
                FROM advisors a
                LEFT JOIN users u ON a.user_id = u.id;",

                "DROP VIEW IF EXISTS vw_search_projects CASCADE;",
                @"CREATE OR REPLACE VIEW vw_search_projects AS
                SELECT 
                    p.id AS id,
                    p.title AS title,
                    COALESCE(p.project_type, '') AS project_type,
                    p.status::text AS status,
                    CONCAT(s.first_name, ' ', s.last_name_1) AS student_name,
                    COALESCE(a.full_name, 'Sin Asignar') AS advisor_name,
                    COALESCE(c.name, 'Sin Empresa') AS company_name,
                    p.advisor_id AS advisor_id,
                    p.is_active AS is_active
                FROM projects p
                LEFT JOIN students s ON p.student_id = s.id
                LEFT JOIN advisors a ON p.advisor_id = a.id
                LEFT JOIN companies c ON p.company_id = c.id
                WHERE p.status <> 'draft';",

                "DROP VIEW IF EXISTS vw_search_companies CASCADE;",
                @"CREATE OR REPLACE VIEW vw_search_companies AS
                SELECT 
                    c.id AS id,
                    c.name AS name,
                    c.rfc AS rfc,
                    COALESCE(c.sector, '') AS sector,
                    COALESCE(c.contact_name, '') AS contact_name,
                    COALESCE(c.contact_email, '') AS contact_email,
                    c.is_active AS is_active
                FROM companies c;"
            };

            foreach (var stmt in viewStatements)
            {
                await _context.Database.ExecuteSqlRawAsync(stmt, cancellationToken);
            }

            _viewsCreated = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SearchService Warning] Error creando vistas de búsqueda: {ex.Message}");
        }
        finally
        {
            _viewLock.Release();
        }
    }
}
