using System.ComponentModel.DataAnnotations;

namespace POSI.Domain.DTOs.Auth;

/// <summary>
/// Solicitud de token de refresco.
/// </summary>
public record RefreshRequestDto(
    [property: Required(ErrorMessage = "El token de refresco es obligatorio.")]
    string RefreshToken
);
