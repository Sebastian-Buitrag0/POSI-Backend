using Microsoft.EntityFrameworkCore;
using POSI.Data;
using POSI.Domain.DTOs.Products;
using POSI.Domain.Entities;
using POSI.Domain.Interfaces;
using POSI.Domain.Settings;

namespace POSI.Services;

/// <summary>
/// Servicio para la gestión de productos dentro de un tenant.
/// </summary>
public class ProductService : IProductService
{
    private readonly AppDbContext _db;
    private readonly ITenantService _tenantService;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="ProductService"/>.
    /// </summary>
    /// <param name="db">Contexto de base de datos de la aplicación.</param>
    /// <param name="tenantService">Servicio para obtener el tenant actual.</param>
    /// <exception cref="ArgumentNullException">Se lanza cuando <paramref name="db"/> o <paramref name="tenantService"/> son null.</exception>
    public ProductService(AppDbContext db, ITenantService tenantService)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _tenantService = tenantService ?? throw new ArgumentNullException(nameof(tenantService));
    }

    /// <summary>
    /// Obtiene todos los productos del tenant actual.
    /// </summary>
    /// <returns>Lista de productos.</returns>
    /// <exception cref="UnauthorizedAccessException">Se lanza cuando no se puede determinar el tenant actual.</exception>
    public async Task<List<ProductDto>> GetAllAsync()
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null)
            throw new UnauthorizedAccessException();

        var products = await _db.Products
            .Where(p => p.TenantId == tenantId.Value)
            .OrderBy(p => p.Name)
            .Select(p => new ProductDto(
                p.Id, p.Name, p.Sku, p.Barcode,
                p.Price, p.Cost, p.Stock, p.MinStock,
                p.IsActive, p.CreatedAt, p.UpdatedAt))
            .ToListAsync()
            .ConfigureAwait(false);

        return products;
    }

    /// <summary>
    /// Crea un nuevo producto en el tenant actual.
    /// </summary>
    /// <param name="dto">Datos del producto a crear.</param>
    /// <returns>El producto creado.</returns>
    /// <exception cref="UnauthorizedAccessException">Se lanza cuando no se puede determinar el tenant actual.</exception>
    /// <exception cref="InvalidOperationException">Se lanza cuando se alcanza el límite de productos del plan.</exception>
    public async Task<ProductDto> CreateAsync(CreateProductDto dto)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null)
            throw new UnauthorizedAccessException();

        var tenant = await _db.Tenants.FindAsync(tenantId.Value).ConfigureAwait(false);
        if (tenant is not null)
        {
            var productCount = await _db.Products
                .CountAsync(p => p.TenantId == tenantId.Value)
                .ConfigureAwait(false);

            if (!PlanLimits.IsWithinProductLimit(tenant.Plan, productCount))
                throw new InvalidOperationException("Has alcanzado el límite de productos de tu plan. Actualiza a Pro para continuar.");
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            Name = dto.Name,
            Sku = dto.Sku,
            Barcode = dto.Barcode,
            Price = dto.Price,
            Cost = dto.Cost,
            Stock = dto.Stock,
            MinStock = dto.MinStock,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync().ConfigureAwait(false);

        return new ProductDto(
            product.Id, product.Name, product.Sku, product.Barcode,
            product.Price, product.Cost, product.Stock, product.MinStock,
            product.IsActive, product.CreatedAt, product.UpdatedAt);
    }

    /// <summary>
    /// Actualiza un producto existente en el tenant actual.
    /// </summary>
    /// <param name="id">Identificador del producto.</param>
    /// <param name="dto">Datos actualizados del producto.</param>
    /// <returns>El producto actualizado, o null si no se encontró.</returns>
    /// <exception cref="UnauthorizedAccessException">Se lanza cuando no se puede determinar el tenant actual.</exception>
    public async Task<ProductDto?> UpdateAsync(Guid id, UpdateProductDto dto)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null)
            throw new UnauthorizedAccessException();

        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId.Value)
            .ConfigureAwait(false);

        if (product is null)
            return null;

        product.Name = dto.Name;
        product.Sku = dto.Sku;
        product.Barcode = dto.Barcode;
        product.Price = dto.Price;
        product.Cost = dto.Cost;
        product.Stock = dto.Stock;
        product.MinStock = dto.MinStock;
        product.IsActive = dto.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync().ConfigureAwait(false);

        return new ProductDto(
            product.Id, product.Name, product.Sku, product.Barcode,
            product.Price, product.Cost, product.Stock, product.MinStock,
            product.IsActive, product.CreatedAt, product.UpdatedAt);
    }

    /// <summary>
    /// Elimina un producto del tenant actual.
    /// </summary>
    /// <param name="id">Identificador del producto.</param>
    /// <returns>true si el producto fue eliminado; false si no se encontró.</returns>
    /// <exception cref="UnauthorizedAccessException">Se lanza cuando no se puede determinar el tenant actual.</exception>
    public async Task<bool> DeleteAsync(Guid id)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null)
            throw new UnauthorizedAccessException();

        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId.Value)
            .ConfigureAwait(false);

        if (product is null)
            return false;

        _db.Products.Remove(product);
        await _db.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }
}
