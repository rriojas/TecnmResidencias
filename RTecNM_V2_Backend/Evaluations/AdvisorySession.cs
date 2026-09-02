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

    public string ReviewStatus { get; set; } = "pending"; // pending | approved | observed
    public string? ReviewNotes { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public long? ReviewedBy { get; set; }

    public Project? Project { get; set; }
    public Advisor? Advisor { get; set; }
}
