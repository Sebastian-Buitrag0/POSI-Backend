using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POSI.Data;
using POSI.Domain.Entities;
using POSI.Domain.Interfaces;

namespace POSI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/cash-register")]
public class CashRegisterController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantService _tenantService;

    public CashRegisterController(AppDbContext db, ITenantService tenantService)
    {
        _db = db;
        _tenantService = tenantService;
    }

    // GET /api/cash-register/current
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent()
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null) return Unauthorized();

        var session = await _db.CashRegisterSessions
            .Where(s => s.TenantId == tenantId.Value && s.Status == "open")
            .OrderByDescending(s => s.OpenedAt)
            .FirstOrDefaultAsync();

        if (session is null) return Ok(new { isOpen = false });

        return Ok(new CashRegisterSessionDto(
            session.Id,
            session.OpeningCash,
            session.ClosingCash,
            session.ActualCash,
            session.Status,
            session.OpenedAt,
            session.ClosedAt,
            session.Notes));
    }

    // POST /api/cash-register/open
    [HttpPost("open")]
    public async Task<IActionResult> Open([FromBody] OpenCashRegisterDto request)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null) return Unauthorized();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub");
        if (userId is null) return Unauthorized();

        // Close any stale open session first
        var stale = await _db.CashRegisterSessions
            .Where(s => s.TenantId == tenantId.Value && s.Status == "open")
            .ToListAsync();
        foreach (var s in stale)
        {
            s.Status = "closed";
            s.ClosedAt = DateTime.UtcNow;
        }

        var session = new CashRegisterSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            OpenedByUserId = userId,
            OpeningCash = request.OpeningCash,
            Status = "open",
            OpenedAt = DateTime.UtcNow,
            Notes = request.Notes,
        };

        _db.CashRegisterSessions.Add(session);
        await _db.SaveChangesAsync();

        return Created($"/api/cash-register/{session.Id}",
            new CashRegisterSessionDto(
                session.Id,
                session.OpeningCash,
                null, null,
                session.Status,
                session.OpenedAt,
                null,
                session.Notes));
    }

    // POST /api/cash-register/close
    [HttpPost("close")]
    public async Task<IActionResult> Close([FromBody] CloseCashRegisterDto request)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null) return Unauthorized();

        var session = await _db.CashRegisterSessions
            .Where(s => s.TenantId == tenantId.Value && s.Status == "open")
            .OrderByDescending(s => s.OpenedAt)
            .FirstOrDefaultAsync();

        if (session is null) return NotFound(new { message = "No hay caja abierta." });

        session.Status = "closed";
        session.ClosedAt = DateTime.UtcNow;
        session.ActualCash = request.ActualCash;
        session.Notes = request.Notes ?? session.Notes;

        await _db.SaveChangesAsync();

        return Ok(new CashRegisterSessionDto(
            session.Id,
            session.OpeningCash,
            session.ClosingCash,
            session.ActualCash,
            session.Status,
            session.OpenedAt,
            session.ClosedAt,
            session.Notes));
    }
}

public record OpenCashRegisterDto(decimal OpeningCash, string? Notes);
public record CloseCashRegisterDto(decimal ActualCash, string? Notes);
public record CashRegisterSessionDto(
    Guid Id,
    decimal OpeningCash,
    decimal? ClosingCash,
    decimal? ActualCash,
    string Status,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    string? Notes);
