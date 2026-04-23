using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POSI.Domain.Interfaces;

namespace POSI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/sales")]
public class SalesController : ControllerBase
{
    private readonly ISalesService _salesService;

    public SalesController(ISalesService salesService)
    {
        _salesService = salesService ?? throw new ArgumentNullException(nameof(salesService));
    }

    [HttpPost("{id:guid}/void")]
    public async Task<IActionResult> VoidSale(Guid id)
    {
        try
        {
            var result = await _salesService.VoidAsync(id);
            if (!result) return NotFound(new { message = "Venta no encontrada." });
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
