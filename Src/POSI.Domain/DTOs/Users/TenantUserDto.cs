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
    [property: Required(ErrorMessage = "El correo es obligatorio.")]
    [property: EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    string Email,

    [property: Required(ErrorMessage = "El nombre es obligatorio.")]
    string FirstName,

    [property: Required(ErrorMessage = "El apellido es obligatorio.")]
    string LastName,

    [property: Required(ErrorMessage = "El rol es obligatorio.")]
    string Role
);

/// <summary>
/// Solicitud para actualizar el rol de un usuario.
/// </summary>
public record UpdateRoleDto(
    [property: Required(ErrorMessage = "El rol es obligatorio.")]
    string Role
);
