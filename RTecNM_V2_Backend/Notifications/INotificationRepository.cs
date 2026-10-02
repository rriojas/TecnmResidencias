using TecNM.Residency.Common;

namespace TecNM.Residency.Notifications;

public interface INotificationRepository
{
    Task<Notification> CreateAsync(Notification notification);
    Task<Notification?> GetByIdAsync(long id);
    Task<PaginatedResult<Notification>> GetHistoryAsync(NotificationHistoryQuery query);
    Task<List<Notification>> GetPendingForUserAsync(long userId, string role, long? careerId, string? residencyModality);
    Task<bool> MarkAsReadAsync(long notificationId, long userId);
    Task<bool> HasUserReadAsync(long notificationId, long userId);
    Task<bool> DeleteAsync(long id, long deletedBy);
    Task<List<NotificationUserOptionDto>> SearchUsersAsync(string? search, string? role);
}
