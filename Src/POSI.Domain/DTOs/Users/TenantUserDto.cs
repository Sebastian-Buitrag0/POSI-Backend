using System.ComponentModel.DataAnnotations;

namespace POSI.Domain.DTOs.Users;

/// <summary>
/// Usuario del tenant devuelto en respuestas de gestión de usuarios.
/// </summary>
public record TenantUserDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    DateTime CreatedAt
);

/// <summary>
/// Solicitud para invitar a un nuevo usuario por correo.
/// </summary>
public record InviteUserDto(
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    string Email,

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    string FirstName,

    [Required(ErrorMessage = "El apellido es obligatorio.")]
    string LastName,

    [Required(ErrorMessage = "El rol es obligatorio.")]
    string Role
);

/// <summary>
/// Solicitud para actualizar el rol de un usuario.
/// </summary>
public record UpdateRoleDto(
    [Required(ErrorMessage = "El rol es obligatorio.")]
    string Role
);
