using System.ComponentModel.DataAnnotations;

namespace POSI.Domain.DTOs.Auth;

/// <summary>
/// Solicitud de cambio de contraseña.
/// </summary>
public record ChangePasswordRequestDto(
    [property: Required(ErrorMessage = "La contraseña actual es obligatoria.")]
    string CurrentPassword,

    [property: Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
    [property: MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
    string NewPassword
);
