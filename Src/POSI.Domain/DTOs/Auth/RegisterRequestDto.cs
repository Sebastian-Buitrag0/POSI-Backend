namespace POSI.Domain.DTOs.Auth;

public record RegisterRequestDto(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string BusinessName
);
