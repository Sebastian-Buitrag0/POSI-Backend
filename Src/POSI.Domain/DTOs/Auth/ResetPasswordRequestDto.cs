using System.ComponentModel.DataAnnotations;

namespace POSI.Domain.DTOs.Auth;

/// <summary>
/// Solicitud de restablecimiento de contraseña.
/// </summary>
public record ResetPasswordRequestDto(
    [property: Required(ErrorMessage = "El correo es obligatorio.")]
    [property: EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    string Email,

    [property: Required(ErrorMessage = "El token es obligatorio.")]
    string Token,

    [property: Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
    [property: MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
    string NewPassword
);
