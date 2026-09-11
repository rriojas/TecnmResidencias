using Microsoft.AspNetCore.Http;

namespace TecNM.Residency.Projects;

public class CreateAccreditationDto
{
    public string EventType { get; set; } = "innovatec"; // "innovatec" a nivel nacional
    public string ProjectTitle { get; set; } = string.Empty;
    public long? CompanyId { get; set; }
    public IFormFile File { get; set; } = null!;
}

public class ReviewAccreditationDto
{
    public bool Approved { get; set; }
    public bool Denied { get; set; } = false; // true si la acreditación no es aceptada / rechazada definitivamente
    public string? Observations { get; set; }
}

public class ResubmitAccreditationDto
{
    public IFormFile File { get; set; } = null!;
}
