using TecNM.Residency.Common;

namespace TecNM.Residency.Auth;

public interface IAuthService
{
    Task<Result<AuthTokenResponseDto>> LoginAsync(LoginRequestDto dto);
    Task<Dictionary<long, string>> GetUserDisplayNamesByIdsAsync(List<long> userIds);
}
