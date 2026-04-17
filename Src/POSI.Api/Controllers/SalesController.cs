using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POSI.Data;
using POSI.Domain.Interfaces;

namespace POSI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/sales")]
public class SalesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantService _tenantService;

    public SalesController(AppDbContext db, ITenantService tenantService)
    {
        _db = db;
        _tenantService = tenantService;
    }

    // POST /api/sales/{id}/void
    [HttpPost("{id:guid}/void")]
    public async Task<IActionResult> VoidSale(Guid id)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null) return Unauthorized();

        var sale = await _db.Sales
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId.Value);

        if (sale is null) return NotFound(new { message = "Venta no encontrada." });
        if (sale.Status != "completed")
            return BadRequest(new { message = "Solo se pueden anular ventas completadas." });

        sale.Status = "voided";

        // Restore product stock for each item with a known product
        foreach (var item in sale.Items)
        {
            if (item.ProductId is null) continue;
            var product = await _db.Products.FindAsync(item.ProductId.Value);
            if (product is not null)
                product.Stock += item.Quantity;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }
}
