# Spec 012 — Products CRUD Endpoints (Backend)

## Objetivo
Endpoints REST para gestión de productos por tenant:
- `GET /api/products` — lista todos los productos del tenant
- `POST /api/products` — crear producto
- `PUT /api/products/{id}` — actualizar producto
- `DELETE /api/products/{id}` — eliminar producto

Todos requieren `[Authorize]`. El `tenantId` viene del claim JWT via `ITenantService`.

## Infraestructura existente
- `AppDbContext` con `Products` DbSet y global query filter por `TenantId`
- `ITenantService.GetCurrentTenantId()` — lee tenantId del JWT
- `Product` entity: `Id(Guid), TenantId, Name, Sku, Barcode, Price, Cost, Stock, MinStock, IsActive, CreatedAt, UpdatedAt`
- `SyncController` como referencia de patrón (usa `_db` y `_tenantService` directo)

---

## Task 12.0 — DTOs (`POSI.Domain/DTOs/Products/`)

### `Src/POSI.Domain/DTOs/Products/ProductDto.cs`
```csharp
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
    bool IsActive
);

public record UpdateProductDto(
    string Name,
    string? Sku,
    string? Barcode,
    decimal Price,
    decimal? Cost,
    int Stock,
    int MinStock,
    bool IsActive
);
```

---

## Task 12.1 — ProductsController (`POSI.Api/Controllers/ProductsController.cs`)

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POSI.Data;
using POSI.Domain.DTOs.Products;
using POSI.Domain.Entities;
using POSI.Domain.Interfaces;

namespace POSI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantService _tenantService;

    public ProductsController(AppDbContext db, ITenantService tenantService)
    {
        _db = db;
        _tenantService = tenantService;
    }

    // GET /api/products
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null) return Unauthorized();

        var products = await _db.Products
            .Where(p => p.TenantId == tenantId.Value)
            .OrderBy(p => p.Name)
            .Select(p => new ProductDto(
                p.Id, p.Name, p.Sku, p.Barcode,
                p.Price, p.Cost, p.Stock, p.MinStock,
                p.IsActive, p.CreatedAt, p.UpdatedAt))
            .ToListAsync();

        return Ok(products);
    }

    // POST /api/products
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductDto dto)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null) return Unauthorized();

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
        await _db.SaveChangesAsync();

        return StatusCode(201, new ProductDto(
            product.Id, product.Name, product.Sku, product.Barcode,
            product.Price, product.Cost, product.Stock, product.MinStock,
            product.IsActive, product.CreatedAt, product.UpdatedAt));
    }

    // PUT /api/products/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductDto dto)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null) return Unauthorized();

        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId.Value);

        if (product is null) return NotFound();

        product.Name = dto.Name;
        product.Sku = dto.Sku;
        product.Barcode = dto.Barcode;
        product.Price = dto.Price;
        product.Cost = dto.Cost;
        product.Stock = dto.Stock;
        product.MinStock = dto.MinStock;
        product.IsActive = dto.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new ProductDto(
            product.Id, product.Name, product.Sku, product.Barcode,
            product.Price, product.Cost, product.Stock, product.MinStock,
            product.IsActive, product.CreatedAt, product.UpdatedAt));
    }

    // DELETE /api/products/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null) return Unauthorized();

        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId.Value);

        if (product is null) return NotFound();

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
```

---

## Task 12.2 — Validación backend

```bash
cd /Users/sebastian-buitrago/Documents/Yo/POSI/POSI-Backend
dotnet build POSI.sln
```

**0 errores, 0 warnings.**

---

## Archivos a crear
```
Src/POSI.Domain/DTOs/Products/ProductDto.cs   ← Task 12.0
Src/POSI.Api/Controllers/ProductsController.cs ← Task 12.1
```

## IMPORTANTE — No hacer
- NO crear servicios adicionales — lógica directamente en el controller
- NO modificar AppDbContext ni entidades
- NO crear migraciones
- NO modificar SyncController
- El global query filter de EF ya filtra por tenantId; igualmente verificamos explícitamente en cada query por seguridad
