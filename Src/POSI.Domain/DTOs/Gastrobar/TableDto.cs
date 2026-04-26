using System.ComponentModel.DataAnnotations;

namespace POSI.Domain.DTOs.Gastrobar;

/// <summary>
/// Mesa en el gastrobar.
/// </summary>
public record TableDto(
    Guid Id,
    string Name,
    int Capacity,
    string Status,
    bool IsActive,
    int ActiveOrderItemCount
);

/// <summary>
/// Solicitud para crear una nueva mesa.
/// </summary>
public record CreateTableDto(
    [Required(ErrorMessage = "El nombre de la mesa es obligatorio.")]
    [MaxLength(100, ErrorMessage = "El nombre no puede exceder 100 caracteres.")]
    string Name,

    [Range(1, int.MaxValue, ErrorMessage = "La capacidad debe ser al menos 1.")]
    int Capacity
);

/// <summary>
/// Solicitud para actualizar el estado de una mesa.
/// </summary>
public record UpdateTableStatusDto(
    [Required(ErrorMessage = "El estado es obligatorio.")]
    string Status
);
