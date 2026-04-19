using POSI.Domain.DTOs.Auth;

namespace POSI.Domain.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request);
    Task<TokenResponseDto> RefreshAsync(string refreshToken);
    Task LogoutAsync(string refreshToken);
    Task<UserDto> GetProfileAsync(string userId);
    Task<string> GetEmailVerificationTokenAsync(string userId);
    Task<string> GetEmailVerificationTokenByEmailAsync(string email);
    Task VerifyEmailAsync(string email, string encodedToken);
    Task<string> GetPasswordResetTokenAsync(string email);
    Task ResetPasswordAsync(string email, string encodedToken, string newPassword);
    Task ChangePasswordAsync(string userId, string currentPassword, string newPassword);
}
