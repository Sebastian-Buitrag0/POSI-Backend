using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POSI.Data;
using POSI.Domain.DTOs.Products;
using POSI.Domain.Entities;
using POSI.Domain.Interfaces;
using POSI.Domain.Settings;

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

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Create([FromBody] CreateProductDto dto)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null) return Unauthorized();

        var tenant = await _db.Tenants.FindAsync(tenantId.Value);
        if (tenant is not null)
        {
            var productCount = await _db.Products.CountAsync(p => p.TenantId == tenantId.Value);
            if (!PlanLimits.IsWithinProductLimit(tenant.Plan, productCount))
                return StatusCode(402, new { message = "Has alcanzado el límite de productos de tu plan. Actualiza a Pro para continuar." });
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
        await _db.SaveChangesAsync();

        return StatusCode(201, new ProductDto(
            product.Id, product.Name, product.Sku, product.Barcode,
            product.Price, product.Cost, product.Stock, product.MinStock,
            product.IsActive, product.CreatedAt, product.UpdatedAt));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
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

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
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
