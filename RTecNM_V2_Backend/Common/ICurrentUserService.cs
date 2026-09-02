using TecNM.Residency.Auth;

namespace TecNM.Residency.Common;

public interface ICurrentUserService
{
    long UserId { get; }
    long? CareerId { get; }
    string? Email { get; }
    UserRole? Role { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(UserRole role);
}
