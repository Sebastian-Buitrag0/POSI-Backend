using Microsoft.EntityFrameworkCore;
using POSI.Data;
using POSI.Domain.DTOs.Sync;
using POSI.Domain.Entities;
using POSI.Domain.Interfaces;
using POSI.Domain.Settings;

namespace POSI.Services;

/// <summary>
/// Servicio encargado de sincronizar productos y ventas desde dispositivos locales hacia el servidor central.
/// </summary>
public class SyncService : ISyncService
{
    private readonly AppDbContext _db;
    private readonly ITenantService _tenantService;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="SyncService"/>.
    /// </summary>
    /// <param name="db">Contexto de base de datos de la aplicación.</param>
    /// <param name="tenantService">Servicio para obtener el tenant actual.</param>
    /// <exception cref="ArgumentNullException">
    /// Se lanza cuando <paramref name="db"/> o <paramref name="tenantService"/> son <c>null</c>.
    /// </exception>
    public SyncService(AppDbContext db, ITenantService tenantService)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _tenantService = tenantService ?? throw new ArgumentNullException(nameof(tenantService));
    }

    /// <summary>
    /// Sincroniza una lista de productos para el tenant actual.
    /// </summary>
    /// <param name="request">DTO que contiene los productos a sincronizar.</param>
    /// <returns>Un DTO con los mapeos de identificadores locales a identificadores del servidor.</returns>
    /// <exception cref="UnauthorizedAccessException">
    /// Se lanza cuando no se puede determinar el tenant actual.
    /// </exception>
    public async Task<SyncProductsResponseDto> SyncProductsAsync(SyncProductsRequestDto request)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null)
            throw new UnauthorizedAccessException("No se pudo determinar el tenant actual.");

        var mappings = new List<SyncMappingDto>();

        foreach (var dto in request.Products)
        {
            var existing = dto.Barcode is not null
                ? await _db.Products
                    .FirstOrDefaultAsync(p => p.TenantId == tenantId.Value && p.Barcode == dto.Barcode)
                    .ConfigureAwait(false)
                : await _db.Products
                    .FirstOrDefaultAsync(p => p.TenantId == tenantId.Value && p.Name == dto.Name)
                    .ConfigureAwait(false);

            if (existing is null)
            {
                var tenant = await _db.Tenants
                    .FindAsync(tenantId.Value)
                    .ConfigureAwait(false);

                if (tenant is not null)
                {
                    var productCount = await _db.Products
                        .CountAsync(p => p.TenantId == tenantId.Value)
                        .ConfigureAwait(false);

                    if (!PlanLimits.IsWithinProductLimit(tenant.Plan, productCount))
                    {
                        continue;
                    }
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
                    CreatedAt = dto.CreatedAt.ToUniversalTime(),
                    UpdatedAt = dto.UpdatedAt.ToUniversalTime(),
                };

                _db.Products.Add(product);
                await _db.SaveChangesAsync().ConfigureAwait(false);
                mappings.Add(new SyncMappingDto(dto.LocalId, product.Id.ToString()));
            }
            else
            {
                existing.Price = dto.Price;
                existing.Stock = dto.Stock;
                existing.Cost = dto.Cost;
                existing.IsActive = dto.IsActive;
                existing.UpdatedAt = dto.UpdatedAt.ToUniversalTime();
                await _db.SaveChangesAsync().ConfigureAwait(false);
                mappings.Add(new SyncMappingDto(dto.LocalId, existing.Id.ToString()));
            }
        }

        return new SyncProductsResponseDto(mappings.Count, mappings);
    }

    /// <summary>
    /// Sincroniza una lista de ventas para el tenant actual.
    /// </summary>
    /// <param name="request">DTO que contiene las ventas a sincronizar.</param>
    /// <returns>
    /// Un DTO con la cantidad de ventas insertadas, omitidas y los mapeos de identificadores.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">
    /// Se lanza cuando no se puede determinar el tenant actual.
    /// </exception>
    public async Task<SyncSalesResponseDto> SyncSalesAsync(SyncSalesRequestDto request)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null)
            throw new UnauthorizedAccessException("No se pudo determinar el tenant actual.");

        var mappings = new List<SyncMappingDto>();
        var skipped = 0;

        foreach (var dto in request.Sales)
        {
            var alreadyExists = await _db.Sales
                .AnyAsync(s => s.TenantId == tenantId.Value && s.SaleNumber == dto.SaleNumber)
                .ConfigureAwait(false);

            if (alreadyExists)
            {
                skipped++;
                var existing = await _db.Sales
                    .FirstAsync(s => s.TenantId == tenantId.Value && s.SaleNumber == dto.SaleNumber)
                    .ConfigureAwait(false);
                mappings.Add(new SyncMappingDto(dto.LocalId, existing.Id.ToString()));
                continue;
            }

            var sale = new Sale
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value,
                SaleNumber = dto.SaleNumber,
                Subtotal = dto.Subtotal,
                Tax = dto.Tax,
                Total = dto.Total,
                PaymentMethod = dto.PaymentMethod,
                Status = dto.Status,
                Notes = dto.Notes,
                CreatedAt = dto.CreatedAt.ToUniversalTime(),
            };

            foreach (var item in dto.Items)
            {
                sale.Items.Add(new SaleItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId.Value,
                    SaleId = sale.Id,
                    ProductName = item.ProductName,
                    UnitPrice = item.UnitPrice,
                    Quantity = item.Quantity,
                    Subtotal = item.Subtotal,
                });
            }

            _db.Sales.Add(sale);
            await _db.SaveChangesAsync().ConfigureAwait(false);
            mappings.Add(new SyncMappingDto(dto.LocalId, sale.Id.ToString()));
        }

        return new SyncSalesResponseDto(mappings.Count - skipped, skipped, mappings);
    }
}
