namespace TecNM.Residency.Auth;

public class AuthTokenResponseDto
{
    public string Token { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public UserResponseDto User { get; set; } = null!;
}

public class UserResponseDto
{
    public long Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? ControlNumber { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsAdmin { get; set; }
    public List<string> Permissions { get; set; } = new();
}
