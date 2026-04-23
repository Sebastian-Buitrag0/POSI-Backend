using System.ComponentModel.DataAnnotations;

namespace POSI.Domain.DTOs.Auth;

/// <summary>
/// Solicitud de registro de usuario.
/// </summary>
public record RegisterRequestDto(
    [property: Required(ErrorMessage = "El correo es obligatorio.")]
    [property: EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    string Email,

    [property: Required(ErrorMessage = "La contraseña es obligatoria.")]
    [property: MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
    string Password,

    [property: Required(ErrorMessage = "El nombre es obligatorio.")]
    string FirstName,

    [property: Required(ErrorMessage = "El apellido es obligatorio.")]
    string LastName,

    [property: Required(ErrorMessage = "El nombre del negocio es obligatorio.")]
    string BusinessName
);
