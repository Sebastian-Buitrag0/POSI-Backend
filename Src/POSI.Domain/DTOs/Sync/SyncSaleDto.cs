namespace POSI.Domain.DTOs.Sync;

/// <summary>
/// Item de venta para sincronizar desde un dispositivo local.
/// </summary>
public record SyncSaleItemDto(
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal Subtotal
);

/// <summary>
/// Venta para sincronizar desde un dispositivo local.
/// </summary>
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

/// <summary>
/// Solicitud de sincronización de ventas en lote.
/// </summary>
public record SyncSalesRequestDto(List<SyncSaleDto> Sales);

/// <summary>
/// Respuesta para una operación de sincronización de ventas.
/// </summary>
public record SyncSalesResponseDto(int Synced, int Skipped, List<SyncMappingDto> Mappings);
