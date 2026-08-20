using TecNM.Residency.Searches.Dtos;

namespace TecNM.Residency.Searches;

public class SearchSourceConfig
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ViewName { get; set; } = string.Empty;
    public string KeyColumn { get; set; } = "id";
    public List<SearchColumnMetadataDto> Columns { get; set; } = new();
}

public class SearchRegistry
{
    private readonly Dictionary<string, SearchSourceConfig> _sources = new(StringComparer.OrdinalIgnoreCase);

    public SearchRegistry()
    {
        RegisterDefaultSources();
    }

    public void RegisterSource(SearchSourceConfig config)
    {
        _sources[config.Key] = config;
    }

    public SearchSourceConfig? GetSource(string key)
    {
        _sources.TryGetValue(key, out var source);
        return source;
    }

    public List<SearchSourceMetadataDto> GetAllMetadata()
    {
        return _sources.Values.Select(s => new SearchSourceMetadataDto
        {
            Key = s.Key,
            DisplayName = s.DisplayName,
            KeyColumn = s.KeyColumn,
            Columns = s.Columns
        }).ToList();
    }

    private void RegisterDefaultSources()
    {
        // 1. STUDENTS
        RegisterSource(new SearchSourceConfig
        {
            Key = "STUDENTS",
            DisplayName = "Alumnos",
            ViewName = "vw_search_students",
            KeyColumn = "id",
            Columns = new List<SearchColumnMetadataDto>
            {
                new() { Name = "id", DisplayName = "ID", Type = "Integer", IsSearchable = false },
                new() { Name = "control_number", DisplayName = "No. Control", Type = "Text", IsSearchable = true },
                new() { Name = "full_name", DisplayName = "Nombre Completo", Type = "Text", IsSearchable = true },
                new() { Name = "email", DisplayName = "Correo Electrónico", Type = "Text", IsSearchable = true },
                new() { Name = "curp", DisplayName = "CURP", Type = "Text", IsSearchable = true }
            }
        });

        // 2. ADVISORS
        RegisterSource(new SearchSourceConfig
        {
            Key = "ADVISORS",
            DisplayName = "Asesores / Docentes",
            ViewName = "vw_search_advisors",
            KeyColumn = "id",
            Columns = new List<SearchColumnMetadataDto>
            {
                new() { Name = "id", DisplayName = "ID", Type = "Integer", IsSearchable = false },
                new() { Name = "full_name", DisplayName = "Nombre Asesor", Type = "Text", IsSearchable = true },
                new() { Name = "title", DisplayName = "Título", Type = "Text", IsSearchable = true },
                new() { Name = "advisor_type", DisplayName = "Tipo Asesor", Type = "Text", IsSearchable = true },
                new() { Name = "email", DisplayName = "Correo", Type = "Text", IsSearchable = true },
                new() { Name = "phone", DisplayName = "Teléfono", Type = "Text", IsSearchable = true }
            }
        });

        // 3. PROJECTS
        RegisterSource(new SearchSourceConfig
        {
            Key = "PROJECTS",
            DisplayName = "Proyectos de Residencia",
            ViewName = "vw_search_projects",
            KeyColumn = "id",
            Columns = new List<SearchColumnMetadataDto>
            {
                new() { Name = "id", DisplayName = "ID", Type = "Integer", IsSearchable = false },
                new() { Name = "title", DisplayName = "Título de Proyecto", Type = "Text", IsSearchable = true },
                new() { Name = "project_type", DisplayName = "Tipo", Type = "Text", IsSearchable = true },
                new() { Name = "status", DisplayName = "Estado", Type = "Text", IsSearchable = true },
                new() { Name = "student_name", DisplayName = "Alumno", Type = "Text", IsSearchable = true },
                new() { Name = "advisor_name", DisplayName = "Asesor", Type = "Text", IsSearchable = true }
            }
        });

        // 4. COMPANIES
        RegisterSource(new SearchSourceConfig
        {
            Key = "COMPANIES",
            DisplayName = "Empresas y Convenios",
            ViewName = "vw_search_companies",
            KeyColumn = "id",
            Columns = new List<SearchColumnMetadataDto>
            {
                new() { Name = "id", DisplayName = "ID", Type = "Integer", IsSearchable = false },
                new() { Name = "name", DisplayName = "Razón Social / Nombre", Type = "Text", IsSearchable = true },
                new() { Name = "rfc", DisplayName = "RFC", Type = "Text", IsSearchable = true },
                new() { Name = "sector", DisplayName = "Sector", Type = "Text", IsSearchable = true },
                new() { Name = "contact_name", DisplayName = "Contacto", Type = "Text", IsSearchable = true },
                new() { Name = "contact_email", DisplayName = "Correo Contacto", Type = "Text", IsSearchable = true }
            }
        });
    }
}
