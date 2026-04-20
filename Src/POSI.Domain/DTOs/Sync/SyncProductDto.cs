namespace POSI.Domain.DTOs.Sync;

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
    string Unit,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record SyncProductsRequestDto(List<SyncProductDto> Products);
public record SyncMappingDto(int LocalId, string RemoteId);
public record SyncProductsResponseDto(int Synced, List<SyncMappingDto> Mappings);
