using System.ComponentModel.DataAnnotations;

namespace POSI.Domain.DTOs.Gastrobar;

/// <summary>
/// Orden de cliente en el gastrobar.
/// </summary>
public record OrderDto(
    Guid Id,
    string OrderNumber,
    Guid TableId,
    string TableName,
    string Status,
    string? WaiterName,
    DateTime OpenedAt,
    List<OrderItemDto> Items,
    decimal Total
);

/// <summary>
/// Item de línea individual dentro de una orden.
/// </summary>
public record OrderItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal Subtotal,
    string Status,
    string? Notes
);

/// <summary>
/// Solicitud para agregar items a una orden.
/// </summary>
public record AddOrderItemsDto(
    [Required(ErrorMessage = "La lista de items es obligatoria.")]
    List<NewOrderItemDto> Items
);

/// <summary>
/// Nuevo item para agregar a una orden.
/// </summary>
public record NewOrderItemDto(
    [Required]
    Guid ProductId,

    [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser al menos 1.")]
    int Quantity,

    string? Notes
);

/// <summary>
/// Solicitud para actualizar el estado de un item de orden.
/// </summary>
public record UpdateOrderItemStatusDto(
    [Required(ErrorMessage = "El estado es obligatorio.")]
    string Status
);

/// <summary>
/// Solicitud para cerrar una orden.
/// </summary>
public record CloseOrderDto(
    [Required(ErrorMessage = "El método de pago es obligatorio.")]
    string PaymentMethod,

    string? Notes
);
