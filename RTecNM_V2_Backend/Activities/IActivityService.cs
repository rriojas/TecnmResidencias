using TecNM.Residency.Common;

namespace TecNM.Residency.Activities;

public interface IActivityService
{
    Task<Result<List<WeeklyActivityDto>>> GetScheduleByProjectIdAsync(long projectId);
    Task<Result<WeeklyActivityDto>> CreateActivityAsync(CreateActivityDto dto);
    Task<Result<bool>> SaveWeeklyProgressAsync(SaveWeeklyProgressDto dto);
}
