using Microsoft.EntityFrameworkCore;
using TecNM.Residency.Common;

namespace TecNM.Residency.Notifications;

public class NotificationRepository : INotificationRepository
{
    private readonly AppDbContext _context;

    public NotificationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Notification> CreateAsync(Notification notification)
    {
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
        return notification;
    }

    public async Task<Notification?> GetByIdAsync(long id)
    {
        return await _context.Notifications
            .Include(n => n.Sender)
            .Include(n => n.TargetUser)
            .Include(n => n.Career)
            .Include(n => n.Reads)
            .FirstOrDefaultAsync(n => n.Id == id && n.IsActive);
    }

    public async Task<PaginatedResult<Notification>> GetHistoryAsync(NotificationHistoryQuery query)
    {
        IQueryable<Notification> q = _context.Notifications
            .AsNoTracking()
            .Include(n => n.Sender)
            .Include(n => n.TargetUser)
            .Include(n => n.Career)
            .Include(n => n.Reads)
            .Where(n => n.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            q = q.Where(n => n.Title.ToLower().Contains(term) || n.Description.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            var r = query.Role.Trim().ToLowerInvariant();
            q = q.Where(n => n.TargetRoles == "all" || n.TargetRoles.ToLower().Contains(r));
        }

        if (query.CareerId.HasValue && query.CareerId.Value > 0)
        {
            q = q.Where(n => n.CareerId == query.CareerId.Value || n.CareerId == null);
        }

        if (query.ActiveOnly.HasValue && query.ActiveOnly.Value)
        {
            var now = DateTime.UtcNow;
            q = q.Where(n => n.ExpiresAt >= now);
        }

        q = q.OrderByDescending(n => n.CreatedAt);

        return await q.ToPaginatedAsync(query.PageNumber, query.PageSize);
    }

    public async Task<List<Notification>> GetPendingForUserAsync(long userId, string role, long? careerId, string? residencyModality)
    {
        var now = DateTime.UtcNow;
        var normalizedRole = (role ?? string.Empty).Trim().ToLowerInvariant();

        // Obtener ids de notificaciones ya leídas por el usuario
        var readNotificationIds = await _context.Database
            .SqlQueryRaw<long>(
                "SELECT notification_id AS \"Value\" FROM user_notification_reads WHERE user_id = {0}", userId)
            .ToListAsync();

        var query = _context.Notifications
            .AsNoTracking()
            .Include(n => n.Career)
            .Include(n => n.Sender)
            .Where(n => n.IsActive && n.ExpiresAt >= now);

        if (readNotificationIds.Count > 0)
        {
            query = query.Where(n => !readNotificationIds.Contains(n.Id));
        }

        var candidates = await query.ToListAsync();

        // Filtrar en memoria para asegurar matching exacto de roles, carrera y modalidad
        return candidates.Where(n =>
        {
            // 0. Si está dirigida a un usuario específico (pruebas o envío unitario)
            if (n.TargetUserId.HasValue)
            {
                return n.TargetUserId.Value == userId;
            }

            // 1. Rol
            var targetRoles = n.TargetRoles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                           .Select(r => r.ToLowerInvariant());
            bool matchesRole = targetRoles.Contains("all") || targetRoles.Contains(normalizedRole);
            if (!matchesRole) return false;

            // 2. Carrera: Roles institucionales globales sin carrera (director, admin) no son excluidos por filtro de carrera
            if (n.CareerId.HasValue && n.CareerId.Value > 0)
            {
                bool isGlobalRole = normalizedRole is "director" or "admin";
                if (!isGlobalRole && (!careerId.HasValue || careerId.Value != n.CareerId.Value))
                    return false;
            }

            // 3. Modalidad de Residencia
            if (!string.IsNullOrWhiteSpace(n.ResidencyModality))
            {
                var targetMod = n.ResidencyModality.Trim().ToLowerInvariant();
                bool isStudentAccreditation = !string.IsNullOrWhiteSpace(residencyModality) &&
                    (residencyModality.Contains("innovatec", StringComparison.OrdinalIgnoreCase) ||
                     residencyModality.Contains("hackatec", StringComparison.OrdinalIgnoreCase) ||
                     residencyModality.StartsWith("acreditacion", StringComparison.OrdinalIgnoreCase));

                if (targetMod is "acreditacion" or "innovatec" or "hackatec")
                {
                    if (!isStudentAccreditation) return false;
                }
                else if (targetMod is "regular")
                {
                    if (isStudentAccreditation) return false;
                }
                else if (!string.IsNullOrWhiteSpace(residencyModality) && !residencyModality.Contains(targetMod, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }).OrderByDescending(n => n.CreatedAt).ToList();
    }

    public async Task<bool> MarkAsReadAsync(long notificationId, long userId)
    {
        var alreadyRead = await _context.Set<UserNotificationRead>()
            .AnyAsync(r => r.NotificationId == notificationId && r.UserId == userId);

        if (alreadyRead) return true;

        var read = new UserNotificationRead
        {
            NotificationId = notificationId,
            UserId = userId,
            ReadAt = DateTime.UtcNow
        };

        _context.Set<UserNotificationRead>().Add(read);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> HasUserReadAsync(long notificationId, long userId)
    {
        return await _context.Set<UserNotificationRead>()
            .AnyAsync(r => r.NotificationId == notificationId && r.UserId == userId);
    }

    public async Task<bool> DeleteAsync(long id, long deletedBy)
    {
        var item = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id);
        if (item == null) return false;

        item.IsActive = false;
        item.DeletedAt = DateTime.UtcNow;
        item.DeletedBy = deletedBy;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<NotificationUserOptionDto>> SearchUsersAsync(string? search, string? role)
    {
        var q = _context.Users
            .AsNoTracking()
            .Where(u => u.IsActive && !string.IsNullOrWhiteSpace(u.Email));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            q = q.Where(u =>
                (u.FirstName != null && u.FirstName.ToLower().Contains(term)) ||
                (u.LastName != null && u.LastName.ToLower().Contains(term)) ||
                (u.LastName2 != null && u.LastName2.ToLower().Contains(term)) ||
                u.Email.ToLower().Contains(term) ||
                (u.ControlNumber != null && u.ControlNumber.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            var r = role.Trim().ToLowerInvariant();
            if (Enum.TryParse<TecNM.Residency.Auth.UserRole>(r, true, out var parsedRole))
            {
                q = q.Where(u => u.Role == parsedRole);
            }
        }

        var users = await q
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .Take(30)
            .Select(u => new NotificationUserOptionDto
            {
                Id = u.Id,
                FullName = $"{u.FirstName} {u.LastName} {u.LastName2}".Trim(),
                Email = u.Email,
                Role = u.Role.ToString(),
                ControlNumber = u.ControlNumber,
                CareerName = null
            })
            .ToListAsync();

        return users;
    }
}
