using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecNM.Residency.Common;

namespace TecNM.Residency.Advisors;

[ApiController]
[Authorize]
[Route("api/v1/advisors")]
public class AdvisorsController : ControllerBase
{
    private readonly IAdvisorService _advisorService;
    private readonly ICurrentUserService _currentUser;

    public AdvisorsController(IAdvisorService advisorService, ICurrentUserService currentUser)
    {
        _advisorService = advisorService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQuery query, [FromQuery] string? status, [FromQuery] bool includeInactive = false)
    {
        var result = await _advisorService.GetPagedAsync(query, status, includeInactive);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { message = result.ErrorMessage });
    }

    [HttpGet("export")]
    [Authorize(Roles = "admin,departmenthead,director")]
    public async Task<IActionResult> ExportPdf([FromQuery] string? search, [FromQuery] string? sortBy, [FromQuery] string? sortDir, [FromQuery] bool includeInactive = false)
    {
        var result = await _advisorService.ExportPdfAsync(search, sortBy, sortDir, includeInactive);
        return result.IsSuccess
            ? File(result.Data!, "application/pdf", "asesores_tecnm.pdf")
            : BadRequest(new { message = result.ErrorMessage });
    }

    [HttpGet("options")]
    public async Task<IActionResult> GetOptions()
    {
        var result = await _advisorService.GetOptionsAsync();
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { message = result.ErrorMessage });
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var result = await _advisorService.GetMeAsync(_currentUser.UserId);
        return result.IsSuccess ? Ok(result.Data) : StatusCode(result.StatusCode ?? 404, new { message = result.ErrorMessage });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _advisorService.GetAdvisorByIdAsync(id);
        return result.IsSuccess ? Ok(result.Data) : NotFound(new { message = result.ErrorMessage });
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Create([FromBody] CreateAdvisorDto dto)
    {
        var result = await _advisorService.CreateAdvisorAsync(dto);
        return result.IsSuccess ? CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data) : BadRequest(new { message = result.ErrorMessage });
    }

    [HttpPost("assign")]
    [Authorize(Roles = "admin,departmenthead")]
    public async Task<IActionResult> Assign([FromBody] AssignAdvisorDto dto)
    {
        var result = await _advisorService.AssignAdvisorAsync(dto);
        return result.IsSuccess ? Ok(new { message = "Asesor asignado al proyecto exitosamente." }) : StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });
    }

    [HttpPut("{id:long}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateAdvisorDto dto)
    {
        var result = await _advisorService.UpdateAdvisorAsync(id, dto);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { message = result.ErrorMessage });
    }

    [HttpDelete("{id:long}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> SoftDelete(long id)
    {
        var result = await _advisorService.SoftDeleteAdvisorAsync(id, _currentUser.UserId);
        return result.IsSuccess ? NoContent() : BadRequest(new { message = result.ErrorMessage });
    }

    [HttpPatch("{id:long}/activate")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Reactivate(long id)
    {
        var result = await _advisorService.ReactivateAdvisorAsync(id);
        return result.IsSuccess ? Ok(new { message = "Asesor reactivado exitosamente." }) : BadRequest(new { message = result.ErrorMessage });
    }
}
