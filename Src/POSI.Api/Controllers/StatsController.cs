using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POSI.Domain.Interfaces;

namespace POSI.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/stats")]
public class StatsController : ControllerBase
{
    private readonly IStatsService _statsService;

    public StatsController(IStatsService statsService)
    {
        _statsService = statsService ?? throw new ArgumentNullException(nameof(statsService));
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string period = "week")
    {
        try
        {
            var stats = await _statsService.GetAsync(period);
            return Ok(stats);
        }
        catch (InvalidOperationException)
        {
            return Unauthorized();
        }
    }
}
