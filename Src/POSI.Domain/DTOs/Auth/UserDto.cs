namespace POSI.Domain.DTOs.Auth;

public record UserDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    string TenantId,
    DateTime CreatedAt
);
