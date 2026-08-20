namespace TecNM.Residency.Activities;

public interface IActivityRepository
{
    Task<List<WeeklyActivity>> GetByProjectIdAsync(long projectId);
    Task<WeeklyActivity?> GetByIdAsync(long id);
    Task<WeeklyActivity> CreateActivityAsync(WeeklyActivity activity);
    Task SaveProgressAsync(WeeklyProgress progress);
    Task UpdateActivityAsync(WeeklyActivity activity);
}
