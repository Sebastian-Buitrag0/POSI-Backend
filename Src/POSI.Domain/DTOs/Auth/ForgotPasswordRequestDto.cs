using System.ComponentModel.DataAnnotations;

namespace POSI.Domain.DTOs.Auth;

/// <summary>
/// Solicitud de contraseña olvidada.
/// </summary>
public record ForgotPasswordRequestDto(
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    string Email
);
