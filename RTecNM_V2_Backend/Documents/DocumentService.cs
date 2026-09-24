using System.Data;
using Microsoft.EntityFrameworkCore;
using MiniExcelLibs;
using TecNM.Residency.Auth;
using TecNM.Residency.Common;
using TecNM.Residency.Common.Notifications;
using TecNM.Residency.Projects;
using TecNM.Residency.Students;

namespace TecNM.Residency.Documents;

public class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _repository;
    private readonly IProjectRepository _projectRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailQueue _emailQueue;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly AppDbContext _context;

    public DocumentService(
        IDocumentRepository repository,
        IProjectRepository projectRepository,
        IStudentRepository studentRepository,
        ICurrentUserService currentUser,
        IEmailQueue emailQueue,
        IEmailTemplateService emailTemplateService,
        AppDbContext context)
    {
        _repository = repository;
        _projectRepository = projectRepository;
        _studentRepository = studentRepository;
        _currentUser = currentUser;
        _emailQueue = emailQueue;
        _emailTemplateService = emailTemplateService;
        _context = context;
    }

    public async Task<DocumentResponseDto> UploadDocumentAsync(UploadDocumentDto dto, string uploadsRootPath)
    {
        if (dto.File == null || dto.File.Length == 0)
        {
            throw new ArgumentException("El archivo es obligatorio y no puede estar vacío.");
        }

        if (dto.File.Length > 5 * 1024 * 1024) // 5MB
        {
            throw new ArgumentException("El tamaño del archivo excede el límite máximo de 5MB.");
        }

        var extension = Path.GetExtension(dto.File.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
        if (!allowedExtensions.Contains(extension))
        {
            throw new ArgumentException("Solo se permiten archivos en formato PDF, JPG o PNG.");
        }

        if (DocumentType.IsOmitted(dto.DocumentType))
        {
            throw new ArgumentException("El anteproyecto técnico y la carta de presentación no se suben como archivos al expediente digital.");
        }

        if (!DocumentType.IsValid(dto.DocumentType))
        {
            throw new ArgumentException($"Tipo de documento no válido: '{dto.DocumentType}'.");
        }

        // Validate format upload order and deadlines
        await CheckFormatUploadOrderAsync(dto.ProjectId, dto.DocumentType);

        var project = await _projectRepository.GetByIdAsync(dto.ProjectId);
        if (project == null)
        {
            throw new InvalidOperationException($"No existe el proyecto con ID {dto.ProjectId}.");
        }

        var documentsFolder = Path.Combine(uploadsRootPath, "documents");
        if (!Directory.Exists(documentsFolder))
        {
            Directory.CreateDirectory(documentsFolder);
        }

        var uniqueFileName = $"{dto.ProjectId}_{dto.DocumentType}_{Guid.NewGuid()}{extension}";
        var relativePath = Path.Combine("documents", uniqueFileName).Replace('\\', '/');
        var fullPath = Path.Combine(documentsFolder, uniqueFileName);

        using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await dto.File.CopyToAsync(stream);
        }

        var contentType = !string.IsNullOrEmpty(dto.File.ContentType)
            ? dto.File.ContentType
            : extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                _ => "application/pdf"
            };

        var normalizedType = dto.DocumentType.ToLowerInvariant();
        if (normalizedType == DocumentType.CartaAprobacion) normalizedType = DocumentType.CartaAceptacion;

        var document = new Document
        {
            ProjectId = dto.ProjectId,
            DocumentType = normalizedType,
            FileName = dto.File.FileName,
            FilePath = relativePath,
            FileSize = dto.File.Length,
            ContentType = contentType,
            Status = DocumentStatus.Uploaded,
            UploadedAt = DateTime.UtcNow,
            IsActive = true,
            IsVisible = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        await _repository.AddAsync(document);
        await _repository.SaveChangesAsync();

        TrySendDocumentNotification(project, document.DocumentType);

        return MapToDto(document);
    }

    public async Task<Result<PaginatedResult<DocumentResponseDto>>> GetByProjectPagedAsync(long projectId, PaginationQuery query, bool includeInactive = false)
    {
        var paged = await _repository.GetPagedByProjectIdAsync(projectId, query, includeInactive);
        var dtos = paged.Items.Select(MapToDto);
        var result = PaginatedResult<DocumentResponseDto>.Create(
            dtos, paged.TotalCount, paged.PageNumber, paged.PageSize);
        return Result<PaginatedResult<DocumentResponseDto>>.Success(result);
    }

    public async Task<(byte[] FileBytes, string ContentType, string FileName)> DownloadDocumentAsync(long id, string uploadsRootPath)
    {
        var document = await _repository.GetByIdAsync(id);
        if (document == null || !document.IsActive)
        {
            throw new KeyNotFoundException($"Documento con ID {id} no encontrado.");
        }

        var relative = document.FilePath.Replace('\\', '/');
        if (relative.StartsWith("uploads/", StringComparison.Ordinal))
        {
            relative = relative["uploads/".Length..];
        }

        var fullPath = Path.Combine(uploadsRootPath, relative.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"El archivo físico no existe en la ruta: {document.FilePath}");
        }

        var fileBytes = await File.ReadAllBytesAsync(fullPath);
        return (fileBytes, document.ContentType, document.FileName);
    }

    public async Task<DocumentResponseDto?> UpdateStatusAsync(long id, UpdateDocumentStatusDto dto)
    {
        var document = await _repository.GetByIdAsync(id);
        if (document == null || !document.IsActive)
        {
            return null;
        }

        if (!DocumentStatus.IsValid(dto.Status))
        {
            throw new ArgumentException($"Estado no válido: '{dto.Status}'.");
        }

        document.Status = dto.Status.ToLowerInvariant();
        document.RejectionReason = dto.Status.Equals(DocumentStatus.Rejected, StringComparison.OrdinalIgnoreCase)
            ? dto.RejectionReason
            : null;
        document.UpdatedAt = DateTime.UtcNow;
        document.UpdatedBy = _currentUser.UserId;

        await _repository.UpdateAsync(document);
        await _repository.SaveChangesAsync();

        if (dto.Status.Equals(DocumentStatus.Approved, StringComparison.OrdinalIgnoreCase) ||
            dto.Status.Equals(DocumentStatus.Uploaded, StringComparison.OrdinalIgnoreCase))
        {
            var project = await _projectRepository.GetByIdAsync(document.ProjectId);
            if (project != null)
            {
                TrySendDocumentNotification(project, document.DocumentType);
            }
        }

        return MapToDto(document);
    }

    private void TrySendDocumentNotification(Project project, string documentType)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var student = await _studentRepository.GetByIdAsync(project.StudentId);
                if (student != null && student.User != null && !string.IsNullOrWhiteSpace(student.User.Email))
                {
                    var friendlyName = GetFriendlyDocumentTypeName(documentType);
                    var loginUrl = "http://localhost:5085/auth/login";
                    var email = _emailTemplateService.BuildLetterAvailableEmail(
                        $"{student.FirstName} {student.LastName}".Trim(),
                        friendlyName,
                        loginUrl
                    );
                    email.ToEmail = student.User.Email;
                    email.ToName = $"{student.FirstName} {student.LastName}".Trim();
                    _emailQueue.Enqueue(email);
                }
            }
            catch
            {
                // Silent catch for background notification dispatch
            }
        });
    }

    private static string GetFriendlyDocumentTypeName(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "carta_presentacion" => "Carta de Presentación",
            "carta_aceptacion" => "Carta de Aceptación",
            "carta_liberacion" => "Carta de Liberación de Residencias",
            "solicitud" => "Solicitud de Residencia Profesional",
            "anteproyecto" => "Documento de Anteproyecto",
            _ => "Documento Oficial de Residencia"
        };
    }

    public async Task<bool> SoftDeleteAsync(long id)
    {
        var document = await _repository.GetByIdAsync(id);
        if (document == null || !document.IsActive)
        {
            return false;
        }

        document.IsActive = false;
        document.DeletedAt = DateTime.UtcNow;
        document.DeletedBy = _currentUser.UserId;
        document.UpdatedAt = DateTime.UtcNow;
        document.UpdatedBy = _currentUser.UserId;
        await _repository.UpdateAsync(document);
        await _repository.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ActivateAsync(long id)
    {
        var document = await _repository.GetByIdAsync(id);
        if (document == null || document.IsActive)
        {
            return false;
        }

        document.IsActive = true;
        document.DeletedAt = null;
        document.DeletedBy = null;
        document.UpdatedAt = DateTime.UtcNow;
        document.UpdatedBy = _currentUser.UserId;
        await _repository.UpdateAsync(document);
        await _repository.SaveChangesAsync();
        return true;
    }

private static DocumentResponseDto MapToDto(Document doc)
        {
            return new DocumentResponseDto
            {
                Id = doc.Id,
                ProjectId = doc.ProjectId,
                DocumentType = doc.DocumentType,
                FileName = doc.FileName,
                FilePath = doc.FilePath,
                FileSize = doc.FileSize,
                ContentType = doc.ContentType,
                Status = doc.Status,
                RejectionReason = doc.RejectionReason,
                UploadedAt = doc.UploadedAt,
                IsActive = doc.IsActive,
                IsVisible = doc.IsVisible,
                DisplayOrder = doc.DisplayOrder,
                CreatedAt = doc.CreatedAt,
                UpdatedAt = doc.UpdatedAt,
                CreatedBy = doc.CreatedBy,
                UpdatedBy = doc.UpdatedBy,
                DeletedBy = doc.DeletedBy,
                DeletedAt = doc.DeletedAt
            };
        }

        private async Task CheckFormatUploadOrderAsync(long projectId, string documentType)
        {
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null) return;

            var studentId = project.StudentId;
            var student = await _studentRepository.GetByIdAsync(studentId);
            if (student == null) return;

            var docTypeLower = documentType.Trim().ToLowerInvariant();

            // Documentos existentes del proyecto
            var existingDocs = await _context.Documents
                .Where(d => d.ProjectId == projectId && d.IsActive)
                .OrderByDescending(d => d.Id)
                .ToListAsync();

            var f29 = existingDocs.FirstOrDefault(d => d.DocumentType.Equals(DocumentType.Formato29, StringComparison.OrdinalIgnoreCase));
            bool isF29Approved = f29 != null && string.Equals(f29.Status, DocumentStatus.Approved, StringComparison.OrdinalIgnoreCase);

            // Regla: No se puede subir Formato 29v2 ni Formato 30 si el primer Formato 29 no está aprobado por coordinación
            if (docTypeLower == DocumentType.Formato29V2 || docTypeLower == DocumentType.Formato30)
            {
                if (!isF29Approved)
                {
                    throw new InvalidOperationException("No se puede subir el Formato 29 (segunda entrega) ni el Formato 30 hasta que la Coordinación haya aprobado el primer Formato 29.");
                }
            }

            // Regla: Si la fecha límite del Formato 29 venció y no está aprobado, solo se permite subir el Formato 29
            if (student.Formato29Deadline.HasValue && DateTime.UtcNow > student.Formato29Deadline.Value)
            {
                if (!isF29Approved && docTypeLower != DocumentType.Formato29)
                {
                    throw new InvalidOperationException("La fecha límite para el Formato 29 ha vencido. Debe subir el Formato 29 requerido para continuar.");
                }
            }

            // Regla: Si la fecha límite del Formato 30 venció y faltan los formatos finales, solo se permite subir 29v2 o 30
            if (student.Formato30Deadline.HasValue && DateTime.UtcNow > student.Formato30Deadline.Value)
            {
                var f29v2 = existingDocs.FirstOrDefault(d => d.DocumentType.Equals(DocumentType.Formato29V2, StringComparison.OrdinalIgnoreCase));
                var f30 = existingDocs.FirstOrDefault(d => d.DocumentType.Equals(DocumentType.Formato30, StringComparison.OrdinalIgnoreCase));
                bool f29v2Approved = f29v2 != null && string.Equals(f29v2.Status, DocumentStatus.Approved, StringComparison.OrdinalIgnoreCase);
                bool f30Approved = f30 != null && string.Equals(f30.Status, DocumentStatus.Approved, StringComparison.OrdinalIgnoreCase);

                if ((!f29v2Approved || !f30Approved) && docTypeLower != DocumentType.Formato29V2 && docTypeLower != DocumentType.Formato30)
                {
                    throw new InvalidOperationException("La fecha límite para el Formato 29 (segunda entrega) y Formato 30 ha vencido. Debe subir los formatos requeridos para continuar.");
                }
            }
        }

        public async Task<List<PendingAcceptanceDto>> GetPendingAcceptanceLettersAsync(long? careerId = null)
    {
        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            careerId = _currentUser.CareerId;
        }
        else if (_currentUser.Role == UserRole.Coordinator)
        {
            if (careerId.HasValue && _currentUser.CareerIds.Contains(careerId.Value))
            {
                // mantener
            }
            else if (_currentUser.CareerIds.Count > 0)
            {
                careerId = _currentUser.CareerIds.FirstOrDefault();
            }
        }

        var query = _context.Projects
            .Include(p => p.Student)
            .Include(p => p.Company)
            .Where(p => p.IsActive && p.Student != null && p.Student.IsActive)
            .Where(p => p.ProjectType != "acreditacion_innovatec" && p.ProjectType != "acreditacion_hackatec")
            .Where(p => p.Status != ProjectStatus.Cancelled && p.Status != ProjectStatus.Draft);

        if (_currentUser.Role == UserRole.Advisor)
        {
            var adv = await _context.Advisors.FirstOrDefaultAsync(a => a.UserId == _currentUser.UserId && a.IsActive);
            if (adv != null)
            {
                query = query.Where(p => p.AdvisorId == adv.Id || p.Student!.AdvisorId == adv.Id);
            }
        }

        if (careerId.HasValue && careerId.Value > 0)
        {
            query = query.Where(p => p.Student!.CareerId == careerId.Value);
        }

        var acceptedProjectIds = await _context.Documents
            .Where(d => d.IsActive && (d.DocumentType == DocumentType.CartaAceptacion || d.DocumentType == DocumentType.CartaAprobacion))
            .Select(d => d.ProjectId)
            .Distinct()
            .ToListAsync();

        var pendingProjects = await query
            .Where(p => !acceptedProjectIds.Contains(p.Id))
            .OrderByDescending(p => p.CreatedAt)
            .Take(50)
            .ToListAsync();

        var careers = await _context.Careers.ToDictionaryAsync(c => c.Id, c => c.Name);

        var result = pendingProjects.Select(p => new PendingAcceptanceDto
        {
            StudentId = p.StudentId,
            StudentControlNumber = p.Student?.ControlNumber ?? string.Empty,
            StudentName = $"{p.Student?.FirstName} {p.Student?.LastName} {p.Student?.LastName2}".Trim(),
            CareerId = p.Student?.CareerId ?? 0,
            CareerName = p.Student != null && careers.TryGetValue(p.Student.CareerId, out var cName) ? cName : "Carrera",
            ProjectId = p.Id,
            ProjectTitle = p.Title,
            CompanyName = p.Company?.Name,
            ProjectStatus = p.Status.ToString(),
            HasAcceptanceLetter = false,
            StatusLabel = "Sin carta de aceptación",
            ProjectCreatedAt = p.CreatedAt
        }).ToList();

        return result;
    }

    private async Task<List<DocumentMatrixItemDto>> BuildDocumentMatrixItemsAsync(string? search, long? careerId, string? completionStatus)
    {
        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            careerId = _currentUser.CareerId;
        }
        else if (_currentUser.Role == UserRole.Coordinator)
        {
            if (careerId.HasValue && _currentUser.CareerIds.Contains(careerId.Value))
            {
                // mantener
            }
            else if (_currentUser.CareerIds.Count > 0)
            {
                careerId = _currentUser.CareerIds.FirstOrDefault();
            }
        }

        var q = _context.Projects
            .Include(p => p.Student)
            .Include(p => p.Company)
            .Where(p => p.IsActive && p.Student != null && p.Student.IsActive)
            .Where(p => p.Status != ProjectStatus.Cancelled);

        if (_currentUser.Role == UserRole.Advisor)
        {
            var adv = await _context.Advisors.FirstOrDefaultAsync(a => a.UserId == _currentUser.UserId && a.IsActive);
            if (adv != null)
            {
                q = q.Where(p => p.AdvisorId == adv.Id || p.Student!.AdvisorId == adv.Id);
            }
        }

        if (careerId.HasValue && careerId.Value > 0)
        {
            q = q.Where(p => p.Student!.CareerId == careerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            q = q.Where(p =>
                p.Title.ToLower().Contains(term) ||
                p.Student!.FirstName.ToLower().Contains(term) ||
                p.Student!.LastName.ToLower().Contains(term) ||
                (p.Student.LastName2 != null && p.Student.LastName2.ToLower().Contains(term)) ||
                p.Student.ControlNumber.ToLower().Contains(term));
        }

        q = q.OrderByDescending(p => p.CreatedAt);

        var coreTypes = new[]
        {
            DocumentType.Solicitud,
            DocumentType.CartaAceptacion,
            DocumentType.Dictamen,
            DocumentType.Libranza
        };

        var allMatchingProjects = await q.ToListAsync();
        var projectIds = allMatchingProjects.Select(p => p.Id).ToList();

        var docs = await _context.Documents
            .Where(d => d.IsActive && projectIds.Contains(d.ProjectId))
            .Where(d => d.DocumentType != DocumentType.Anteproyecto && d.DocumentType != DocumentType.CartaPresentacion)
            .ToListAsync();

        var docsByProject = docs.GroupBy(d => d.ProjectId).ToDictionary(g => g.Key, g => g.ToList());
        var careers = await _context.Careers.ToDictionaryAsync(c => c.Id, c => c.Name);

        var matrixItems = new List<DocumentMatrixItemDto>();

        foreach (var p in allMatchingProjects)
        {
            var pDocs = docsByProject.GetValueOrDefault(p.Id, new List<Document>());
            var docMap = new Dictionary<string, DocumentFileSummaryDto>();
            foreach (var d in pDocs)
            {
                var docTypeKey = d.DocumentType.ToLowerInvariant();
                if (docTypeKey == DocumentType.CartaAprobacion) docTypeKey = DocumentType.CartaAceptacion;

                docMap[docTypeKey] = new DocumentFileSummaryDto
                {
                    Id = d.Id,
                    DocumentType = d.DocumentType,
                    FileName = d.FileName,
                    Status = d.Status,
                    UploadedAt = d.UploadedAt
                };
            }

            bool isAccreditation = p.ProjectType is "acreditacion_innovatec" or "acreditacion_hackatec";
            int requiredCount = isAccreditation ? 1 : coreTypes.Length;
            int uploadedCount = isAccreditation
                ? (docMap.ContainsKey(DocumentType.ConstanciaAcreditacion.ToLowerInvariant()) ? 1 : 0)
                : coreTypes.Count(t => docMap.ContainsKey(t.ToLowerInvariant()));
            bool isCompleted = uploadedCount >= requiredCount;

            if (!string.IsNullOrWhiteSpace(completionStatus))
            {
                if (completionStatus.Equals("completed", StringComparison.OrdinalIgnoreCase) && !isCompleted)
                    continue;
                if (completionStatus.Equals("incomplete", StringComparison.OrdinalIgnoreCase) && isCompleted)
                    continue;
            }

            matrixItems.Add(new DocumentMatrixItemDto
            {
                ProjectId = p.Id,
                StudentId = p.StudentId,
                StudentControlNumber = p.Student?.ControlNumber ?? string.Empty,
                StudentName = $"{p.Student?.FirstName} {p.Student?.LastName} {p.Student?.LastName2}".Trim(),
                CareerId = p.Student?.CareerId ?? 0,
                CareerName = p.Student != null && careers.TryGetValue(p.Student.CareerId, out var cn) ? cn : "Carrera",
                ProjectTitle = p.Title,
                CompanyName = p.Company?.Name,
                ProjectStatus = p.Status.ToString(),
                Documents = docMap,
                UploadedCount = uploadedCount,
                RequiredCount = requiredCount,
                IsCompleted = isCompleted,
                IsAccreditation = isAccreditation,
                ProjectType = p.ProjectType
            });
        }

        return matrixItems;
    }

    public async Task<PaginatedResult<DocumentMatrixItemDto>> GetDocumentMatrixAsync(PaginationQuery query, long? careerId = null, string? completionStatus = null)
    {
        var matrixItems = await BuildDocumentMatrixItemsAsync(query.Search, careerId, completionStatus);

        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var totalCount = matrixItems.Count;
        var pagedItems = matrixItems
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        return PaginatedResult<DocumentMatrixItemDto>.Create(
            pagedItems,
            totalCount,
            pageNumber,
            pageSize
        );
    }

    public async Task<Result<byte[]>> ExportDocumentMatrixExcelAsync(string? search = null, long? careerId = null, string? completionStatus = null)
    {
        var items = await BuildDocumentMatrixItemsAsync(search, careerId, completionStatus);

        var table = new DataTable("Expedientes");
        table.Columns.Add("No. Control", typeof(string));
        table.Columns.Add("Estudiante", typeof(string));
        table.Columns.Add("Carrera", typeof(string));
        table.Columns.Add("Anteproyecto", typeof(string));
        table.Columns.Add("Empresa", typeof(string));
        table.Columns.Add("Solicitud (1=Entregado, 0=Faltante)", typeof(int));
        table.Columns.Add("Carta Aceptación (1=Entregado, 0=Faltante)", typeof(int));
        table.Columns.Add("Dictamen (1=Entregado, 0=Faltante)", typeof(int));
        table.Columns.Add("Liberación (1=Entregado, 0=Faltante)", typeof(int));
        table.Columns.Add("Total Entregados", typeof(int));
        table.Columns.Add("Estatus Expediente", typeof(string));
        table.Columns.Add("Avance", typeof(string));

        foreach (var item in items)
        {
            table.Rows.Add(
                item.StudentControlNumber,
                item.StudentName,
                item.CareerName,
                item.ProjectTitle,
                item.CompanyName ?? "—",
                item.IsAccreditation ? 1 : (item.Documents.ContainsKey("solicitud") ? 1 : 0),
                item.IsAccreditation ? 1 : (item.Documents.ContainsKey("carta_aceptacion") ? 1 : 0),
                item.IsAccreditation ? 1 : (item.Documents.ContainsKey("dictamen") ? 1 : 0),
                item.IsAccreditation ? 1 : (item.Documents.ContainsKey("libranza") ? 1 : 0),
                item.UploadedCount,
                item.IsCompleted ? (item.IsAccreditation ? "Acreditado (InnovaTec)" : "Completado") : "Incompleto",
                $"{item.UploadedCount}/{item.RequiredCount}"
            );
        }

        var legendTable = new DataTable("Leyenda");
        legendTable.Columns.Add("Columna / Concepto", typeof(string));
        legendTable.Columns.Add("Valor", typeof(string));
        legendTable.Columns.Add("Significado / Descripción", typeof(string));

        legendTable.Rows.Add("Solicitud, Carta Aceptación, Dictamen, Liberación", "1", "Documento entregado y registrado en sistema");
        legendTable.Rows.Add("Solicitud, Carta Aceptación, Dictamen, Liberación", "0", "Documento faltante / pendiente de entrega");
        legendTable.Rows.Add("Total Entregados", "0 a 4", "Suma numérica de documentos entregados (permite conteo/fórmulas directas)");
        legendTable.Rows.Add("Estatus Expediente", "Completado", "Expediente con 4 de 4 documentos entregados");
        legendTable.Rows.Add("Estatus Expediente", "Incompleto", "Expediente con documentos pendientes (< 4)");

        var sheets = new Dictionary<string, object>
        {
            ["Expedientes"] = table,
            ["Leyenda_Simbologia"] = legendTable
        };

        using var ms = new MemoryStream();
        MiniExcel.SaveAs(ms, sheets);
        return Result<byte[]>.Success(ms.ToArray());
    }
}
