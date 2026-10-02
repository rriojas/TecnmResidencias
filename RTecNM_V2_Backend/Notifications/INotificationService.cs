using TecNM.Residency.Common;

namespace TecNM.Residency.Notifications;

public interface INotificationService
{
    Task<Result<NotificationResponseDto>> CreateNotificationAsync(CreateNotificationDto dto);
    Task<Result<PaginatedResult<NotificationResponseDto>>> GetHistoryAsync(NotificationHistoryQuery query);
    Task<Result<List<PendingNotificationDto>>> GetPendingNotificationsAsync();
    Task<Result<bool>> MarkAsReadAsync(long notificationId);
    Task<Result<bool>> DeleteAsync(long notificationId);
    Task<Result<List<NotificationUserOptionDto>>> SearchUsersAsync(string? search, string? role);
}
