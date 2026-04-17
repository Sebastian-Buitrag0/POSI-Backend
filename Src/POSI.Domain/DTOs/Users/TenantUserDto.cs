namespace POSI.Domain.DTOs.Users;

public record TenantUserDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    DateTime CreatedAt
);

public record InviteUserDto(
    string Email,
    string FirstName,
    string LastName,
    string Role
);

public record UpdateRoleDto(string Role);
