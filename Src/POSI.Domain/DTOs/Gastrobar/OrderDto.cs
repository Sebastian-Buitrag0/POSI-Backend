namespace POSI.Domain.DTOs.Gastrobar;

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

public record AddOrderItemsDto(List<NewOrderItemDto> Items);
public record NewOrderItemDto(Guid ProductId, int Quantity, string? Notes);
public record UpdateOrderItemStatusDto(string Status);
public record CloseOrderDto(string PaymentMethod, string? Notes);
