using System.ComponentModel.DataAnnotations;

namespace POSI.Domain.DTOs.Users;

/// <summary>
/// Solicitud para crear una cuenta de usuario local.
/// </summary>
public record CreateLocalUserDto(
    [property: Required(ErrorMessage = "El nombre es obligatorio.")]
    string FirstName,

    [property: Required(ErrorMessage = "El apellido es obligatorio.")]
    string LastName,

    [property: Required(ErrorMessage = "La cédula es obligatoria.")]
    string Cedula,

    [property: Required(ErrorMessage = "El rol es obligatorio.")]
    string Role,

    [property: Required(ErrorMessage = "La contraseña es obligatoria.")]
    [property: MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
    string Password
);
