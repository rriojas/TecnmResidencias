using TecNM.Residency.Common;

namespace TecNM.Residency.Auth;

public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string? AvatarPath { get; set; }
    public bool IsAdmin { get; set; }

    public string? Phone { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? LastName2 { get; set; }
    public string? ControlNumber { get; set; }
    public long? CareerId { get; set; }

    public ICollection<UserRoleAssignment> UserRoles { get; set; } = new List<UserRoleAssignment>();
    public ICollection<UserCareer> UserCareers { get; set; } = new List<UserCareer>();
}
