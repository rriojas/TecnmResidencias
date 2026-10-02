using System.ComponentModel.DataAnnotations;

namespace TecNM.Residency.Notifications;

public class CreateNotificationDto
{
    [Required(ErrorMessage = "El tema o título es obligatorio.")]
    [StringLength(200, ErrorMessage = "El tema no puede exceder 200 caracteres.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción del aviso es obligatoria.")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "La fecha de expiración es obligatoria.")]
    public DateTime ExpiresAt { get; set; }

    // Lista de roles: ["student", "advisor"] o ["all"]
    public List<string> TargetRoles { get; set; } = new();

    // Filtros opcionales
    public long? CareerId { get; set; }
    public string? ResidencyModality { get; set; } // null = todas, "innovatec", "regular", "hackatec", etc.

    // Destinatario específico para pruebas o envíos unitarios
    public long? TargetUserId { get; set; }
}

public class NotificationResponseDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string TargetRoles { get; set; } = string.Empty;
    public List<string> TargetRolesList { get; set; } = new();
    public long? CareerId { get; set; }
    public string? CareerName { get; set; }
    public string? ResidencyModality { get; set; }
    public long? TargetUserId { get; set; }
    public string? TargetUserName { get; set; }
    public string? TargetUserEmail { get; set; }
    public long SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string Type { get; set; } = "manual";
    public DateTime CreatedAt { get; set; }
    public bool IsExpired => ExpiresAt < DateTime.UtcNow;
    public int ReadsCount { get; set; }
}

public class NotificationUserOptionDto
{
    public long Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? ControlNumber { get; set; }
    public string? CareerName { get; set; }
}

public class PendingNotificationDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public string Type { get; set; } = "manual"; // "manual" | "system"
    public string Source { get; set; } = "Aviso Académico"; // "Aviso Académico" | "Documentación" | "Plazos"
    public string? ActionUrl { get; set; } // Enlace rápido si aplica (ej. "/documents")
    public DateTime CreatedAt { get; set; }
}

public class NotificationHistoryQuery
{
    public string? Search { get; set; }
    public string? Role { get; set; }
    public long? CareerId { get; set; }
    public bool? ActiveOnly { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
