using Microsoft.EntityFrameworkCore;
using POSI.Data;
using POSI.Domain.Constants;
using POSI.Domain.DTOs.CashRegister;
using POSI.Domain.Entities;
using POSI.Domain.Interfaces;

namespace POSI.Services;

/// <summary>
/// Servicio que gestiona las operaciones de caja registradora.
/// </summary>
public class CashRegisterService : ICashRegisterService
{
    private readonly AppDbContext _db;
    private readonly ITenantService _tenantService;

    /// <summary>
    /// Inicializa una nueva instancia del servicio de caja registradora.
    /// </summary>
    /// <param name="db">Contexto de base de datos.</param>
    /// <param name="tenantService">Servicio de resolución del tenant actual.</param>
    /// <exception cref="ArgumentNullException">Si <paramref name="db"/> o <paramref name="tenantService"/> son nulos.</exception>
    public CashRegisterService(AppDbContext db, ITenantService tenantService)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _tenantService = tenantService ?? throw new ArgumentNullException(nameof(tenantService));
    }

    private Guid? TenantId => _tenantService.GetCurrentTenantId();

    private Guid RequireTenantId() => TenantId ?? throw new InvalidOperationException("Tenant no autenticado.");

    /// <inheritdoc />
    public async Task<CashRegisterSessionDto?> GetCurrentAsync()
    {
        var tenantId = RequireTenantId();

        var session = await _db.CashRegisterSessions
            .Where(s => s.TenantId == tenantId && s.Status == CashRegisterStatuses.Open)
            .OrderByDescending(s => s.OpenedAt)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (session is null) return null;

        return new CashRegisterSessionDto(
            session.Id,
            session.OpeningCash,
            session.ClosingCash,
            session.ActualCash,
            session.Status,
            session.OpenedAt,
            session.ClosedAt,
            session.Notes);
    }

    /// <inheritdoc />
    public async Task<CashRegisterSessionDto> OpenAsync(OpenCashRegisterDto dto, string userId)
    {
        var tenantId = RequireTenantId();

        var stale = await _db.CashRegisterSessions
            .Where(s => s.TenantId == tenantId && s.Status == CashRegisterStatuses.Open)
            .ToListAsync()
            .ConfigureAwait(false);

        foreach (var s in stale)
        {
            s.Status = CashRegisterStatuses.Closed;
            s.ClosedAt = DateTime.UtcNow;
        }

        var session = new CashRegisterSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OpenedByUserId = userId,
            OpeningCash = dto.OpeningCash,
            Status = CashRegisterStatuses.Open,
            OpenedAt = DateTime.UtcNow,
            Notes = dto.Notes,
        };

        _db.CashRegisterSessions.Add(session);
        await _db.SaveChangesAsync().ConfigureAwait(false);

        return new CashRegisterSessionDto(
            session.Id,
            session.OpeningCash,
            null,
            null,
            session.Status,
            session.OpenedAt,
            null,
            session.Notes);
    }

    /// <inheritdoc />
    public async Task<CashRegisterSessionDto?> CloseAsync(CloseCashRegisterDto dto)
    {
        var tenantId = RequireTenantId();

        var session = await _db.CashRegisterSessions
            .Where(s => s.TenantId == tenantId && s.Status == CashRegisterStatuses.Open)
            .OrderByDescending(s => s.OpenedAt)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (session is null) return null;

        session.Status = CashRegisterStatuses.Closed;
        session.ClosedAt = DateTime.UtcNow;
        session.ActualCash = dto.ActualCash;
        session.Notes = dto.Notes ?? session.Notes;

        await _db.SaveChangesAsync().ConfigureAwait(false);

        return new CashRegisterSessionDto(
            session.Id,
            session.OpeningCash,
            session.ClosingCash,
            session.ActualCash,
            session.Status,
            session.OpenedAt,
            session.ClosedAt,
            session.Notes);
    }
}
