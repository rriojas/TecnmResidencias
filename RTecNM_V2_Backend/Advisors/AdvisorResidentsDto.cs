namespace TecNM.Residency.Advisors;

public class AdvisorResidentsResponseDto
{
    public long AdvisorId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public long DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int TotalResidents { get; set; }
    public List<AdvisorResidentItemDto> Residents { get; set; } = new();
}

public class AdvisorResidentItemDto
{
    public long StudentId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string ControlNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public long CareerId { get; set; }
    public string CareerName { get; set; } = string.Empty;
    public long? ProjectId { get; set; }
    public string? ProjectTitle { get; set; }
    public string? ProjectStatus { get; set; }
    public string? CompanyName { get; set; }
    public int AdvisoryCount { get; set; }
}
