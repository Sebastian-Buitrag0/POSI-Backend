using System.ComponentModel.DataAnnotations;

namespace POSI.Domain.DTOs.Auth;

/// <summary>
/// Solicitud de registro de usuario.
/// </summary>
public record RegisterRequestDto(
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    string Email,

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
    string Password,

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    string FirstName,

    [Required(ErrorMessage = "El apellido es obligatorio.")]
    string LastName,

    [Required(ErrorMessage = "El nombre del negocio es obligatorio.")]
    string BusinessName
);
