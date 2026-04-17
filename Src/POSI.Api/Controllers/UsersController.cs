using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using POSI.Data;
using POSI.Domain.DTOs.Users;
using POSI.Domain.Entities;
using POSI.Domain.Interfaces;
using POSI.Domain.Settings;
using System.Security.Cryptography;

namespace POSI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantService _tenantService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IEmailService _emailService;
    private readonly EmailSettings _emailSettings;

    public UsersController(
        AppDbContext db,
        ITenantService tenantService,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IEmailService emailService,
        IOptions<EmailSettings> emailSettings)
    {
        _db = db;
        _tenantService = tenantService;
        _userManager = userManager;
        _roleManager = roleManager;
        _emailService = emailService;
        _emailSettings = emailSettings.Value;
    }

    // GET /api/users
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null) return Unauthorized();

        var users = await _db.Users
            .Where(u => u.TenantId == tenantId.Value)
            .OrderBy(u => u.CreatedAt)
            .ToListAsync();

        var result = new List<TenantUserDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new TenantUserDto(
                user.Id,
                user.Email!,
                user.FirstName,
                user.LastName,
                roles.FirstOrDefault() ?? "Cashier",
                user.CreatedAt));
        }
        return Ok(result);
    }

    // POST /api/users/invite
    [HttpPost("invite")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Invite([FromBody] InviteUserDto dto)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null) return Unauthorized();

        // Check plan limit
        var tenant = await _db.Tenants.FindAsync(tenantId.Value);
        if (tenant is not null)
        {
            var userCount = await _db.Users.CountAsync(u => u.TenantId == tenantId.Value);
            if (!PlanLimits.IsWithinUserLimit(tenant.Plan, userCount))
                return StatusCode(402, new { message = "Has alcanzado el límite de usuarios de tu plan." });
        }

        // Check duplicate email
        if (await _userManager.FindByEmailAsync(dto.Email) is not null)
            return Conflict(new { message = "Este correo ya está registrado." });

        // Validate role
        var validRoles = new[] { "Admin", "Manager", "Cashier" };
        if (!validRoles.Contains(dto.Role))
            return BadRequest(new { message = "Rol inválido. Debe ser Admin, Manager o Cashier." });

        // Generate temp password
        var tempPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(9))
            .Replace("+", "A").Replace("/", "B").Replace("=", "C");

        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            TenantId = tenantId.Value,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
        };

        var result = await _userManager.CreateAsync(user, tempPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return BadRequest(new { message = errors });
        }

        // Ensure role exists and assign
        if (!await _roleManager.RoleExistsAsync(dto.Role))
            await _roleManager.CreateAsync(new IdentityRole(dto.Role));
        await _userManager.AddToRoleAsync(user, dto.Role);

        // Send invite email
        var loginUrl = $"{_emailSettings.BaseUrl}";
        _ = _emailService.SendInviteEmailAsync(dto.Email, dto.FirstName, tenant?.Name ?? "POSI", tempPassword, loginUrl);

        var roles = await _userManager.GetRolesAsync(user);
        return StatusCode(201, new TenantUserDto(
            user.Id, user.Email!, user.FirstName, user.LastName,
            roles.FirstOrDefault() ?? dto.Role, user.CreatedAt));
    }

    // PUT /api/users/{id}/role
    [HttpPut("{id}/role")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateRole(string id, [FromBody] UpdateRoleDto dto)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null) return Unauthorized();

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId.Value);
        if (user is null) return NotFound();

        var validRoles = new[] { "Admin", "Manager", "Cashier" };
        if (!validRoles.Contains(dto.Role))
            return BadRequest(new { message = "Rol inválido." });

        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);

        if (!await _roleManager.RoleExistsAsync(dto.Role))
            await _roleManager.CreateAsync(new IdentityRole(dto.Role));
        await _userManager.AddToRoleAsync(user, dto.Role);

        return Ok(new { message = "Rol actualizado." });
    }

    // DELETE /api/users/{id}
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Remove(string id)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null) return Unauthorized();

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId.Value);
        if (user is null) return NotFound();

        // No permitir que el Admin se elimine a sí mismo
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (user.Id == currentUserId)
            return BadRequest(new { message = "No puedes eliminarte a ti mismo." });

        await _userManager.DeleteAsync(user);
        return NoContent();
    }
}
