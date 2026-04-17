using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POSI.Data;
using POSI.Domain.DTOs.Stats;
using POSI.Domain.Entities;
using POSI.Domain.Interfaces;

namespace POSI.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/stats")]
public class StatsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantService _tenantService;

    public StatsController(AppDbContext db, ITenantService tenantService)
    {
        _db = db;
        _tenantService = tenantService;
    }

    // GET /api/stats?period=today|week|month
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string period = "week")
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null) return Unauthorized();

        var now = DateTime.UtcNow;
        // Todas las fechas deben ser Kind=Utc para PostgreSQL (timestamptz)
        var today = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var prevMonthStart = monthStart.AddMonths(-1);

        var (start, prevStart, prevEnd) = period switch
        {
            "today" => (today, today.AddDays(-1), today),
            "month" => (monthStart, prevMonthStart, monthStart),
            _ => (today.AddDays(-6), today.AddDays(-13), today.AddDays(-6)), // week
        };

        var sales = await _db.Sales
            .Where(s => s.TenantId == tenantId.Value && s.Status == "completed" && s.CreatedAt >= start)
            .ToListAsync();

        var prevSales = await _db.Sales
            .Where(s => s.TenantId == tenantId.Value && s.Status == "completed"
                        && s.CreatedAt >= prevStart && s.CreatedAt < prevEnd)
            .ToListAsync();

        // KPIs
        var totalRevenue = sales.Sum(s => s.Total);
        var totalCount = sales.Count;
        var avgTicket = totalCount > 0 ? totalRevenue / totalCount : 0;

        var prevRevenue = prevSales.Sum(s => s.Total);
        var prevCount = prevSales.Count;

        double revenueChange = prevRevenue > 0
            ? (double)((totalRevenue - prevRevenue) / prevRevenue * 100)
            : totalRevenue > 0 ? 100 : 0;

        double countChange = prevCount > 0
            ? (double)(totalCount - prevCount) / prevCount * 100
            : totalCount > 0 ? 100 : 0;

        // Ventas por día (últimos 7 días siempre para la gráfica)
        var chartStart = today.AddDays(-6);
        var chartSales = await _db.Sales
            .Where(s => s.TenantId == tenantId.Value && s.Status == "completed" && s.CreatedAt >= chartStart)
            .ToListAsync();

        var salesByDay = Enumerable.Range(0, 7)
            .Select(i =>
            {
                var day = chartStart.AddDays(i);
                var daySales = chartSales.Where(s => s.CreatedAt.Date == day).ToList();
                return new DailySalesDto(
                    day.ToString("dd/MM"),
                    daySales.Sum(s => s.Total),
                    daySales.Count);
            })
            .ToList();

        // Por método de pago
        var byMethod = sales
            .GroupBy(s => s.PaymentMethod)
            .Select(g => new PaymentMethodDto(g.Key, g.Sum(s => s.Total), g.Count()))
            .OrderByDescending(x => x.Revenue)
            .ToList();

        // Top productos
        var saleIds = sales.Select(s => s.Id).ToList();
        List<SaleItem> items = [];
        if (saleIds.Count > 0)
        {
            items = await _db.SaleItems
                .Where(si => saleIds.Contains(si.SaleId))
                .ToListAsync();
        }

        var topProducts = items
            .GroupBy(si => si.ProductName)
            .Select(g => new TopProductDto(g.Key, g.Sum(si => si.Quantity), g.Sum(si => si.Subtotal)))
            .OrderByDescending(x => x.Revenue)
            .Take(5)
            .ToList();

        // Stock bajo — materializar primero, luego mapear (EF Core no puede
        // construir records posicionales ni llamar .ToString() en SQL)
        var lowStockRaw = await _db.Products
            .Where(p => p.TenantId == tenantId.Value && p.IsActive && p.Stock <= p.MinStock)
            .OrderBy(p => p.Stock)
            .Take(10)
            .ToListAsync();

        var lowStock = lowStockRaw
            .Select(p => new LowStockDto(p.Id.ToString(), p.Name, p.Stock, p.MinStock))
            .ToList();

        return Ok(new StatsDto(
            period,
            totalCount,
            totalRevenue,
            avgTicket,
            revenueChange,
            countChange,
            salesByDay,
            byMethod,
            topProducts,
            lowStock));
    }
}
