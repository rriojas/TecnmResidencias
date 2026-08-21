using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecNM.Residency.Common;

namespace TecNM.Residency.Admin;

[ApiController]
[Authorize(Roles = "admin,departmenthead,director")]
[Route("api/v1/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IDashboardMetricsService _metricsService;
    private readonly IReportGeneratorService _reportService;

    public AdminController(
        IDashboardMetricsService metricsService,
        IReportGeneratorService reportService)
    {
        _metricsService = metricsService;
        _reportService = reportService;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var result = await _metricsService.GetDashboardMetricsAsync();
        return Ok(result.Data);
    }

    [HttpGet("reports/releasable")]
    public async Task<IActionResult> GetReleasableProjects([FromQuery] PaginationQuery query)
    {
        var result = await _reportService.GetReleasableProjectsAsync(query);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpPost("reports/release-letter/{projectId:long}")]
    public async Task<IActionResult> IssueReleaseLetter(long projectId)
    {
        var result = await _reportService.IssueReleaseLetterAsync(projectId);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }
}
