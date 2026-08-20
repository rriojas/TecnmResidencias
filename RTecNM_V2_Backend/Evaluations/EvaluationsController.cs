using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecNM.Residency.Auth;
using TecNM.Residency.Common;
using TecNM.Residency.Projects;

namespace TecNM.Residency.Evaluations;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class EvaluationsController : ControllerBase
{
    private readonly IEvaluationService _evaluationService;
    private readonly IProjectService _projectService;

    public EvaluationsController(IEvaluationService evaluationService, IProjectService projectService)
    {
        _evaluationService = evaluationService;
        _projectService = projectService;
    }

    [HttpPost]
    [Authorize(Roles = "admin,departmenthead,advisor")]
    public async Task<IActionResult> Grade([FromBody] GradeEvaluationDto dto)
    {
        var access = await _projectService.CanAccessProjectAsync(dto.ProjectId);
        if (!access.IsSuccess)
            return StatusCode(access.StatusCode ?? 403, new { message = access.ErrorMessage });

        var result = await _evaluationService.GradeEvaluationAsync(dto);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpGet("project/{projectId:long}")]
    public async Task<IActionResult> GetByProject(long projectId, [FromQuery] PaginationQuery query)
    {
        var access = await _projectService.CanAccessProjectAsync(projectId);
        if (!access.IsSuccess)
            return StatusCode(access.StatusCode ?? 403, new { message = access.ErrorMessage });

        var result = await _evaluationService.GetEvaluationsByProjectIdPagedAsync(projectId, query);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpPost("sessions")]
    [Authorize(Roles = "admin,departmenthead,advisor")]
    [RequirePermission("advisories.session.record")]
    public async Task<IActionResult> RecordSession([FromBody] CreateAdvisorySessionDto dto)
    {
        var access = await _projectService.CanAccessProjectAsync(dto.ProjectId);
        if (!access.IsSuccess)
            return StatusCode(access.StatusCode ?? 403, new { message = access.ErrorMessage });

        var result = await _evaluationService.RecordAdvisorySessionAsync(dto);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpGet("sessions")]
    [Authorize(Roles = "admin,departmenthead")]
    public async Task<IActionResult> GetAllSessions([FromQuery] PaginationQuery query, [FromQuery] long? projectId, [FromQuery] bool includeInactive = false)
    {
        var result = await _evaluationService.GetAllAdvisorySessionsPagedAsync(query, projectId, includeInactive);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpGet("sessions/export")]
    [Authorize(Roles = "admin,departmenthead")]
    public async Task<IActionResult> ExportSessionsPdf([FromQuery] long? projectId, [FromQuery] string? search, [FromQuery] string? sortBy, [FromQuery] string? sortDir, [FromQuery] bool includeInactive = false)
    {
        var result = await _evaluationService.ExportSessionsPdfAsync(projectId, search, sortBy, sortDir, includeInactive);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return File(result.Data!, "application/pdf", "asesorias_tecnm.pdf");
    }

    [HttpGet("sessions/project/{projectId:long}")]
    public async Task<IActionResult> GetSessionsByProject(long projectId, [FromQuery] PaginationQuery query, [FromQuery] bool includeInactive = false)
    {
        var access = await _projectService.CanAccessProjectAsync(projectId);
        if (!access.IsSuccess)
            return StatusCode(access.StatusCode ?? 403, new { message = access.ErrorMessage });

        var result = await _evaluationService.GetAdvisorySessionsByProjectIdPagedAsync(projectId, query, includeInactive);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }
}
