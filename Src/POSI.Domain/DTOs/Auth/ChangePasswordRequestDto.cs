using System.ComponentModel.DataAnnotations;

namespace POSI.Domain.DTOs.Auth;

/// <summary>
/// Solicitud de cambio de contraseña.
/// </summary>
public record ChangePasswordRequestDto(
    [Required(ErrorMessage = "La contraseña actual es obligatoria.")]
    string CurrentPassword,

    [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
    [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
    string NewPassword
);
