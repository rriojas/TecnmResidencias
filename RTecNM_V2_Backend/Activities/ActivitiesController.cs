using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecNM.Residency.Projects;

namespace TecNM.Residency.Activities;

[ApiController]
[Authorize]
[Route("api/v1/projects/{projectId:long}/[controller]")]
public class ActivitiesController : ControllerBase
{
    private readonly IActivityService _activityService;
    private readonly IProjectService _projectService;

    public ActivitiesController(IActivityService activityService, IProjectService projectService)
    {
        _activityService = activityService;
        _projectService = projectService;
    }

    private async Task<IActionResult?> EnsureProjectAccessAsync(long projectId)
    {
        var access = await _projectService.CanAccessProjectAsync(projectId);
        return access.IsSuccess ? null : StatusCode(access.StatusCode ?? 403, new { message = access.ErrorMessage });
    }

    [HttpGet]
    public async Task<IActionResult> GetSchedule(long projectId)
    {
        var denied = await EnsureProjectAccessAsync(projectId);
        if (denied is not null) return denied;

        var result = await _activityService.GetScheduleByProjectIdAsync(projectId);
        return Ok(result.Data);
    }

    [HttpPost]
    [Authorize(Roles = "student")]
    public async Task<IActionResult> CreateActivity(long projectId, [FromBody] CreateActivityDto dto)
    {
        var denied = await EnsureProjectAccessAsync(projectId);
        if (denied is not null) return denied;

        var projectResult = await _projectService.GetProjectByIdAsync(projectId);
        if (projectResult.IsSuccess && projectResult.Data != null)
        {
            if (projectResult.Data.IsCompleted || projectResult.Data.IsReadOnly)
                return StatusCode(400, new { message = "No se pueden agregar actividades a un proyecto concluido o no aprobado." });
        }

        var payload = dto with { ProjectId = projectId };
        var result = await _activityService.CreateActivityAsync(payload);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpPost("progress")]
    [Authorize(Roles = "student")]
    public async Task<IActionResult> SaveProgress(long projectId, [FromBody] SaveWeeklyProgressDto dto)
    {
        var denied = await EnsureProjectAccessAsync(projectId);
        if (denied is not null) return denied;

        var projectResult = await _projectService.GetProjectByIdAsync(projectId);
        if (projectResult.IsSuccess && projectResult.Data != null)
        {
            if (projectResult.Data.IsCompleted || projectResult.Data.IsReadOnly)
                return StatusCode(400, new { message = "No se puede modificar el avance semanal de un proyecto concluido o archivado." });
        }

        var result = await _activityService.SaveWeeklyProgressAsync(dto);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(new { message = "Avance semanal guardado correctamente." });
    }
}
