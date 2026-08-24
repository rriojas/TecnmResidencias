using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecNM.Residency.Common;

namespace TecNM.Residency.Companies;

[ApiController]
[Authorize]
[Route("api/v1/companies")]
public class CompaniesController : ControllerBase
{
    private readonly ICompanyService _companyService;
    private readonly ICurrentUserService _currentUser;
    private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;
    private readonly Microsoft.Extensions.Logging.ILogger<CompaniesController> _logger;

    public CompaniesController(
        ICompanyService companyService,
        ICurrentUserService currentUser,
        Microsoft.AspNetCore.Hosting.IWebHostEnvironment env,
        Microsoft.Extensions.Logging.ILogger<CompaniesController> logger)
    {
        _companyService = companyService;
        _currentUser = currentUser;
        _env = env;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Roles = "admin,vinculacion,departmenthead,academic,director,student")]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = false)
    {
        var result = await _companyService.GetAllAsync(activeOnly);
        return Ok(result.Data);
    }

    [HttpGet("options")]
    [Authorize(Roles = "admin,vinculacion,departmenthead,academic,director,student")]
    public async Task<IActionResult> GetOptions()
    {
        var result = await _companyService.GetAllAsync(activeOnly: true);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        var options = result.Data?.Select(c => new { id = c.Id, name = c.Name, rfc = c.Rfc });
        return Ok(options);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "admin,vinculacion,departmenthead,academic,director,student")]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _companyService.GetByIdAsync(id);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 404, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpPost]
    [Authorize(Roles = "admin,vinculacion")]
    public async Task<IActionResult> Create([FromBody] CreateCompanyDto dto)
    {
        var result = await _companyService.CreateAsync(dto, _currentUser.UserId);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin,vinculacion")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateCompanyDto dto)
    {
        var result = await _companyService.UpdateAsync(id, dto, _currentUser.UserId);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin,vinculacion")]
    public async Task<IActionResult> SoftDelete(long id)
    {
        var result = await _companyService.SoftDeleteAsync(id, _currentUser.UserId);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(new { message = "Empresa desactivada correctamente" });
    }

    [HttpPatch("{id}/activate")]
    [Authorize(Roles = "admin,vinculacion")]
    public async Task<IActionResult> Activate(long id)
    {
        var result = await _companyService.ReactivateAsync(id);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(new { message = "Empresa reactivada correctamente" });
    }

    [HttpPost("import-excel")]
    [TecNM.Residency.Auth.RequirePermission("companies.import.excel")]
    public async Task<IActionResult> ImportExcel(IFormFile file)
    {
        var result = await _companyService.ImportExcelAsync(file, _currentUser.UserId);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpGet("import/template")]
    [Authorize(Roles = "admin,vinculacion,departmenthead")]
    public IActionResult DownloadExcelTemplate()
    {
        var filePath = Path.Combine(_env.ContentRootPath, "Templates", "Excel", "Plantilla_Empresas.xlsx");
        if (!System.IO.File.Exists(filePath))
        {
            ExcelTemplateSeeder.EnsureTemplatesExist(_env.ContentRootPath, _logger);
        }
        var bytes = System.IO.File.ReadAllBytes(filePath);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Plantilla_Empresas.xlsx");
    }
}
