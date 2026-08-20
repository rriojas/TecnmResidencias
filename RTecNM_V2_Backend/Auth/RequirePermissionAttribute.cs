using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TecNM.Residency.Auth;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class RequirePermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    public string Permission { get; }

    public RequirePermissionAttribute(string permission)
    {
        Permission = permission;
    }

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (user == null || user.Identity == null || !user.Identity.IsAuthenticated)
        {
            context.Result = new ObjectResult(new { message = "Sesión requerida." })
            {
                StatusCode = 401
            };
            return Task.CompletedTask;
        }

        // Bypass SuperAdmin (IsAdmin claim)
        var isAdminClaim = user.FindFirst("isAdmin")?.Value ?? user.FindFirst("is_admin")?.Value;
        if (isAdminClaim == "true")
        {
            return Task.CompletedTask;
        }

        // Check exact match OR hierarchical parent/child match
        var hasPermission = user.HasClaim(c =>
            c.Type == "Permission" &&
            (c.Value == Permission || Permission.StartsWith(c.Value + ".") || c.Value.StartsWith(Permission + "."))
        );

        if (!hasPermission)
        {
            context.Result = new ObjectResult(new { message = $"Permiso insuficiente: {Permission}" })
            {
                StatusCode = 403
            };
        }

        return Task.CompletedTask;
    }
}
