using POSI.Domain.DTOs.Products;

namespace POSI.Domain.Interfaces;

/// <summary>
/// Define operaciones de gestión de productos para el tenant actual.
/// </summary>
public interface IProductService
{
    /// <summary>
    /// Obtiene todos los productos para el tenant actual.
    /// </summary>
    Task<List<ProductDto>> GetAllAsync();

    /// <summary>
    /// Crea un nuevo producto para el tenant actual.
    /// </summary>
    Task<ProductDto> CreateAsync(CreateProductDto dto);

    /// <summary>
    /// Actualiza un producto existente.
    /// </summary>
    Task<ProductDto?> UpdateAsync(Guid id, UpdateProductDto dto);

    /// <summary>
    /// Elimina un producto del tenant actual.
    /// </summary>
    Task<bool> DeleteAsync(Guid id);
}
