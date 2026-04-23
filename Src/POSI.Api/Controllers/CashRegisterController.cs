using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POSI.Domain.DTOs.CashRegister;
using POSI.Domain.Interfaces;

namespace POSI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/cash-register")]
public class CashRegisterController : ControllerBase
{
    private readonly ICashRegisterService _cashRegisterService;

    public CashRegisterController(ICashRegisterService cashRegisterService)
    {
        _cashRegisterService = cashRegisterService ?? throw new ArgumentNullException(nameof(cashRegisterService));
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent()
    {
        try
        {
            var session = await _cashRegisterService.GetCurrentAsync();
            if (session is null) return Ok(new { isOpen = false });
            return Ok(session);
        }
        catch (InvalidOperationException)
        {
            return Unauthorized();
        }
    }

    [HttpPost("open")]
    public async Task<IActionResult> Open([FromBody] OpenCashRegisterDto request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub");
        if (userId is null) return Unauthorized();

        try
        {
            var session = await _cashRegisterService.OpenAsync(request, userId);
            return Created($"/api/cash-register/{session.Id}", session);
        }
        catch (InvalidOperationException)
        {
            return Unauthorized();
        }
    }

    [HttpPost("close")]
    public async Task<IActionResult> Close([FromBody] CloseCashRegisterDto request)
    {
        try
        {
            var session = await _cashRegisterService.CloseAsync(request);
            if (session is null) return NotFound(new { message = "No hay caja abierta." });
            return Ok(session);
        }
        catch (InvalidOperationException)
        {
            return Unauthorized();
        }
    }
}
