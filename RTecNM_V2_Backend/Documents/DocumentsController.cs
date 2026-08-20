using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using TecNM.Residency.Common;
using TecNM.Residency.Projects;

namespace TecNM.Residency.Documents;

[ApiController]
[Authorize]
[Route("api/v1/documents")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly IDocumentRepository _documentRepository;
    private readonly IProjectService _projectService;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public DocumentsController(
        IDocumentService documentService,
        IDocumentRepository documentRepository,
        IProjectService projectService,
        IWebHostEnvironment environment,
        IConfiguration configuration)
    {
        _documentService = documentService;
        _documentRepository = documentRepository;
        _projectService = projectService;
        _environment = environment;
        _configuration = configuration;
    }

    private string UploadsRootPath =>
        Path.GetFullPath(Path.Combine(_environment.ContentRootPath, _configuration["Uploads:Path"] ?? "uploads"));

    private async Task<IActionResult?> EnsureProjectAccessAsync(long projectId)
    {
        var access = await _projectService.CanAccessProjectAsync(projectId);
        return access.IsSuccess ? null : StatusCode(access.StatusCode ?? 403, new { message = access.ErrorMessage });
    }

    /// <summary>
    /// Subir expediente o evidencia digital en PDF (máx 10MB)
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload([FromForm] UploadDocumentDto dto)
    {
        var denied = await EnsureProjectAccessAsync(dto.ProjectId);
        if (denied is not null) return denied;

        try
        {
            var result = await _documentService.UploadDocumentAsync(dto, UploadsRootPath);
            return CreatedAtAction(nameof(GetByProject), new { projectId = result.ProjectId }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Listar documentos cargados por ID de proyecto
    /// </summary>
    [HttpGet("project/{projectId}")]
    public async Task<IActionResult> GetByProject(long projectId, [FromQuery] PaginationQuery query, [FromQuery] bool includeInactive = false)
    {
        var denied = await EnsureProjectAccessAsync(projectId);
        if (denied is not null) return denied;

        var result = await _documentService.GetByProjectPagedAsync(projectId, query, includeInactive);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    /// <summary>
    /// Descargar archivo físico del documento
    /// </summary>
    [HttpGet("{id}/download")]
    public async Task<IActionResult> Download(long id)
    {
        try
        {
            var document = await _documentRepository.GetByIdAsync(id);
            if (document is null || !document.IsActive)
                return NotFound(new { message = $"Documento con ID {id} no encontrado." });

            var denied = await EnsureProjectAccessAsync(document.ProjectId);
            if (denied is not null) return denied;

            var (fileBytes, contentType, fileName) = await _documentService.DownloadDocumentAsync(id, UploadsRootPath);
            return File(fileBytes, contentType, fileName);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Actualizar estado de aprobación del documento
    /// </summary>
    [HttpPatch("{id}/status")]
    [Authorize(Roles = "admin,departmenthead,advisor")]
    public async Task<ActionResult<DocumentResponseDto>> UpdateStatus(long id, [FromBody] UpdateDocumentStatusDto dto)
    {
        try
        {
            var result = await _documentService.UpdateStatusAsync(id, dto);
            if (result == null)
            {
                return NotFound(new { message = $"Documento con ID {id} no encontrado." });
            }
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Desactivación lógica (Soft Delete)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "admin,departmenthead")]
    public async Task<IActionResult> SoftDelete(long id)
    {
        var success = await _documentService.SoftDeleteAsync(id);
        if (!success)
        {
            return NotFound(new { message = $"Documento con ID {id} no encontrado o ya inactivo." });
        }
        return NoContent();
    }

    /// <summary>
    /// Reactivación de documento desactivado
    /// </summary>
    [HttpPatch("{id}/activate")]
    [Authorize(Roles = "admin,departmenthead")]
    public async Task<IActionResult> Activate(long id)
    {
        var success = await _documentService.ActivateAsync(id);
        if (!success)
        {
            return NotFound(new { message = $"Documento con ID {id} no encontrado o ya activo." });
        }
        return Ok(new { message = "Documento reactivado exitosamente." });
    }
}
