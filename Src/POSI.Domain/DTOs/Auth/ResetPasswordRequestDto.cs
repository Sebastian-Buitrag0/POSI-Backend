using System.ComponentModel.DataAnnotations;

namespace POSI.Domain.DTOs.Auth;

/// <summary>
/// Solicitud de restablecimiento de contraseña.
/// </summary>
public record ResetPasswordRequestDto(
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    string Email,

    [Required(ErrorMessage = "El token es obligatorio.")]
    string Token,

    [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
    [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
    string NewPassword
);
