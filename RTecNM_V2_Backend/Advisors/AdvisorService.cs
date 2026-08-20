using TecNM.Residency.Auth;
using TecNM.Residency.Common;
using TecNM.Residency.Projects;

namespace TecNM.Residency.Advisors;

public class AdvisorService : IAdvisorService
{
    private readonly IAdvisorRepository _advisorRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly ICurrentUserService _currentUser;

    public AdvisorService(IAdvisorRepository advisorRepository, IProjectRepository projectRepository, IRoleRepository roleRepository, ICurrentUserService currentUser)
    {
        _advisorRepository = advisorRepository;
        _projectRepository = projectRepository;
        _roleRepository = roleRepository;
        _currentUser = currentUser;
    }

    public async Task<Result<PaginatedResult<AdvisorResponseDto>>> GetPagedAsync(PaginationQuery query, string? status, bool includeInactive = false)
    {
        var paged = await _advisorRepository.GetPagedAsync(query, status, includeInactive);
        var dtos = paged.Items.Select(MapToResponseDto);
        var result = PaginatedResult<AdvisorResponseDto>.Create(
            dtos, paged.TotalCount, paged.PageNumber, paged.PageSize);
        return Result<PaginatedResult<AdvisorResponseDto>>.Success(result);
    }

    public async Task<Result<byte[]>> ExportPdfAsync(string? search, string? sortBy, string? sortDir, bool includeInactive = false)
    {
        var advisors = await _advisorRepository.GetAllForExportAsync(search, sortBy, sortDir, includeInactive);
        var definition = new PdfTableDefinition
        {
            Title = "Asesores Institucionales - TecNM Campus Monclova",
            Headers = new List<string> { "ID", "Nombre Completo", "Tipo", "Título", "Teléfono", "Estado", "Creado el", "Actualizado el" },
            Rows = advisors.Select(a => new List<string>
            {
                a.Id.ToString(),
                a.FullName,
                a.AdvisorType == AdvisorType.Internal ? "Interno" : "Externo",
                a.Title ?? string.Empty,
                a.Phone ?? string.Empty,
                a.IsActive ? "Activo" : "Inactivo",
                a.CreatedAt.ToString("dd/MM/yyyy"),
                a.UpdatedAt.ToString("dd/MM/yyyy")
            }).ToList()
        };

        return Result<byte[]>.Success(PdfExportService.GenerateTablePdf(definition));
    }

    public async Task<Result<List<AdvisorOptionDto>>> GetOptionsAsync()
    {
        var advisors = await _advisorRepository.GetOptionsAsync();
        var dtos = advisors.Select(a => new AdvisorOptionDto
        {
            Id = a.Id,
            FullName = a.FullName
        }).ToList();

        return Result<List<AdvisorOptionDto>>.Success(dtos);
    }

    public async Task<Result<AdvisorResponseDto>> GetAdvisorByIdAsync(long id)
    {
        var advisor = await _advisorRepository.GetByIdAsync(id);
        if (advisor == null)
        {
            return Result<AdvisorResponseDto>.Failure("Asesor no encontrado.");
        }

        return Result<AdvisorResponseDto>.Success(MapToResponseDto(advisor));
    }

    public async Task<Result<AdvisorResponseDto>> GetMeAsync(long userId)
    {
        if (userId <= 0)
            return Result<AdvisorResponseDto>.Failure("Sesión no autenticada.", 401);

        var advisor = await _advisorRepository.GetByUserIdAsync(userId);
        if (advisor == null)
            return Result<AdvisorResponseDto>.Failure("No se encontró un perfil de asesor asociado a tu cuenta.", 404);

        return Result<AdvisorResponseDto>.Success(MapToResponseDto(advisor));
    }

    public async Task<Result<AdvisorResponseDto>> CreateAdvisorAsync(CreateAdvisorDto dto)
    {
        var existing = await _advisorRepository.GetByUserIdAsync(dto.UserId);
        if (existing != null)
        {
            return Result<AdvisorResponseDto>.Failure("Ya existe un asesor registrado con este usuario.");
        }

        var advisor = new Advisor
        {
            UserId = dto.UserId,
            DepartmentId = dto.DepartmentId,
            AdvisorType = dto.AdvisorType,
            FullName = dto.FullName,
            Title = dto.Title,
            Phone = dto.Phone,
            IsActive = true,
            IsVisible = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        var created = await _advisorRepository.AddAsync(advisor);

        await _roleRepository.EnsureUserRoleAsync(created.UserId, "advisor", _currentUser.UserId);

        return Result<AdvisorResponseDto>.Success(MapToResponseDto(created));
    }

    public async Task<Result<AdvisorResponseDto>> UpdateAdvisorAsync(long id, UpdateAdvisorDto dto)
    {
        var advisor = await _advisorRepository.GetByIdAsync(id);
        if (advisor == null)
        {
            return Result<AdvisorResponseDto>.Failure("Asesor no encontrado.");
        }

        advisor.DepartmentId = dto.DepartmentId;
        advisor.AdvisorType = dto.AdvisorType;
        advisor.FullName = dto.FullName;
        advisor.Title = dto.Title;
        advisor.Phone = dto.Phone;
        advisor.UpdatedAt = DateTime.UtcNow;
        advisor.UpdatedBy = _currentUser.UserId;

        await _advisorRepository.UpdateAsync(advisor);
        return Result<AdvisorResponseDto>.Success(MapToResponseDto(advisor));
    }

    public async Task<Result<bool>> SoftDeleteAdvisorAsync(long id, long deletedByUserId)
    {
        var advisor = await _advisorRepository.GetByIdAsync(id);
        if (advisor == null)
        {
            return Result<bool>.Failure("Asesor no encontrado.");
        }

        advisor.IsActive = false;
        advisor.DeletedAt = DateTime.UtcNow;
        advisor.DeletedBy = deletedByUserId;
        await _advisorRepository.UpdateAsync(advisor);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> ReactivateAdvisorAsync(long id)
    {
        var advisor = await _advisorRepository.GetByIdAsync(id);
        if (advisor == null)
        {
            return Result<bool>.Failure("Asesor no encontrado.");
        }

        advisor.IsActive = true;
        advisor.DeletedAt = null;
        advisor.DeletedBy = null;
        advisor.UpdatedAt = DateTime.UtcNow;
        await _advisorRepository.UpdateAsync(advisor);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> AssignAdvisorAsync(AssignAdvisorDto dto)
    {
        var advisor = await _advisorRepository.GetByIdAsync(dto.AdvisorId);
        if (advisor == null)
        {
            return Result<bool>.Failure("Asesor no encontrado.");
        }

        var project = await _projectRepository.GetByIdAsync(dto.ProjectId);
        if (project == null)
        {
            return Result<bool>.Failure("Anteproyecto no encontrado.", 404);
        }

        project.AdvisorId = dto.AdvisorId;
        project.UpdatedAt = DateTime.UtcNow;
        project.UpdatedBy = _currentUser.UserId;
        await _projectRepository.UpdateAsync(project);

        return Result<bool>.Success(true);
    }

    private static AdvisorResponseDto MapToResponseDto(Advisor advisor)
    {
        return new AdvisorResponseDto(
            advisor.Id,
            advisor.UserId,
            advisor.DepartmentId,
            advisor.AdvisorType.ToString().ToLowerInvariant(),
            advisor.FullName,
            advisor.Title,
            advisor.Phone,
            advisor.IsActive,
            advisor.CreatedAt,
            advisor.UpdatedAt,
            advisor.IsVisible,
            advisor.DisplayOrder,
            advisor.CreatedBy,
            advisor.UpdatedBy,
            advisor.DeletedBy,
            advisor.DeletedAt
        );
    }
}
