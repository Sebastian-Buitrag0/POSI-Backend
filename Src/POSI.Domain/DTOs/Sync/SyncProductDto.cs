namespace POSI.Domain.DTOs.Sync;

/// <summary>
/// Producto para sincronizar desde un dispositivo local.
/// </summary>
public record SyncProductDto(
    int LocalId,
    string Name,
    string? Sku,
    string? Barcode,
    decimal Price,
    decimal? Cost,
    int Stock,
    int MinStock,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>
/// Solicitud de sincronización de productos en lote.
/// </summary>
public record SyncProductsRequestDto(List<SyncProductDto> Products);

/// <summary>
/// Mapeo entre identificadores locales y remotos.
/// </summary>
public record SyncMappingDto(int LocalId, string RemoteId);

/// <summary>
/// Respuesta para una operación de sincronización de productos.
/// </summary>
public record SyncProductsResponseDto(int Synced, List<SyncMappingDto> Mappings);
