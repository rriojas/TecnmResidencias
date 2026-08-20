using Microsoft.EntityFrameworkCore;
using TecNM.Residency.Common;

namespace TecNM.Residency.Activities;

public class ActivityRepository : IActivityRepository
{
    private readonly AppDbContext _context;

    public ActivityRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<WeeklyActivity>> GetByProjectIdAsync(long projectId)
    {
        return await _context.WeeklyActivities
            .Include(a => a.Progresses)
            .Where(a => a.ProjectId == projectId && a.IsActive)
            .OrderBy(a => a.ActivityNumber)
            .ToListAsync();
    }

    public async Task<WeeklyActivity?> GetByIdAsync(long id)
    {
        return await _context.WeeklyActivities
            .Include(a => a.Progresses)
            .FirstOrDefaultAsync(a => a.Id == id && a.IsActive);
    }

    public async Task<WeeklyActivity> CreateActivityAsync(WeeklyActivity activity)
    {
        _context.WeeklyActivities.Add(activity);
        await _context.SaveChangesAsync();
        return activity;
    }

    public async Task SaveProgressAsync(WeeklyProgress progress)
    {
        var existing = await _context.WeeklyProgresses
            .FirstOrDefaultAsync(p => p.ActivityId == progress.ActivityId && p.WeekNumber == progress.WeekNumber);

        if (existing != null)
        {
            existing.Status = progress.Status;
            existing.Notes = progress.Notes;
            existing.UpdatedAt = DateTime.UtcNow;
            _context.WeeklyProgresses.Update(existing);
        }
        else
        {
            _context.WeeklyProgresses.Add(progress);
        }

        await _context.SaveChangesAsync();
    }

    public async Task UpdateActivityAsync(WeeklyActivity activity)
    {
        _context.WeeklyActivities.Update(activity);
        await _context.SaveChangesAsync();
    }
}
