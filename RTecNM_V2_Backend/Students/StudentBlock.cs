using TecNM.Residency.Auth;
using TecNM.Residency.Common;

namespace TecNM.Residency.Students;

public class StudentBlock : BaseEntity
{
    public long StudentId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public long BlockedBy { get; set; }
    public DateTime BlockedAt { get; set; } = DateTime.UtcNow;
    public new bool IsActive { get; set; } = true;
    public DateTime? UnblockedAt { get; set; }
    public long? UnblockedBy { get; set; }

    public Student? Student { get; set; }
    public User? BlockedByUser { get; set; }
    public User? UnblockedByUser { get; set; }
}