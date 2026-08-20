using TecNM.Residency.Common;

namespace TecNM.Residency.Activities;

public class WeeklyProgress : BaseEntity
{
    public long ActivityId { get; set; }
    public int WeekNumber { get; set; }
    public string Status { get; set; } = "pending";
    public string? Notes { get; set; }

    public WeeklyActivity? Activity { get; set; }
}
