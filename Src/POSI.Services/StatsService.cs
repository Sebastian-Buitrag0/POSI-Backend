using Microsoft.EntityFrameworkCore;
using POSI.Data;
using POSI.Domain.Constants;
using POSI.Domain.DTOs.Stats;
using POSI.Domain.Entities;
using POSI.Domain.Interfaces;

namespace POSI.Services;

/// <summary>
/// Servicio para la consulta de estadísticas y datos de panel de control.
/// </summary>
public class StatsService : IStatsService
{
    private readonly AppDbContext _db;
    private readonly ITenantService _tenantService;

    /// <summary>
    /// Inicializa una nueva instancia del servicio de estadísticas.
    /// </summary>
    /// <param name="db">Contexto de base de datos.</param>
    /// <param name="tenantService">Servicio de tenant.</param>
    /// <exception cref="ArgumentNullException">Si <paramref name="db"/> o <paramref name="tenantService"/> son nulos.</exception>
    public StatsService(AppDbContext db, ITenantService tenantService)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _tenantService = tenantService ?? throw new ArgumentNullException(nameof(tenantService));
    }

    /// <inheritdoc/>
    public async Task<StatsDto> GetAsync(string period)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null)
            throw new InvalidOperationException("Tenant no autenticado.");

        var now = DateTime.UtcNow;
        var today = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var prevMonthStart = monthStart.AddMonths(-1);

        var (start, prevStart, prevEnd) = period switch
        {
            "today" => (today, today.AddDays(-1), today),
            "month" => (monthStart, prevMonthStart, monthStart),
            _ => (today.AddDays(-6), today.AddDays(-13), today.AddDays(-6)),
        };

        var sales = await _db.Sales
            .Where(s => s.TenantId == tenantId.Value && s.Status == SaleStatuses.Completed && s.CreatedAt >= start)
            .ToListAsync()
            .ConfigureAwait(false);

        var prevSales = await _db.Sales
            .Where(s => s.TenantId == tenantId.Value && s.Status == SaleStatuses.Completed
                        && s.CreatedAt >= prevStart && s.CreatedAt < prevEnd)
            .ToListAsync()
            .ConfigureAwait(false);

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

        var chartStart = today.AddDays(-6);
        var chartSales = await _db.Sales
            .Where(s => s.TenantId == tenantId.Value && s.Status == SaleStatuses.Completed && s.CreatedAt >= chartStart)
            .ToListAsync()
            .ConfigureAwait(false);

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

        var byMethod = sales
            .GroupBy(s => s.PaymentMethod)
            .Select(g => new PaymentMethodDto(g.Key, g.Sum(s => s.Total), g.Count()))
            .OrderByDescending(x => x.Revenue)
            .ToList();

        var saleIds = sales.Select(s => s.Id).ToList();
        List<SaleItem> items = [];
        if (saleIds.Count > 0)
        {
            items = await _db.SaleItems
                .Where(si => saleIds.Contains(si.SaleId))
                .ToListAsync()
                .ConfigureAwait(false);
        }

        var topProducts = items
            .GroupBy(si => si.ProductName)
            .Select(g => new TopProductDto(g.Key, g.Sum(si => si.Quantity), g.Sum(si => si.Subtotal)))
            .OrderByDescending(x => x.Revenue)
            .Take(5)
            .ToList();

        var lowStockRaw = await _db.Products
            .Where(p => p.TenantId == tenantId.Value && p.IsActive && p.Stock <= p.MinStock)
            .OrderBy(p => p.Stock)
            .Take(10)
            .ToListAsync()
            .ConfigureAwait(false);

        var lowStock = lowStockRaw
            .Select(p => new LowStockDto(p.Id.ToString(), p.Name, p.Stock, p.MinStock))
            .ToList();

        return new StatsDto(
            period,
            totalCount,
            totalRevenue,
            avgTicket,
            revenueChange,
            countChange,
            salesByDay,
            byMethod,
            topProducts,
            lowStock);
    }
}
