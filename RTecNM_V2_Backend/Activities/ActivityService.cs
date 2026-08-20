using TecNM.Residency.Common;

namespace TecNM.Residency.Activities;

public class ActivityService : IActivityService
{
    private readonly IActivityRepository _repository;

    public ActivityService(IActivityRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<List<WeeklyActivityDto>>> GetScheduleByProjectIdAsync(long projectId)
    {
        var activities = await _repository.GetByProjectIdAsync(projectId);
        
        var dtos = activities.Select(a => MapToDto(a)).ToList();
        return Result<List<WeeklyActivityDto>>.Success(dtos);
    }

    public async Task<Result<WeeklyActivityDto>> CreateActivityAsync(CreateActivityDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return Result<WeeklyActivityDto>.Failure("La descripción de la actividad es obligatoria.");

        var activity = new WeeklyActivity
        {
            ProjectId = dto.ProjectId,
            ActivityNumber = dto.ActivityNumber,
            Title = dto.Title.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Pre-generate 26 week progress slots
        for (int w = 1; w <= 26; w++)
        {
            activity.Progresses.Add(new WeeklyProgress
            {
                WeekNumber = w,
                Status = "pending",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        var created = await _repository.CreateActivityAsync(activity);
        return Result<WeeklyActivityDto>.Success(MapToDto(created));
    }

    public async Task<Result<bool>> SaveWeeklyProgressAsync(SaveWeeklyProgressDto dto)
    {
        if (dto.WeekNumber < 1 || dto.WeekNumber > 26)
            return Result<bool>.Failure("El número de semana debe estar entre 1 y 26.");

        var activity = await _repository.GetByIdAsync(dto.ActivityId);
        if (activity == null)
            return Result<bool>.Failure("Actividad no encontrada.", 404);

        var validStatuses = new[] { "pending", "in_progress", "completed", "pendiente", "en_proceso", "completado" };
        if (!validStatuses.Contains(dto.Status.ToLowerInvariant()))
            return Result<bool>.Failure($"Estado '{dto.Status}' no válido.");

        var progress = new WeeklyProgress
        {
            ActivityId = dto.ActivityId,
            WeekNumber = dto.WeekNumber,
            Status = dto.Status.ToLowerInvariant(),
            Notes = dto.Notes,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.SaveProgressAsync(progress);
        return Result<bool>.Success(true);
    }

    private static WeeklyActivityDto MapToDto(WeeklyActivity a)
    {
        var progresses = a.Progresses
            .Where(p => p.IsActive)
            .OrderBy(p => p.WeekNumber)
            .Select(p => new WeeklyProgressDto(
                p.Id,
                p.WeekNumber,
                p.Status,
                p.Notes
            ))
            .ToList();

        // Ensure 26 weeks exist in response even if empty
        if (progresses.Count < 26)
        {
            var existingWeeks = progresses.Select(p => p.WeekNumber).ToHashSet();
            for (int w = 1; w <= 26; w++)
            {
                if (!existingWeeks.Contains(w))
                {
                    progresses.Add(new WeeklyProgressDto(0, w, "pending", null));
                }
            }
            progresses = progresses.OrderBy(p => p.WeekNumber).ToList();
        }

        return new WeeklyActivityDto(
            a.Id,
            a.ProjectId,
            a.ActivityNumber,
            a.Title,
            progresses
        );
    }
}
