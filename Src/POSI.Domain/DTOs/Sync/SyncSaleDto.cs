namespace POSI.Domain.DTOs.Sync;

public record SyncSaleItemDto(
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal Subtotal
);

public record SyncSaleDto(
    int LocalId,
    string SaleNumber,
    decimal Subtotal,
    decimal Tax,
    decimal Total,
    string PaymentMethod,
    string Status,
    string? Notes,
    DateTime CreatedAt,
    List<SyncSaleItemDto> Items
);

public record SyncSalesRequestDto(List<SyncSaleDto> Sales);
public record SyncSalesResponseDto(int Synced, int Skipped, List<SyncMappingDto> Mappings);
