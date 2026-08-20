using TecNM.Residency.Common;
using TecNM.Residency.Projects;

namespace TecNM.Residency.Evaluations;

public class Evaluation : BaseEntity
{
    public long ProjectId { get; set; }
    public long EvaluatorId { get; set; }
    public string EvaluationPeriod { get; set; } = "partial_1";
    public decimal Score { get; set; }
    public string? Feedback { get; set; }

    public Project? Project { get; set; }
}
