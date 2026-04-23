namespace POSI.Domain.DTOs.Users;

public record CreateLocalUserDto(string FirstName, string LastName, string Cedula, string Role, string Password);
