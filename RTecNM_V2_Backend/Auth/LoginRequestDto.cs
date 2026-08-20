using System.ComponentModel.DataAnnotations;

namespace TecNM.Residency.Auth;

public class LoginRequestDto
{
    [Required(ErrorMessage = "El correo electronico es requerido")]
    [EmailAddress(ErrorMessage = "El correo electronico no es valido")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contrasena es requerida")]
    public string Password { get; set; } = string.Empty;
}
