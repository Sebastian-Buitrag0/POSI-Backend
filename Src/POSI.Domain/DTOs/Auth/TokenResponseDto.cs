namespace POSI.Domain.DTOs.Auth;

/// <summary>
/// Par de tokens devuelto después de refrescar tokens.
/// </summary>
public record TokenResponseDto(string AccessToken, string RefreshToken);
