using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TecNM.Residency.Common;

namespace TecNM.Residency.Auth;

public class AuthService : IAuthService
{
    private readonly IAuthRepository _authRepository;
    private readonly AppDbContext _context;
    private readonly JwtSettings _jwtSettings;

    public AuthService(IAuthRepository authRepository, AppDbContext context, IOptions<JwtSettings> jwtSettings)
    {
        _authRepository = authRepository;
        _context = context;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<Result<AuthTokenResponseDto>> LoginAsync(LoginRequestDto dto)
    {
        var user = await _authRepository.GetByEmailAsync(dto.Email);

        if (user is null)
            return Result<AuthTokenResponseDto>.Failure("Credenciales invalidas", 401);

        if (!user.IsActive)
            return Result<AuthTokenResponseDto>.Failure("Cuenta desactivada. Contacte a la administracion", 403);

        if (!VerifyPassword(dto.Password, user.PasswordHash))
            return Result<AuthTokenResponseDto>.Failure("Credenciales invalidas", 401);

        if (IsLegacyPasswordHash(user.PasswordHash))
        {
            var newHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            await _authRepository.UpdatePasswordHashAsync(user.Id, newHash);
        }

        if (user.Role == UserRole.Admin)
        {
            user.IsAdmin = true;
        }

        var permissions = await GetUserPermissionSlugsAsync(user);
        var token = GenerateJwtToken(user, permissions);
        var expiresIn = (int)TimeSpan.FromMinutes(_jwtSettings.ExpirationMinutes).TotalSeconds;

        string? fullName = null;
        string? controlNumber = null;

        if (user.Role == UserRole.Student)
        {
            var student = await _context.Students
                .AsNoTracking()
                .Where(s => s.UserId == user.Id)
                .Select(s => new {
                    FullName = (s.FirstName + " " + s.LastName + (string.IsNullOrWhiteSpace(s.LastName2) ? "" : " " + s.LastName2)).Trim(),
                    s.ControlNumber
                })
                .FirstOrDefaultAsync();

            if (student != null)
            {
                fullName = student.FullName;
                controlNumber = student.ControlNumber;
            }
        }
        else if (user.Role == UserRole.Advisor)
        {
            var advisor = await _context.Advisors
                .AsNoTracking()
                .Where(a => a.UserId == user.Id)
                .Select(a => a.FullName)
                .FirstOrDefaultAsync();

            fullName = advisor;
        }

        var response = new AuthTokenResponseDto
        {
            Token = token,
            ExpiresIn = expiresIn,
            User = new UserResponseDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = fullName,
                ControlNumber = controlNumber,
                Role = user.Role.ToString().ToLowerInvariant(),
                IsActive = user.IsActive,
                IsAdmin = user.IsAdmin,
                Permissions = permissions
            }
        };

        return Result<AuthTokenResponseDto>.Success(response);
    }

    public async Task<Dictionary<long, string>> GetUserDisplayNamesByIdsAsync(List<long> userIds)
    {
        return await _authRepository.GetUserDisplayNamesByIdsAsync(userIds);
    }

    private async Task<List<string>> GetUserPermissionSlugsAsync(User user)
    {
        if (user.IsAdmin || user.Role == UserRole.Admin)
        {
            return new List<string> {
                "projects", "projects.proposals", "projects.review", "projects.delete",
                "students", "students.read", "students.manage",
                "advisors", "advisors.read", "advisors.manage",
                "activities", "activities.schedule",
                "evaluations", "evaluations.advisories", "evaluations.grading",
                "documents", "documents.digital", "documents.manage",
                "admin", "admin.reports", "admin.roles"
            };
        }

        return await _context.UserRoles
            .Where(ur => ur.UserId == user.Id && ur.IsActive && ur.Role!.IsActive)
            .SelectMany(ur => ur.Role!.RolePermissions)
            .Where(rp => rp.IsActive && rp.Permission!.IsActive && rp.Permission!.Module!.IsActive)
            .Select(rp => rp.Permission!.Slug)
            .Distinct()
            .ToListAsync();
    }

    private bool VerifyPassword(string password, string hash)
    {
        if (IsLegacyPasswordHash(hash))
            return VerifyLegacyPassword(password, hash);

        return BCrypt.Net.BCrypt.Verify(password, hash);
    }

    private bool IsLegacyPasswordHash(string hash)
    {
        return hash.StartsWith("$legacy_sha256$");
    }

    private bool VerifyLegacyPassword(string password, string hash)
    {
        var parts = hash.Split('$');
        if (parts.Length < 3) return false;

        var storedHash = parts[2];
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var inputBytes = Encoding.UTF8.GetBytes(password);
        var hashBytes = sha256.ComputeHash(inputBytes);
        var computedHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return storedHash == computedHash;
    }

    private string GenerateJwtToken(User user, List<string> permissions)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claimsList = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString().ToLowerInvariant()),
            new("isAdmin", user.IsAdmin.ToString().ToLowerInvariant()),
            new("is_admin", user.IsAdmin.ToString().ToLowerInvariant())
        };

        foreach (var perm in permissions)
        {
            claimsList.Add(new Claim("Permission", perm));
        }

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claimsList,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
