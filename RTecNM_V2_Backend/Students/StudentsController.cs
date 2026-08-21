using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecNM.Residency.Common;

namespace TecNM.Residency.Students;

[ApiController]
[Authorize]
[Route("api/v1/students")]
public class StudentsController : ControllerBase
{
    private readonly IStudentService _studentService;
    private readonly ICurrentUserService _currentUser;

    public StudentsController(IStudentService studentService, ICurrentUserService currentUser)
    {
        _studentService = studentService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Roles = "admin,departmenthead,director")]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQuery query, [FromQuery] string? status, [FromQuery] bool includeInactive = false)
    {
        var result = await _studentService.GetPagedAsync(query, status, includeInactive);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpGet("export")]
    [Authorize(Roles = "admin,departmenthead,director")]
    public async Task<IActionResult> ExportPdf([FromQuery] string? search, [FromQuery] string? sortBy, [FromQuery] string? sortDir, [FromQuery] bool includeInactive = false)
    {
        var result = await _studentService.ExportPdfAsync(search, sortBy, sortDir, includeInactive);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return File(result.Data!, "application/pdf", "estudiantes_tecnm.pdf");
    }

    [HttpGet("options")]
    [Authorize(Roles = "admin,departmenthead,director")]
    public async Task<IActionResult> GetOptions()
    {
        var result = await _studentService.GetOptionsAsync();
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var result = await _studentService.GetMeAsync(_currentUser.UserId);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 404, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "admin,departmenthead,director")]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _studentService.GetByIdAsync(id);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpPost]
    [Authorize(Roles = "admin,departmenthead,director")]
    public async Task<IActionResult> Create([FromBody] CreateStudentDto dto)
    {
        var result = await _studentService.CreateAsync(dto);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin,departmenthead,director")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateStudentDto dto)
    {
        var result = await _studentService.UpdateAsync(id, dto);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin,departmenthead,director")]
    public async Task<IActionResult> SoftDelete(long id)
    {
        var result = await _studentService.SoftDeleteAsync(id, _currentUser.UserId);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(new { message = "Estudiante desactivado correctamente" });
    }

    [HttpPatch("{id}/activate")]
    [Authorize(Roles = "admin,departmenthead,director")]
    public async Task<IActionResult> Activate(long id)
    {
        var result = await _studentService.ReactivateAsync(id);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(new { message = "Estudiante reactivado correctamente" });
    }
}
