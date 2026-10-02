using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TecNM.Residency.Notifications;

[ApiController]
[Route("api/v1/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _service;

    public NotificationsController(INotificationService service)
    {
        _service = service;
    }

    [HttpPost]
    [Authorize(Roles = "admin,departmenthead,academic,academico,jefecarrera,careerhead,coordinadora,coordinator")]
    public async Task<IActionResult> Create([FromBody] CreateNotificationDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _service.CreateNotificationAsync(dto);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return CreatedAtAction(nameof(GetHistory), new { id = result.Data!.Id }, result.Data);
    }

    [HttpGet("history")]
    [Authorize(Roles = "admin,departmenthead,academic,academico,jefecarrera,careerhead,coordinadora,coordinator")]
    public async Task<IActionResult> GetHistory([FromQuery] NotificationHistoryQuery query)
    {
        var result = await _service.GetHistoryAsync(query);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpGet("user-options")]
    [Authorize(Roles = "admin,departmenthead,academic,academico,jefecarrera,careerhead,coordinadora,coordinator")]
    public async Task<IActionResult> SearchUsers([FromQuery] string? search, [FromQuery] string? role)
    {
        var result = await _service.SearchUsersAsync(search, role);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpGet("pending")]
    [Authorize]
    public async Task<IActionResult> GetPending()
    {
        var result = await _service.GetPendingNotificationsAsync();
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpPatch("{id}/read")]
    [Authorize]
    public async Task<IActionResult> MarkAsRead(long id)
    {
        var result = await _service.MarkAsReadAsync(id);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(new { success = true });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin,departmenthead,academic,academico,jefecarrera,careerhead,coordinadora,coordinator")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _service.DeleteAsync(id);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(new { success = true });
    }
}
