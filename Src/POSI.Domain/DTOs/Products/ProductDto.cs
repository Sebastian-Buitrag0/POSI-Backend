namespace POSI.Domain.DTOs.Products;

public record ProductDto(
    Guid Id,
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

public record CreateProductDto(
    string Name,
    string? Sku,
    string? Barcode,
    decimal Price,
    decimal? Cost,
    int Stock,
    int MinStock,
    bool IsActive,
    string Unit = "unidad"
);

public record UpdateProductDto(
    string Name,
    string? Sku,
    string? Barcode,
    decimal Price,
    decimal? Cost,
    int Stock,
    int MinStock,
    bool IsActive,
    string Unit = "unidad"
);
