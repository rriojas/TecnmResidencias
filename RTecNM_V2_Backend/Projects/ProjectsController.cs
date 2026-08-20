using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecNM.Residency.Auth;
using TecNM.Residency.Common;

namespace TecNM.Residency.Projects;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly ICurrentUserService _currentUser;

    public ProjectsController(IProjectService projectService, ICurrentUserService currentUser)
    {
        _projectService = projectService;
        _currentUser = currentUser;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectDto dto)
    {
        var result = await _projectService.CreateProjectAsync(dto);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQuery query, [FromQuery] string? status, [FromQuery] bool includeInactive = false)
    {
        var result = await _projectService.GetPagedAsync(query, status, includeInactive);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpGet("export")]
    [Authorize(Roles = "admin,departmenthead")]
    public async Task<IActionResult> ExportPdf([FromQuery] string? status, [FromQuery] string? search, [FromQuery] string? sortBy, [FromQuery] string? sortDir, [FromQuery] bool includeInactive = false)
    {
        var result = await _projectService.ExportPdfAsync(status, search, sortBy, sortDir, includeInactive);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return File(result.Data!, "application/pdf", "anteproyectos_tecnm.pdf");
    }

    [HttpGet("options")]
    public async Task<IActionResult> GetOptions()
    {
        var result = await _projectService.GetOptionsAsync();
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyProjects([FromQuery] PaginationQuery query, [FromQuery] bool includeInactive = false)
    {
        var result = await _projectService.GetMyProjectsPagedAsync(query, includeInactive);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 404, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpGet("me/current")]
    public async Task<IActionResult> GetMyCurrentProject()
    {
        var result = await _projectService.GetMyCurrentProjectAsync();
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 404, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpGet("advisor/me")]
    public async Task<IActionResult> GetAdvisorProjects([FromQuery] PaginationQuery query)
    {
        var result = await _projectService.GetAdvisorProjectsPagedAsync(query);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 404, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpGet("student/{studentId:long}")]
    public async Task<IActionResult> GetByStudentId(long studentId, [FromQuery] PaginationQuery query)
    {
        if (!_currentUser.IsInRole(UserRole.Admin) && !_currentUser.IsInRole(UserRole.DepartmentHead))
            return StatusCode(403, new { message = "No tiene permisos para consultar anteproyectos de otros estudiantes." });

        var result = await _projectService.GetProjectsByStudentIdPagedAsync(studentId, query);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _projectService.GetProjectByIdAsync(id);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 404, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpPatch("{id:long}/status")]
    [RequirePermission("projects.review")]
    public async Task<IActionResult> UpdateStatus(long id, [FromBody] UpdateProjectStatusDto dto)
    {
        var result = await _projectService.UpdateStatusAsync(id, dto);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateProjectDto dto)
    {
        var result = await _projectService.UpdateProjectAsync(id, dto);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpPatch("{id:long}/submit")]
    public async Task<IActionResult> Submit(long id)
    {
        var result = await _projectService.SendToReviewAsync(id);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpGet("{id:long}/pdf")]
    public async Task<IActionResult> GetPdf(long id)
    {
        var result = await _projectService.GetProjectPdfAsync(id);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return File(result.Data!, "application/pdf", $"anteproyecto_{id}.pdf");
    }

    [HttpPatch("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id)
    {
        var result = await _projectService.CancelProjectAsync(id);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(new { message = "Solicitud de anteproyecto cancelada correctamente." });
    }

    [HttpDelete("{id:long}")]
    [RequirePermission("projects.delete")]
    public async Task<IActionResult> SoftDelete(long id)
    {
        var result = await _projectService.SoftDeleteAsync(id);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return NoContent();
    }

    [HttpPatch("{id:long}/activate")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Activate(long id)
    {
        var result = await _projectService.ActivateAsync(id);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(new { message = "Anteproyecto reactivado exitosamente." });
    }
}
