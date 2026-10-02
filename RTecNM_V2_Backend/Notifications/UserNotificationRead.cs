using TecNM.Residency.Auth;

namespace TecNM.Residency.Notifications;

public class UserNotificationRead
{
    public long Id { get; set; }
    public long NotificationId { get; set; }
    public long UserId { get; set; }
    public DateTime ReadAt { get; set; } = DateTime.UtcNow;

    public Notification? Notification { get; set; }
    public User? User { get; set; }
}
