# Spec 015-A — Multi-usuario + Roles (Backend)

## Objetivo
- Roles: `Admin`, `Manager`, `Cashier`
- Admin puede invitar usuarios al tenant, verlos y eliminarlos
- Invitación: crea el usuario con password temporal y manda email
- Plan limit: free=1 usuario, pro=5, business=ilimitado
- Guards en endpoints: solo Admin puede crear/editar/eliminar productos y gestionar usuarios

## Infraestructura existente
- `ApplicationUser` con `TenantId`, `EmailConfirmed`
- `UserManager<ApplicationUser>`, `RoleManager<IdentityRole>`
- `IEmailService` — `SendVerificationEmailAsync`, `SendPasswordResetEmailAsync`
- `PlanLimits` — solo tiene `ProductLimits`, hay que agregarle `UserLimits`
- `ProductsController` — tiene `[Authorize]` a nivel de clase
- `Tenant.Name` — nombre del negocio para el email de invitación
- `AppDbContext.Users` DbSet (heredado de IdentityDbContext)
- Email de invitación necesita `EmailSettings.BaseUrl` para el link de login

---

## Task 15.0 — Agregar UserLimits a PlanLimits

En `Src/POSI.Domain/Settings/PlanLimits.cs`, agregar después del bloque `ProductLimits`:

```csharp
private static readonly Dictionary<string, int> UserLimits = new(StringComparer.OrdinalIgnoreCase)
{
    ["free"]     = 1,
    ["pro"]      = 5,
    ["business"] = Unlimited,
};

public static int GetUserLimit(string plan) =>
    UserLimits.TryGetValue(plan, out var limit) ? limit : 1;

public static bool IsWithinUserLimit(string plan, int currentCount) =>
    GetUserLimit(plan) == Unlimited || currentCount < GetUserLimit(plan);
```

---

## Task 15.1 — Agregar SendInviteEmailAsync a IEmailService

En `Src/POSI.Domain/Interfaces/IEmailService.cs`, agregar:
```csharp
Task SendInviteEmailAsync(string toEmail, string firstName, string businessName, string tempPassword, string loginUrl);
```

En `Src/POSI.Api/Services/EmailService.cs`, implementar:

```csharp
public async Task SendInviteEmailAsync(string toEmail, string firstName, string businessName, string tempPassword, string loginUrl)
{
    var html = $"""
        <div style="font-family:sans-serif;max-width:480px;margin:auto;padding:32px">
          <h2 style="color:#3B82F6">Te invitaron a {businessName}</h2>
          <p>Hola {firstName}, <strong>{businessName}</strong> te ha invitado a usar POSI.</p>
          <p>Tus credenciales de acceso:</p>
          <ul>
            <li><strong>Correo:</strong> {toEmail}</li>
            <li><strong>Contraseña temporal:</strong> <code style="background:#F3F4F6;padding:4px 8px;border-radius:4px">{tempPassword}</code></li>
          </ul>
          <a href="{loginUrl}"
             style="display:inline-block;background:#3B82F6;color:#fff;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:bold">
            Iniciar sesión
          </a>
          <p style="color:#6B7280;margin-top:24px;font-size:13px">
            Por seguridad, cambia tu contraseña después de iniciar sesión.
          </p>
        </div>
        """;
    await SendAsync(toEmail, $"Invitación a {businessName} — POSI", html);
}
```

---

## Task 15.2 — DTOs de usuarios

### `Src/POSI.Domain/DTOs/Users/TenantUserDto.cs`
```csharp
namespace POSI.Domain.DTOs.Users;

public record TenantUserDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    DateTime CreatedAt
);

public record InviteUserDto(
    string Email,
    string FirstName,
    string LastName,
    string Role
);

public record UpdateRoleDto(string Role);
```

---

## Task 15.3 — UsersController

### `Src/POSI.Api/Controllers/UsersController.cs`

```csharp
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
            EmailConfirmed = true, // Admin lo invita explícitamente
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
```

---

## Task 15.4 — Guards en ProductsController

En `Src/POSI.Api/Controllers/ProductsController.cs`, agregar `[Authorize(Roles = "Admin")]`
en los métodos `Create`, `Update` y `Delete` (el GET queda sin restricción de rol):

```csharp
[HttpPost]
[Authorize(Roles = "Admin,Manager")]
public async Task<IActionResult> Create(...)

[HttpPut("{id:guid}")]
[Authorize(Roles = "Admin,Manager")]
public async Task<IActionResult> Update(...)

[HttpDelete("{id:guid}")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> Delete(...)
```

---

## Task 15.5 — Validación

```bash
cd /Users/sebastian-buitrago/Documents/Yo/POSI/POSI-Backend
dotnet build POSI.sln
```

**0 errores, 0 warnings.**

---

## Archivos a crear
```
Src/POSI.Domain/DTOs/Users/TenantUserDto.cs   ← Task 15.2
Src/POSI.Api/Controllers/UsersController.cs   ← Task 15.3
```

## Archivos a modificar
```
Src/POSI.Domain/Settings/PlanLimits.cs              ← Task 15.0
Src/POSI.Domain/Interfaces/IEmailService.cs          ← Task 15.1
Src/POSI.Api/Services/EmailService.cs                ← Task 15.1
Src/POSI.Api/Controllers/ProductsController.cs       ← Task 15.4
```

## IMPORTANTE — No hacer
- NO crear migraciones — `ApplicationUser.TenantId` ya existe
- NO modificar `AppDbContext`
- Manager puede crear/editar productos pero NO eliminar (solo Admin)
- `EmailConfirmed = true` para usuarios invitados (Admin los invita explícitamente)
- No permitir que un Admin se auto-elimine
