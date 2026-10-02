using TecNM.Residency.Auth;
using TecNM.Residency.Careers;
using TecNM.Residency.Common;

namespace TecNM.Residency.Notifications;

public class Notification : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    
    // Lista de roles separados por coma: "student,advisor" o "all"
    public string TargetRoles { get; set; } = "all";

    // Filtros opcionales
    public long? CareerId { get; set; }
    public string? ResidencyModality { get; set; } // null = todas, "innovatec", "regular", "hackatec", etc.

    public long SenderId { get; set; }
    public string Type { get; set; } = "manual"; // "manual" | "system"

    // Destinatario específico (opcional para pruebas o avisos individuales)
    public long? TargetUserId { get; set; }

    public User? Sender { get; set; }
    public User? TargetUser { get; set; }
    public Career? Career { get; set; }
    public ICollection<UserNotificationRead> Reads { get; set; } = new List<UserNotificationRead>();
}
