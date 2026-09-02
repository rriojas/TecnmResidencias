using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecNM.Residency.Auth;
using TecNM.Residency.Common;

namespace TecNM.Residency.Admin;

[ApiController]
[Authorize(Roles = "admin,vinculacion,departmenthead,academic,academico,director,jefecarrera")]
[Route("api/v1/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IDashboardMetricsService _metricsService;
    private readonly IReportGeneratorService _reportService;
    private readonly ICurrentUserService _currentUser;

    public AdminController(
        IDashboardMetricsService metricsService,
        IReportGeneratorService reportService,
        ICurrentUserService currentUser)
    {
        _metricsService = metricsService;
        _reportService = reportService;
        _currentUser = currentUser;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] long? careerId = null)
    {
        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            careerId = _currentUser.CareerId;
        }

        var result = await _metricsService.GetDashboardMetricsAsync(careerId);
        return Ok(result.Data);
    }

    [HttpGet("reports/releasable")]
    [Authorize(Roles = "admin,departmenthead,director")]
    public async Task<IActionResult> GetReleasableProjects([FromQuery] PaginationQuery query)
    {
        var result = await _reportService.GetReleasableProjectsAsync(query);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpPost("reports/release-letter/{projectId:long}")]
    [Authorize(Roles = "admin,departmenthead,director")]
    public async Task<IActionResult> IssueReleaseLetter(long projectId)
    {
        var result = await _reportService.IssueReleaseLetterAsync(projectId);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }
}
