using System.ComponentModel.DataAnnotations;

namespace POSI.Domain.DTOs.Auth;

/// <summary>
/// Solicitud de reenvío de correo de verificación.
/// </summary>
public record ResendVerificationRequestDto(
    [Required(ErrorMessage = "El identificador del usuario es obligatorio.")]
    string UserId,

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    string Email
);
