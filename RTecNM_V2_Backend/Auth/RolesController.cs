using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecNM.Residency.Common;

namespace TecNM.Residency.Auth;

[ApiController]
[Route("api/v1/roles")]
[Authorize]
[RequirePermission("admin.roles")]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllRoles([FromQuery] PaginationQuery query, [FromQuery] bool includeInactive = false)
    {
        var result = await _roleService.GetPagedRolesAsync(query, includeInactive);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetRoleById(long id)
    {
        var result = await _roleService.GetRoleByIdAsync(id);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpPost]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleDto dto)
    {
        var result = await _roleService.CreateRoleAsync(dto);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return CreatedAtAction(nameof(GetRoleById), new { id = result.Data!.Id }, result.Data);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateRole(long id, [FromBody] UpdateRoleDto dto)
    {
        var result = await _roleService.UpdateRoleAsync(id, dto);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> SoftDeleteRole(long id)
    {
        var result = await _roleService.SoftDeleteRoleAsync(id);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return NoContent();
    }

    [HttpGet("modules-permissions")]
    public async Task<IActionResult> GetModulesWithPermissions()
    {
        var result = await _roleService.GetModulesWithPermissionsAsync();
        return Ok(result.Data);
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsersForManagement([FromQuery] PaginationQuery query, [FromQuery] string? roleFilter, [FromQuery] string? search, [FromQuery] bool includeInactive = false)
    {
        var result = await _roleService.GetUsersForManagementPagedAsync(query, roleFilter, search, includeInactive);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpGet("users/options")]
    public async Task<IActionResult> GetUserOptions()
    {
        var result = await _roleService.GetUserOptionsAsync();
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportRolesPdf([FromQuery] string? search, [FromQuery] string? sortBy, [FromQuery] string? sortDir, [FromQuery] bool includeInactive = false)
    {
        var result = await _roleService.ExportRolesPdfAsync(search, sortBy, sortDir, includeInactive);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return File(result.Data!, "application/pdf", "roles_tecnm.pdf");
    }

    [HttpGet("users/export")]
    public async Task<IActionResult> ExportUsersPdf([FromQuery] string? search, [FromQuery] string? roleFilter, [FromQuery] string? sortBy, [FromQuery] string? sortDir, [FromQuery] bool includeInactive = false)
    {
        var result = await _roleService.ExportUsersPdfAsync(search, roleFilter, sortBy, sortDir, includeInactive);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return File(result.Data!, "application/pdf", "usuarios_tecnm.pdf");
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserManagementDto dto)
    {
        var result = await _roleService.CreateUserAsync(dto);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpPut("users/{userId:long}")]
    public async Task<IActionResult> UpdateUser(long userId, [FromBody] UpdateUserManagementDto dto)
    {
        var result = await _roleService.UpdateUserAsync(userId, dto);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpPost("users/{userId:long}/assign")]
    public async Task<IActionResult> AssignUserRoles(long userId, [FromBody] AssignUserRolesDto dto)
    {
        var result = await _roleService.AssignUserRolesAsync(userId, dto);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(new { message = "Roles asignados correctamente" });
    }

    [HttpPatch("users/{userId:long}/toggle-admin")]
    public async Task<IActionResult> ToggleUserAdminStatus(long userId, [FromBody] ToggleAdminDto dto)
    {
        var result = await _roleService.ToggleUserAdminStatusAsync(userId, dto);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(new { message = "Estatus de SuperAdministrador actualizado correctamente" });
    }
}
