using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POSI.Domain.DTOs.Gastrobar;
using POSI.Domain.Interfaces;

namespace POSI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/gastrobar")]
public class GastrobarController : ControllerBase
{
    private readonly IGastrobarService _gastrobarService;

    public GastrobarController(IGastrobarService gastrobarService)
    {
        _gastrobarService = gastrobarService;
    }

    // Tables
    [HttpGet("tables")]
    public async Task<IActionResult> GetTables()
    {
        try
        {
            var tables = await _gastrobarService.GetTablesAsync();
            return Ok(tables);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("tables")]
    public async Task<IActionResult> CreateTable([FromBody] CreateTableDto dto)
    {
        try
        {
            var table = await _gastrobarService.CreateTableAsync(dto);
            return StatusCode(201, table);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("tables/{id:guid}/status")]
    public async Task<IActionResult> UpdateTableStatus(Guid id, [FromBody] UpdateTableStatusDto dto)
    {
        try
        {
            var table = await _gastrobarService.UpdateTableStatusAsync(id, dto);
            return Ok(table);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("tables/{id:guid}")]
    public async Task<IActionResult> DeleteTable(Guid id)
    {
        try
        {
            await _gastrobarService.DeleteTableAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // Orders
    [HttpGet("orders")]
    public async Task<IActionResult> GetActiveOrders()
    {
        try
        {
            var orders = await _gastrobarService.GetActiveOrdersAsync();
            return Ok(orders);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("orders/{id:guid}")]
    public async Task<IActionResult> GetOrder(Guid id)
    {
        try
        {
            var order = await _gastrobarService.GetOrderAsync(id);
            return Ok(order);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("tables/{tableId:guid}/orders")]
    public async Task<IActionResult> OpenOrder(Guid tableId)
    {
        try
        {
            var order = await _gastrobarService.OpenOrderAsync(tableId);
            return StatusCode(201, order);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("orders/{id:guid}/items")]
    public async Task<IActionResult> AddItems(Guid id, [FromBody] AddOrderItemsDto dto)
    {
        try
        {
            var order = await _gastrobarService.AddItemsAsync(id, dto);
            return Ok(order);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("orders/{id:guid}/items/{itemId:guid}/status")]
    public async Task<IActionResult> UpdateItemStatus(Guid id, Guid itemId, [FromBody] UpdateOrderItemStatusDto dto)
    {
        try
        {
            var item = await _gastrobarService.UpdateItemStatusAsync(itemId, dto);
            return Ok(item);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("orders/{id:guid}/close")]
    public async Task<IActionResult> CloseOrder(Guid id, [FromBody] CloseOrderDto dto)
    {
        try
        {
            var saleId = await _gastrobarService.CloseOrderAsync(id, dto);
            return Ok(new { saleId });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("orders/{id:guid}/cancel")]
    public async Task<IActionResult> CancelOrder(Guid id)
    {
        try
        {
            await _gastrobarService.CancelOrderAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
