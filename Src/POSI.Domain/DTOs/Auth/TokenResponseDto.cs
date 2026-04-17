namespace POSI.Domain.DTOs.Auth;

public record TokenResponseDto(string AccessToken, string RefreshToken);
