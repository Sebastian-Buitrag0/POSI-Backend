# Spec 011-A — Sync Endpoints (Backend)

## Objetivo
Dos endpoints que reciben datos offline del Flutter y los persisten en PostgreSQL:
- `POST /api/products/sync` — upsert de productos por tenant
- `POST /api/sales/sync` — insert de ventas (idempotente por saleNumber)

Ambos requieren `[Authorize]` (JWT). El `tenantId` viene del claim JWT, no del body.

## Infraestructura existente
- `AppDbContext` con `Products`, `Sales`, `SaleItems` DbSets
- `ITenantService.GetCurrentTenantId()` — lee tenantId del JWT
- `AuthController` como referencia de patrón

---

## Task 11.0 — DTOs de Sync (`POSI.Domain/DTOs/Sync/`)

### `Src/POSI.Domain/DTOs/Sync/SyncProductDto.cs`
```csharp
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
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record SyncProductsRequestDto(List<SyncProductDto> Products);
public record SyncMappingDto(int LocalId, string RemoteId);
public record SyncProductsResponseDto(int Synced, List<SyncMappingDto> Mappings);
```

### `Src/POSI.Domain/DTOs/Sync/SyncSaleDto.cs`
```csharp
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
```

---

## Task 11.1 — SyncController (`POSI.Api/Controllers/SyncController.cs`)

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POSI.Data;
using POSI.Domain.DTOs.Sync;
using POSI.Domain.Entities;
using POSI.Domain.Interfaces;

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

    // POST /api/products/sync
    [HttpPost("api/products/sync")]
    public async Task<IActionResult> SyncProducts([FromBody] SyncProductsRequestDto request)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null) return Unauthorized();

        var mappings = new List<SyncMappingDto>();

        foreach (var dto in request.Products)
        {
            // Upsert por barcode (si existe) o por nombre
            var existing = dto.Barcode is not null
                ? await _db.Products.FirstOrDefaultAsync(p =>
                    p.TenantId == tenantId.Value && p.Barcode == dto.Barcode)
                : await _db.Products.FirstOrDefaultAsync(p =>
                    p.TenantId == tenantId.Value && p.Name == dto.Name);

            if (existing is null)
            {
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
                await _db.SaveChangesAsync();
                mappings.Add(new(dto.LocalId, product.Id.ToString()));
            }
            else
            {
                existing.Price = dto.Price;
                existing.Stock = dto.Stock;
                existing.Cost = dto.Cost;
                existing.IsActive = dto.IsActive;
                existing.UpdatedAt = dto.UpdatedAt.ToUniversalTime();
                await _db.SaveChangesAsync();
                mappings.Add(new(dto.LocalId, existing.Id.ToString()));
            }
        }

        return Ok(new SyncProductsResponseDto(mappings.Count, mappings));
    }

    // POST /api/sales/sync
    [HttpPost("api/sales/sync")]
    public async Task<IActionResult> SyncSales([FromBody] SyncSalesRequestDto request)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null) return Unauthorized();

        var mappings = new List<SyncMappingDto>();
        var skipped = 0;

        foreach (var dto in request.Sales)
        {
            // Idempotente: skip si saleNumber ya existe para este tenant
            if (await _db.Sales.AnyAsync(s =>
                    s.TenantId == tenantId.Value && s.SaleNumber == dto.SaleNumber))
            {
                skipped++;
                // Igual devolvemos el remoteId existente para que el Flutter pueda marcarlo
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
```

---

## Task 11.2 — Validación backend

```bash
cd /Users/sebastian-buitrago/Documents/Yo/POSI/POSI-Backend
dotnet build POSI.sln
```

**0 errores, 0 warnings.**

## Archivos a crear
```
Src/POSI.Domain/DTOs/Sync/
  SyncProductDto.cs   ← Task 11.0
  SyncSaleDto.cs      ← Task 11.0
Src/POSI.Api/Controllers/
  SyncController.cs   ← Task 11.1
```

## IMPORTANTE — No hacer
- NO crear servicios adicionales — lógica directamente en el controller (es simple CRUD)
- NO modificar AppDbContext ni entidades
- NO crear migraciones
- El `SyncController` usa rutas completas en los atributos `[HttpPost("api/products/sync")]`
  porque la clase no tiene `[Route]` base
