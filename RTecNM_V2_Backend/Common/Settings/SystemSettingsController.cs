using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TecNM.Residency.Auth;

namespace TecNM.Residency.Common.Settings;

[ApiController]
[Route("api/v1/system/settings")]
[Authorize(Roles = "admin")]
public class SystemSettingsController : ControllerBase
{
    private readonly ISystemSettingService _settingService;
    private readonly ICurrentUserService _currentUser;

    public SystemSettingsController(ISystemSettingService settingService, ICurrentUserService currentUser)
    {
        _settingService = settingService;
        _currentUser = currentUser;
    }

    [HttpGet("smtp")]
    public async Task<IActionResult> GetSmtpConfig()
    {
        var config = await _settingService.GetSmtpConfigAsync();
        return Ok(config);
    }

    [HttpPut("smtp")]
    public async Task<IActionResult> UpdateSmtpConfig([FromBody] SmtpConfigDto dto)
    {
        var result = await _settingService.UpdateSmtpConfigAsync(dto, _currentUser.UserId);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(new { message = "Configuración SMTP actualizada exitosamente." });
    }

    public class TestSmtpRequestDto
    {
        public string RecipientEmail { get; set; } = string.Empty;
        public SmtpConfigDto? CustomConfig { get; set; }
    }

    [HttpPost("smtp/test")]
    public async Task<IActionResult> TestSmtpConnection([FromBody] TestSmtpRequestDto dto)
    {
        var result = await _settingService.TestSmtpConnectionAsync(dto.RecipientEmail, dto.CustomConfig);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(new { message = $"Correo de prueba enviado con éxito a '{dto.RecipientEmail}'." });
    }

    [HttpGet("template/presentation-letter")]
    public async Task<IActionResult> GetPresentationLetterTemplate()
    {
        var html = await _settingService.GetPresentationLetterTemplateAsync();
        return Ok(new { templateHtml = html });
    }

    public class UpdateTemplateDto
    {
        public string TemplateHtml { get; set; } = string.Empty;
    }

    [HttpPut("template/presentation-letter")]
    public async Task<IActionResult> UpdatePresentationLetterTemplate([FromBody] UpdateTemplateDto dto)
    {
        var result = await _settingService.UpdatePresentationLetterTemplateAsync(dto.TemplateHtml, _currentUser.UserId);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(new { message = "Plantilla HTML actualizada correctamente." });
    }

    [HttpPost("template/presentation-letter/upload")]
    public async Task<IActionResult> UploadPresentationLetterTemplate(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Debe proporcionar un archivo HTML válido." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".html" && ext != ".htm")
            return BadRequest(new { message = "El archivo debe tener extensión .html o .htm." });

        using var reader = new StreamReader(file.OpenReadStream());
        var htmlContent = await reader.ReadToEndAsync();

        var result = await _settingService.UpdatePresentationLetterTemplateAsync(htmlContent, _currentUser.UserId);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(new { message = "Archivo de plantilla cargado y aplicado exitosamente." });
    }

    [HttpPost("template/presentation-letter/upload-word")]
    public async Task<IActionResult> UploadPresentationLetterWordTemplate(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Debe seleccionar un archivo Word (.docx) válido." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".docx" && ext != ".doc")
            return BadRequest(new { message = "El archivo debe ser un documento Word (.docx)." });

        using var stream = file.OpenReadStream();
        var result = await _settingService.UploadWordTemplateAsync(stream, _currentUser.UserId);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(new { message = "Plantilla Word (.docx) procesada y desglosada correctamente a HTML.", templateHtml = result.Data });
    }

    [HttpPost("template/presentation-letter/reset")]
    public async Task<IActionResult> ResetPresentationLetterTemplate()
    {
        var result = await _settingService.ResetPresentationLetterTemplateAsync(_currentUser.UserId);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(new { message = "Plantilla restablecida al formato institucional por defecto." });
    }
}
