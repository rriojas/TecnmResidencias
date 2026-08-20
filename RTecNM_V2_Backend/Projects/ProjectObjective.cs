using TecNM.Residency.Common;

namespace TecNM.Residency.Projects;

public class ProjectObjective : BaseEntity
{
    public long ProjectId { get; set; }
    public int ObjectiveNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public string? Notes { get; set; }

    public Project? Project { get; set; }
}
