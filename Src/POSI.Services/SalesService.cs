using Microsoft.EntityFrameworkCore;
using POSI.Data;
using POSI.Domain.Constants;
using POSI.Domain.Interfaces;

namespace POSI.Services;

/// <summary>
/// Servicio para la gestión de ventas.
/// </summary>
public class SalesService : ISalesService
{
    private readonly AppDbContext _db;
    private readonly ITenantService _tenantService;

    /// <summary>
    /// Inicializa una nueva instancia del servicio de ventas.
    /// </summary>
    /// <param name="db">Contexto de base de datos.</param>
    /// <param name="tenantService">Servicio de tenant.</param>
    /// <exception cref="ArgumentNullException">Si <paramref name="db"/> o <paramref name="tenantService"/> son nulos.</exception>
    public SalesService(AppDbContext db, ITenantService tenantService)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _tenantService = tenantService ?? throw new ArgumentNullException(nameof(tenantService));
    }

    /// <inheritdoc/>
    public async Task<bool> VoidAsync(Guid saleId)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null)
            throw new InvalidOperationException("Tenant no autenticado.");

        var sale = await _db.Sales
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == saleId && s.TenantId == tenantId.Value)
            .ConfigureAwait(false);

        if (sale is null)
            return false;

        if (sale.Status != SaleStatuses.Completed)
            throw new InvalidOperationException("Solo se pueden anular ventas completadas.");

        sale.Status = SaleStatuses.Voided;

        foreach (var item in sale.Items)
        {
            if (item.ProductId is null)
                continue;

            var product = await _db.Products
                .FindAsync(item.ProductId.Value)
                .ConfigureAwait(false);

            if (product is not null)
                product.Stock += item.Quantity;
        }

        await _db.SaveChangesAsync().ConfigureAwait(false);
        return true;
    }
}
