using System.ComponentModel.DataAnnotations;

namespace POSI.Domain.DTOs.Auth;

/// <summary>
/// Solicitud de inicio de sesión de usuario.
/// </summary>
public record LoginRequestDto(
    [Required(ErrorMessage = "El identificador es obligatorio.")]
    string Identifier,

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    string Password
);
