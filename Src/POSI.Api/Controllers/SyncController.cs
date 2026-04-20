using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POSI.Data;
using POSI.Domain.DTOs.Sync;
using POSI.Domain.Entities;
using POSI.Domain.Interfaces;
using POSI.Domain.Settings;

namespace POSI.Api.Controllers;

[ApiController]
[Authorize]
public class SyncController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantService _tenantService;

    public SyncController(AppDbContext db, ITenantService tenantService)
    {
        _db = db;
        _tenantService = tenantService;
    }

    [HttpPost("api/products/sync")]
    public async Task<IActionResult> SyncProducts([FromBody] SyncProductsRequestDto request)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null) return Unauthorized();

        var mappings = new List<SyncMappingDto>();

        foreach (var dto in request.Products)
        {
            var existing = dto.Barcode is not null
                ? await _db.Products.FirstOrDefaultAsync(p =>
                    p.TenantId == tenantId.Value && p.Barcode == dto.Barcode)
                : await _db.Products.FirstOrDefaultAsync(p =>
                    p.TenantId == tenantId.Value && p.Name == dto.Name);

            if (existing is null)
            {
                var tenant = await _db.Tenants.FindAsync(tenantId.Value);
                if (tenant is not null)
                {
                    var productCount = await _db.Products.CountAsync(p => p.TenantId == tenantId.Value);
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
                    Unit = dto.Unit,
                    CreatedAt = dto.CreatedAt.ToUniversalTime(),
                    UpdatedAt = dto.UpdatedAt.ToUniversalTime(),
                };
                _db.Products.Add(product);
                await _db.SaveChangesAsync();
                mappings.Add(new(dto.LocalId, product.Id.ToString()));
            }
            else
            {
                existing.Price = dto.Price;
                existing.Stock = dto.Stock;
                existing.Cost = dto.Cost;
                existing.IsActive = dto.IsActive;
                existing.Unit = dto.Unit;
                existing.UpdatedAt = dto.UpdatedAt.ToUniversalTime();
                await _db.SaveChangesAsync();
                mappings.Add(new(dto.LocalId, existing.Id.ToString()));
            }
        }

        return Ok(new SyncProductsResponseDto(mappings.Count, mappings));
    }

    [HttpPost("api/sales/sync")]
    public async Task<IActionResult> SyncSales([FromBody] SyncSalesRequestDto request)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null) return Unauthorized();

        var mappings = new List<SyncMappingDto>();
        var skipped = 0;

        foreach (var dto in request.Sales)
        {
            if (await _db.Sales.AnyAsync(s =>
                    s.TenantId == tenantId.Value && s.SaleNumber == dto.SaleNumber))
            {
                skipped++;
                var existing = await _db.Sales.FirstAsync(s =>
                    s.TenantId == tenantId.Value && s.SaleNumber == dto.SaleNumber);
                mappings.Add(new(dto.LocalId, existing.Id.ToString()));
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
            await _db.SaveChangesAsync();
            mappings.Add(new(dto.LocalId, sale.Id.ToString()));
        }

        return Ok(new SyncSalesResponseDto(mappings.Count - skipped, skipped, mappings));
    }
}
