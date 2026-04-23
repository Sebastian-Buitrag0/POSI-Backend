namespace POSI.Domain.DTOs.Auth;

/// <summary>
/// Datos del usuario devueltos en respuestas de autenticación.
/// </summary>
public record UserDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    string TenantId,
    DateTime CreatedAt
);
