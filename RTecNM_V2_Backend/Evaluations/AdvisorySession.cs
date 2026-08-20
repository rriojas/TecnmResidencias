using TecNM.Residency.Advisors;
using TecNM.Residency.Common;
using TecNM.Residency.Projects;

namespace TecNM.Residency.Evaluations;

public class AdvisorySession : BaseEntity
{
    public long ProjectId { get; set; }
    public long AdvisorId { get; set; }
    public DateTime SessionDate { get; set; } = DateTime.UtcNow;
    public string TopicsCovered { get; set; } = string.Empty;
    public string? StudentAgreements { get; set; }

    public Project? Project { get; set; }
    public Advisor? Advisor { get; set; }
}
