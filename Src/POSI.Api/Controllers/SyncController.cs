using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POSI.Domain.DTOs.Sync;
using POSI.Domain.Interfaces;

namespace POSI.Api.Controllers;

[ApiController]
[Authorize]
public class SyncController : ControllerBase
{
    private readonly ISyncService _syncService;

    public SyncController(ISyncService syncService)
    {
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
    }

    [HttpPost("api/products/sync")]
    public async Task<IActionResult> SyncProducts([FromBody] SyncProductsRequestDto request)
    {
        try
        {
            var result = await _syncService.SyncProductsAsync(request);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    [HttpPost("api/sales/sync")]
    public async Task<IActionResult> SyncSales([FromBody] SyncSalesRequestDto request)
    {
        try
        {
            var result = await _syncService.SyncSalesAsync(request);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }
}
