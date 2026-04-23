using System.ComponentModel.DataAnnotations;

namespace POSI.Domain.DTOs.Auth;

/// <summary>
/// Solicitud de inicio de sesión de usuario.
/// </summary>
public record LoginRequestDto(
    [property: Required(ErrorMessage = "El identificador es obligatorio.")]
    string Identifier,

    [property: Required(ErrorMessage = "La contraseña es obligatoria.")]
    string Password
);
