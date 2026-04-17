namespace POSI.Domain.DTOs.Auth;

public record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    UserDto User
);
