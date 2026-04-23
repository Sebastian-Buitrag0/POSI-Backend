namespace POSI.Domain.DTOs.Auth;

/// <summary>
/// Respuesta devuelta después de una autenticación exitosa.
/// </summary>
public record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    UserDto User
);
