using POSI.Domain.DTOs.Gastrobar;

namespace POSI.Domain.Interfaces;

public interface IGastrobarService
{
    // Tables
    Task<List<TableDto>> GetTablesAsync();
    Task<TableDto> CreateTableAsync(CreateTableDto dto);
    Task<TableDto> UpdateTableStatusAsync(Guid tableId, UpdateTableStatusDto dto);
    Task DeleteTableAsync(Guid tableId);

    // Orders
    Task<OrderDto> GetOrderAsync(Guid orderId);
    Task<List<OrderDto>> GetActiveOrdersAsync();
    Task<OrderDto> OpenOrderAsync(Guid tableId);
    Task<OrderDto> AddItemsAsync(Guid orderId, AddOrderItemsDto dto);
    Task<OrderItemDto> UpdateItemStatusAsync(Guid itemId, UpdateOrderItemStatusDto dto);
    Task<Guid> CloseOrderAsync(Guid orderId, CloseOrderDto dto);
    Task CancelOrderAsync(Guid orderId);
}
