using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POSI.Domain.DTOs.SuperAdmin;
using POSI.Domain.Interfaces;

namespace POSI.Api.Controllers;

[ApiController]
[Route("api/superadmin")]
public class SuperAdminController : ControllerBase
{
    private readonly ISuperAdminService _superAdminService;

    public SuperAdminController(ISuperAdminService superAdminService)
    {
        _superAdminService = superAdminService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] SuperAdminLoginRequestDto dto)
    {
        try
        {
            var result = await _superAdminService.LoginAsync(dto);
            if (result == null)
                return Unauthorized(new { message = "Invalid credentials or user is not a super admin." });

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred during login.", detail = ex.Message });
        }
    }

    [HttpGet("dashboard")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> GetDashboard()
    {
        try
        {
            var dashboard = await _superAdminService.GetDashboardAsync();
            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while fetching the dashboard.", detail = ex.Message });
        }
    }

    [HttpGet("tenants")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> GetAllTenants()
    {
        try
        {
            var tenants = await _superAdminService.GetAllTenantsAsync();
            return Ok(tenants);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while fetching tenants.", detail = ex.Message });
        }
    }

    [HttpGet("tenants/{tenantId:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> GetTenantById(Guid tenantId)
    {
        try
        {
            var tenant = await _superAdminService.GetTenantByIdAsync(tenantId);
            if (tenant == null)
                return NotFound(new { message = "Tenant not found." });

            return Ok(tenant);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while fetching the tenant.", detail = ex.Message });
        }
    }

    [HttpPost("tenants")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantDto dto)
    {
        try
        {
            var tenant = await _superAdminService.CreateTenantAsync(dto);
            return Ok(tenant);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while creating the tenant.", detail = ex.Message });
        }
    }

    [HttpPut("tenants/{tenantId:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> UpdateTenant(Guid tenantId, [FromBody] UpdateTenantDto dto)
    {
        try
        {
            var updated = await _superAdminService.UpdateTenantAsync(tenantId, dto);
            if (!updated)
                return NotFound(new { message = "Tenant not found." });

            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while updating the tenant.", detail = ex.Message });
        }
    }

    [HttpDelete("tenants/{tenantId:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> DeleteTenant(Guid tenantId)
    {
        try
        {
            var deleted = await _superAdminService.DeleteTenantAsync(tenantId);
            if (!deleted)
                return NotFound(new { message = "Tenant not found." });

            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while deleting the tenant.", detail = ex.Message });
        }
    }

    [HttpGet("users")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> GetAllUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 500) pageSize = 500;

            var users = await _superAdminService.GetAllUsersAsync(page, pageSize);
            return Ok(users);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while fetching users.", detail = ex.Message });
        }
    }

    [HttpDelete("users/{userId}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        try
        {
            var deleted = await _superAdminService.DeleteUserAsync(userId);
            if (!deleted)
                return NotFound(new { message = "User not found." });

            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while deleting the user.", detail = ex.Message });
        }
    }

    [HttpGet("tenants/{tenantId:guid}/stats")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> GetTenantStats(Guid tenantId)
    {
        try
        {
            var stats = await _superAdminService.GetTenantStatsAsync(tenantId);
            if (stats == null)
                return NotFound(new { message = "Tenant not found." });

            return Ok(stats);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while fetching tenant stats.", detail = ex.Message });
        }
    }
}
