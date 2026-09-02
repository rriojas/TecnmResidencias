using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TecNM.Residency.Auth;

namespace TecNM.Residency.Common;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public long UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var claim = user?.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? user?.FindFirstValue("sub")
                     ?? user?.FindFirstValue("nameid")
                     ?? user?.FindFirstValue("id");
            return long.TryParse(claim, out var parsed) ? parsed : 0;
        }
    }

    public long? CareerId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var claim = user?.FindFirstValue("career_id")
                     ?? user?.FindFirstValue("careerId")
                     ?? user?.FindFirstValue("CareerId");
            return long.TryParse(claim, out var parsed) && parsed > 0 ? parsed : null;
        }
    }

    public string? Email
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user?.FindFirstValue(ClaimTypes.Email)
                ?? user?.FindFirstValue("email");
        }
    }

    public UserRole? Role
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var roleClaim = (user?.FindFirstValue(ClaimTypes.Role)
                          ?? user?.FindFirstValue("role")
                          ?? user?.FindFirstValue("Role") ?? "").ToLowerInvariant().Replace("_", "");
            return roleClaim switch
            {
                "student" or "estudiante" => UserRole.Student,
                "advisor" or "asesor" => UserRole.Advisor,
                "academico" or "academic" or "departmenthead" or "jefatura" => UserRole.Academic,
                "vinculacion" => UserRole.Vinculacion,
                "director" => UserRole.Director,
                "admin" or "superadmin" => UserRole.Admin,
                "jefecarrera" or "careerhead" => UserRole.CareerHead,
                _ => null
            };
        }
    }

    public bool IsAdmin =>
        (_httpContextAccessor.HttpContext?.User?.FindFirstValue("isAdmin") == "true") ||
        (_httpContextAccessor.HttpContext?.User?.FindFirstValue("is_admin") == "true");

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(UserRole role) => Role == role || (role == UserRole.Admin && IsAdmin);
}
