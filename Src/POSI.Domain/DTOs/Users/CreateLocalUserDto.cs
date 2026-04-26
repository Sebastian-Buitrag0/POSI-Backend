using System.ComponentModel.DataAnnotations;

namespace POSI.Domain.DTOs.Users;

/// <summary>
/// Solicitud para crear una cuenta de usuario local.
/// </summary>
public record CreateLocalUserDto(
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    string FirstName,

    [Required(ErrorMessage = "El apellido es obligatorio.")]
    string LastName,

    [Required(ErrorMessage = "La cédula es obligatoria.")]
    string Cedula,

    [Required(ErrorMessage = "El rol es obligatorio.")]
    string Role,

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
    string Password
);
