using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecNM.Residency.Common;

namespace TecNM.Residency.Careers;

[ApiController]
[Authorize]
[Route("api/v1/careers")]
public class CareersController : ControllerBase
{
    private readonly ICareerService _careerService;

    public CareersController(ICareerService careerService)
    {
        _careerService = careerService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetPaged([FromQuery] PaginationQuery query, [FromQuery] string? status, [FromQuery] bool includeInactive = false)
    {
        var result = await _careerService.GetPagedAsync(query, status, includeInactive);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpGet("all")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
    {
        var result = await _careerService.GetAllAsync(includeInactive);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpGet("{id:long}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _careerService.GetByIdAsync(id);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Create([FromBody] CreateCareerDto dto)
    {
        var result = await _careerService.CreateAsync(dto);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    [HttpPut("{id:long}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateCareerDto dto)
    {
        var result = await _careerService.UpdateAsync(id, dto);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpPatch("{id:long}/toggle-status")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ToggleStatus(long id)
    {
        var result = await _careerService.ToggleStatusAsync(id);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }
}
