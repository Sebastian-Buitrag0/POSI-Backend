using System.ComponentModel.DataAnnotations;

namespace POSI.Domain.DTOs.Products;

/// <summary>
/// Producto en el inventario.
/// </summary>
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
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>
/// Datos de creación de producto.
/// </summary>
public record CreateProductDto(
    [Required(ErrorMessage = "El nombre del producto es obligatorio.")]
    [MaxLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres.")]
    string Name,

    [MaxLength(100, ErrorMessage = "El SKU no puede exceder 100 caracteres.")]
    string? Sku,

    [MaxLength(100, ErrorMessage = "El código de barras no puede exceder 100 caracteres.")]
    string? Barcode,

    [Range(0, double.MaxValue, ErrorMessage = "El precio no puede ser negativo.")]
    decimal Price,

    [Range(0, double.MaxValue, ErrorMessage = "El costo no puede ser negativo.")]
    decimal? Cost,

    [Range(0, int.MaxValue, ErrorMessage = "El stock no puede ser negativo.")]
    int Stock,

    [Range(0, int.MaxValue, ErrorMessage = "El stock mínimo no puede ser negativo.")]
    int MinStock,

    bool IsActive
);

/// <summary>
/// Datos de actualización de producto.
/// </summary>
public record UpdateProductDto(
    [Required(ErrorMessage = "El nombre del producto es obligatorio.")]
    [MaxLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres.")]
    string Name,

    [MaxLength(100, ErrorMessage = "El SKU no puede exceder 100 caracteres.")]
    string? Sku,

    [MaxLength(100, ErrorMessage = "El código de barras no puede exceder 100 caracteres.")]
    string? Barcode,

    [Range(0, double.MaxValue, ErrorMessage = "El precio no puede ser negativo.")]
    decimal Price,

    [Range(0, double.MaxValue, ErrorMessage = "El costo no puede ser negativo.")]
    decimal? Cost,

    [Range(0, int.MaxValue, ErrorMessage = "El stock no puede ser negativo.")]
    int Stock,

    [Range(0, int.MaxValue, ErrorMessage = "El stock mínimo no puede ser negativo.")]
    int MinStock,

    bool IsActive
);
