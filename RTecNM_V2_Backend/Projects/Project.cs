using TecNM.Residency.Advisors;
using TecNM.Residency.Common;
using TecNM.Residency.Companies;
using TecNM.Residency.Students;

namespace TecNM.Residency.Projects;

public class Project : BaseEntity
{
    public long StudentId { get; set; }
    public long? AdvisorId { get; set; }
    public long CompanyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ProjectType { get; set; }
    public string ProblemStatement { get; set; } = string.Empty;
    public string Justification { get; set; } = string.Empty;
    public string GeneralObjective { get; set; } = string.Empty;
    public ProjectStatus Status { get; set; } = ProjectStatus.Pending;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? ReviewComments { get; set; }

    public Student? Student { get; set; }
    public Advisor? Advisor { get; set; }
    public Company? Company { get; set; }

    public ICollection<ProjectObjective> Objectives { get; set; } = new List<ProjectObjective>();
}
