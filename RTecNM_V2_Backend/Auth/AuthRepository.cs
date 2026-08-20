using Microsoft.EntityFrameworkCore;
using TecNM.Residency.Common;

namespace TecNM.Residency.Auth;

public class AuthRepository : IAuthRepository
{
    private readonly AppDbContext _context;

    public AuthRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        var cleanEmail = (email ?? "").Trim().ToLowerInvariant();
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == cleanEmail);
    }

    public async Task<User> AddUserAsync(User user)
    {
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task UpdatePasswordHashAsync(long userId, string newHash)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            user.PasswordHash = newHash;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<Dictionary<long, string>> GetUserDisplayNamesByIdsAsync(List<long> userIds)
    {
        var ids = (userIds ?? new List<long>()).Where(id => id > 0).Distinct().ToList();
        var result = new Dictionary<long, string>();

        if (ids.Count == 0) return result;

        var advisorNames = await _context.Advisors
            .Where(a => ids.Contains(a.UserId) && a.IsActive)
            .Select(a => new { a.UserId, a.FullName })
            .ToDictionaryAsync(a => a.UserId, a => a.FullName);

        var studentNames = await _context.Students
            .Where(s => ids.Contains(s.UserId) && s.IsActive)
            .Select(s => new { s.UserId, Name = (s.FirstName + " " + s.LastName).Trim() })
            .ToDictionaryAsync(s => s.UserId, s => s.Name);

        var emails = await _context.Users
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToDictionaryAsync(u => u.Id, u => u.Email);

        foreach (var id in ids)
        {
            if (advisorNames.TryGetValue(id, out var advisorName))
                result[id] = advisorName;
            else if (studentNames.TryGetValue(id, out var studentName))
                result[id] = studentName;
            else if (emails.TryGetValue(id, out var email))
                result[id] = email;
        }

        return result;
    }
}
