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
    /// Subir expediente o evidencia digital en PDF (máx 5MB)
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload([FromForm] UploadDocumentDto dto)
    {
        if (User.IsInRole("vinculacion"))
            return StatusCode(403, new { message = "El rol Vinculación sólo tiene acceso de lectura al expediente digital." });

        var denied = await EnsureProjectAccessAsync(dto.ProjectId);
        if (denied is not null) return denied;

        var projectResult = await _projectService.GetProjectByIdAsync(dto.ProjectId);
        if (projectResult.IsSuccess && projectResult.Data != null)
        {
            var isStaff = User.IsInRole("admin") || User.IsInRole("departmenthead") || User.IsInRole("director") || User.IsInRole("academic") || User.IsInRole("jefecarrera") || User.IsInRole("careerhead") || User.IsInRole("coordinator") || User.IsInRole("coordinadora");
            var isAccreditation = string.Equals(projectResult.Data.ProjectType, "acreditacion_innovatec", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(projectResult.Data.ProjectType, "acreditacion_hackatec", StringComparison.OrdinalIgnoreCase);

            if (projectResult.Data.IsCompleted && !isStaff && !isAccreditation)
                return StatusCode(400, new { message = "El proyecto de residencia se encuentra concluido. No se permiten nuevas cargas al expediente digital." });

            if (projectResult.Data.Status.Equals("cancelled", StringComparison.OrdinalIgnoreCase) && !isStaff)
                return StatusCode(400, new { message = "El anteproyecto se encuentra cancelado. No se permiten cargas al expediente." });

            var isPreApprovalDoc = dto.DocumentType.Equals(DocumentType.CartaAceptacion, StringComparison.OrdinalIgnoreCase)
                || dto.DocumentType.Equals(DocumentType.ConstanciaAcreditacion, StringComparison.OrdinalIgnoreCase);

            if (!projectResult.Data.CanUploadDocuments && !isStaff && !isPreApprovalDoc && !isAccreditation)
                return StatusCode(400, new { message = "El anteproyecto aún no ha sido aprobado. En esta etapa solo se requiere cargar la Carta de Aceptación / Aprobación de la empresa." });

            if (!isStaff && (dto.DocumentType.Equals(DocumentType.Formato29V2, StringComparison.OrdinalIgnoreCase) || dto.DocumentType.Equals(DocumentType.Formato30, StringComparison.OrdinalIgnoreCase)))
            {
                var docsResult = await _documentService.GetByProjectPagedAsync(dto.ProjectId, new PaginationQuery { PageNumber = 1, PageSize = 100 });
                var f29 = docsResult.Data?.Items.FirstOrDefault(d => d.DocumentType.Equals(DocumentType.Formato29, StringComparison.OrdinalIgnoreCase));
                if (f29 == null || !string.Equals(f29.Status, DocumentStatus.Approved, StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(400, new { message = "No se permite la subida del Formato 29 (segunda entrega) ni Formato 30. El primer Formato 29 debe estar previamente entregado y aprobado por la Coordinación." });
                }
            }
        }

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
    /// Listar documentos cargados por query param de proyecto
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] long? projectId, [FromQuery] PaginationQuery query, [FromQuery] bool includeInactive = false)
    {
        if (projectId.HasValue)
        {
            var denied = await EnsureProjectAccessAsync(projectId.Value);
            if (denied is not null) return denied;

            var result = await _documentService.GetByProjectPagedAsync(projectId.Value, query, includeInactive);
            if (!result.IsSuccess)
                return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

            return Ok(result.Data);
        }

        return BadRequest(new { message = "El parámetro projectId es requerido." });
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
    /// Visualizar archivo físico del documento en línea (inline stream sin forzar descarga)
    /// </summary>
    [HttpGet("{id}/view")]
    public async Task<IActionResult> ViewFile(long id)
    {
        try
        {
            var document = await _documentRepository.GetByIdAsync(id);
            if (document is null || !document.IsActive)
                return NotFound(new { message = $"Documento con ID {id} no encontrado." });

            var denied = await EnsureProjectAccessAsync(document.ProjectId);
            if (denied is not null) return denied;

            var (fileBytes, contentType, fileName) = await _documentService.DownloadDocumentAsync(id, UploadsRootPath);

            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            var resolvedContentType = ext switch
            {
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                _ => string.IsNullOrWhiteSpace(contentType) || contentType == "application/octet-stream"
                    ? "application/pdf"
                    : contentType
            };

            Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
            return File(fileBytes, resolvedContentType);
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
    [Authorize(Roles = "admin,vinculacion,departmenthead,academic,academico,advisor,jefecarrera,careerhead,coordinadora,coordinator")]
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
    [Authorize(Roles = "admin,departmenthead,academic,academico")]
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
    [Authorize(Roles = "admin,departmenthead,academic,academico")]
    public async Task<IActionResult> Activate(long id)
    {
        var success = await _documentService.ActivateAsync(id);
        if (!success)
        {
            return NotFound(new { message = $"Documento con ID {id} no encontrado o ya activo." });
        }
        return Ok(new { message = "Documento reactivado exitosamente." });
    }

    /// <summary>
    /// Lista de alumnos con proyecto activo que no han subido su carta de aceptación
    /// </summary>
    [HttpGet("pending-acceptance")]
    [Authorize(Roles = "admin,vinculacion,departmenthead,academic,academico,director,jefecarrera,careerhead,coordinadora,coordinator,advisor")]
    public async Task<IActionResult> GetPendingAcceptanceLetters([FromQuery] long? careerId = null)
    {
        var result = await _documentService.GetPendingAcceptanceLettersAsync(careerId);
        return Ok(result);
    }

    /// <summary>
    /// Matriz consolidada de expedientes digitales con filtros y paginación
    /// </summary>
    [HttpGet("matrix")]
    [Authorize(Roles = "admin,vinculacion,departmenthead,academic,academico,director,jefecarrera,careerhead,coordinadora,coordinator,advisor")]
    public async Task<IActionResult> GetDocumentMatrix([FromQuery] PaginationQuery query, [FromQuery] long? careerId = null, [FromQuery] string? completionStatus = null)
    {
        var result = await _documentService.GetDocumentMatrixAsync(query, careerId, completionStatus);
        return Ok(result);
    }

    /// <summary>
    /// Exporta la matriz completa de expedientes digitales a Excel (.xlsx) respetando filtros
    /// </summary>
    [HttpGet("matrix/export")]
    [Authorize(Roles = "admin,vinculacion,departmenthead,academic,academico,director,jefecarrera,careerhead,coordinadora,coordinator,advisor")]
    public async Task<IActionResult> ExportDocumentMatrixExcel([FromQuery] string? search = null, [FromQuery] long? careerId = null, [FromQuery] string? completionStatus = null)
    {
        var result = await _documentService.ExportDocumentMatrixExcelAsync(search, careerId, completionStatus);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, result.ErrorMessage);

        var fileName = $"expedientes_digitales_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        return File(result.Data!, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
