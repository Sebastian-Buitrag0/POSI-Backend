using System.ComponentModel.DataAnnotations;

namespace POSI.Domain.DTOs.Auth;

/// <summary>
/// Solicitud de contraseña olvidada.
/// </summary>
public record ForgotPasswordRequestDto(
    [property: Required(ErrorMessage = "El correo es obligatorio.")]
    [property: EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    string Email
);
