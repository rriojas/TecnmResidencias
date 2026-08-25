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

    public DocumentService(
        IDocumentRepository repository,
        IProjectRepository projectRepository,
        IStudentRepository studentRepository,
        ICurrentUserService currentUser,
        IEmailQueue emailQueue,
        IEmailTemplateService emailTemplateService)
    {
        _repository = repository;
        _projectRepository = projectRepository;
        _studentRepository = studentRepository;
        _currentUser = currentUser;
        _emailQueue = emailQueue;
        _emailTemplateService = emailTemplateService;
    }

    public async Task<DocumentResponseDto> UploadDocumentAsync(UploadDocumentDto dto, string uploadsRootPath)
    {
        if (dto.File == null || dto.File.Length == 0)
        {
            throw new ArgumentException("El archivo es obligatorio y no puede estar vacío.");
        }

        if (dto.File.Length > 10 * 1024 * 1024) // 10MB
        {
            throw new ArgumentException("El tamaño del archivo excede el límite máximo de 10MB.");
        }

        var extension = Path.GetExtension(dto.File.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
        if (!allowedExtensions.Contains(extension))
        {
            throw new ArgumentException("Solo se permiten archivos en formato PDF, JPG o PNG.");
        }

        if (!DocumentType.IsValid(dto.DocumentType))
        {
            throw new ArgumentException($"Tipo de documento no válido: '{dto.DocumentType}'.");
        }

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

        var document = new Document
        {
            ProjectId = dto.ProjectId,
            DocumentType = dto.DocumentType.ToLowerInvariant(),
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
}
