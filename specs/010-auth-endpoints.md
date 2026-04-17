# Spec 010 — Auth Endpoints (Register / Login / Refresh / Logout / Profile)

## Objetivo
Implementar los 5 endpoints de autenticación que el Flutter ya consume:
- `POST /api/auth/register` → crea Tenant + User Admin → devuelve JWT
- `POST /api/auth/login` → valida credenciales → devuelve JWT
- `POST /api/auth/refresh` → rota el refresh token
- `POST /api/auth/logout` → revoca el refresh token
- `GET  /api/auth/profile` → devuelve el usuario autenticado

## JSON exacto que espera el Flutter

### AuthResponse (login y register):
```json
{
  "accessToken": "eyJ...",
  "refreshToken": "abc123...",
  "user": {
    "id": "string",
    "email": "string",
    "firstName": "string",
    "lastName": "string",
    "role": "Admin",
    "tenantId": "string",
    "createdAt": "2024-01-15T10:00:00Z"
  }
}
```

### RefreshResponse:
```json
{ "accessToken": "...", "refreshToken": "..." }
```

### RegisterRequest (del Flutter):
```json
{ "email": "...", "password": "...", "firstName": "...", "lastName": "...", "businessName": "..." }
```

### LoginRequest:
```json
{ "email": "...", "password": "..." }
```

### RefreshRequest:
```json
{ "refreshToken": "..." }
```

## Infraestructura existente (NO recrear)
- `POSI.Domain/Entities/` — todas las entidades ya existen
- `POSI.Domain/Interfaces/ITenantService.cs` — ya existe
- `POSI.Data/AppDbContext.cs` — ya existe con DbSets
- `POSI.Api/Program.cs` — ya tiene Identity, JWT, Serilog, Swagger con Bearer
- `appsettings.json` — ya tiene `Jwt` y `ConnectionStrings`

## Paquetes que faltan

### `Src/POSI.Services/POSI.Services.csproj`
Reemplazar contenido completo:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.0.3" />
    <PackageReference Include="Microsoft.IdentityModel.Tokens" Version="8.0.3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\POSI.Domain\POSI.Domain.csproj" />
    <ProjectReference Include="..\POSI.Data\POSI.Data.csproj" />
  </ItemGroup>

</Project>
```

---

## Task 10.0 — Excepciones de dominio

### `Src/POSI.Domain/Exceptions/AuthException.cs`
```csharp
namespace POSI.Domain.Exceptions;

public class DuplicateEmailException : Exception
{
    public DuplicateEmailException() : base("El email ya está registrado.") { }
}

public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException() : base("Email o contraseña incorrectos.") { }
}

public class InvalidRefreshTokenException : Exception
{
    public InvalidRefreshTokenException() : base("Token de refresco inválido o expirado.") { }
}
```

---

## Task 10.1 — JwtSettings + IAuthService

### `Src/POSI.Domain/Settings/JwtSettings.cs`
```csharp
namespace POSI.Domain.Settings;

public class JwtSettings
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
```

### `Src/POSI.Domain/Interfaces/IAuthService.cs`
```csharp
using POSI.Domain.DTOs.Auth;

namespace POSI.Domain.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request);
    Task<TokenResponseDto> RefreshAsync(string refreshToken);
    Task LogoutAsync(string refreshToken);
    Task<UserDto> GetProfileAsync(string userId);
}
```

---

## Task 10.2 — DTOs

### `Src/POSI.Domain/DTOs/Auth/RegisterRequestDto.cs`
```csharp
namespace POSI.Domain.DTOs.Auth;

public record RegisterRequestDto(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string BusinessName
);
```

### `Src/POSI.Domain/DTOs/Auth/LoginRequestDto.cs`
```csharp
namespace POSI.Domain.DTOs.Auth;

public record LoginRequestDto(string Email, string Password);
```

### `Src/POSI.Domain/DTOs/Auth/UserDto.cs`
```csharp
namespace POSI.Domain.DTOs.Auth;

/// Debe coincidir EXACTAMENTE con UserEntity.fromJson() del Flutter
public record UserDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    string TenantId,
    DateTime CreatedAt
);
```

### `Src/POSI.Domain/DTOs/Auth/AuthResponseDto.cs`
```csharp
namespace POSI.Domain.DTOs.Auth;

/// Coincide con AuthResponse.fromJson() del Flutter
public record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    UserDto User
);
```

### `Src/POSI.Domain/DTOs/Auth/TokenResponseDto.cs`
```csharp
namespace POSI.Domain.DTOs.Auth;

public record TokenResponseDto(string AccessToken, string RefreshToken);
```

---

## Task 10.3 — AuthService (`POSI.Services`)

### `Src/POSI.Services/AuthService.cs`

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using POSI.Data;
using POSI.Domain.DTOs.Auth;
using POSI.Domain.Entities;
using POSI.Domain.Exceptions;
using POSI.Domain.Interfaces;
using POSI.Domain.Settings;

namespace POSI.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly AppDbContext _db;
    private readonly JwtSettings _jwt;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        AppDbContext db,
        IOptions<JwtSettings> jwt)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _db = db;
        _jwt = jwt.Value;
    }

    // ── Register ──────────────────────────────────────────────────────────

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request)
    {
        // Verificar email único
        if (await _userManager.FindByEmailAsync(request.Email) is not null)
            throw new DuplicateEmailException();

        // Crear Tenant
        var slug = await GenerateUniqueSlugAsync(request.BusinessName);
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.BusinessName,
            Slug = slug,
        };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync();

        // Crear usuario
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            TenantId = tenant.Id,
            CreatedAt = DateTime.UtcNow,
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            // Rollback tenant si falla el usuario
            _db.Tenants.Remove(tenant);
            await _db.SaveChangesAsync();
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException(errors);
        }

        // Asignar rol Admin (primer usuario del tenant)
        await EnsureRoleExistsAsync("Admin");
        await _userManager.AddToRoleAsync(user, "Admin");

        return await BuildAuthResponseAsync(user, "Admin");
    }

    // ── Login ─────────────────────────────────────────────────────────────

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email)
            ?? throw new InvalidCredentialsException();

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
            throw new InvalidCredentialsException();

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "Cashier";

        return await BuildAuthResponseAsync(user, role);
    }

    // ── Refresh ───────────────────────────────────────────────────────────

    public async Task<TokenResponseDto> RefreshAsync(string refreshToken)
    {
        var token = await _db.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == refreshToken && !r.IsRevoked)
            ?? throw new InvalidRefreshTokenException();

        if (token.ExpiresAt < DateTime.UtcNow)
        {
            token.IsRevoked = true;
            await _db.SaveChangesAsync();
            throw new InvalidRefreshTokenException();
        }

        // Rotar: revocar el actual y generar uno nuevo
        token.IsRevoked = true;

        var roles = await _userManager.GetRolesAsync(token.User);
        var role = roles.FirstOrDefault() ?? "Cashier";

        var newAccessToken = GenerateAccessToken(token.User, role);
        var newRefreshToken = await CreateRefreshTokenAsync(token.User.Id);

        await _db.SaveChangesAsync();

        return new TokenResponseDto(newAccessToken, newRefreshToken.Token);
    }

    // ── Logout ────────────────────────────────────────────────────────────

    public async Task LogoutAsync(string refreshToken)
    {
        var token = await _db.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == refreshToken);

        if (token is not null)
        {
            token.IsRevoked = true;
            await _db.SaveChangesAsync();
        }
    }

    // ── Profile ───────────────────────────────────────────────────────────

    public async Task<UserDto> GetProfileAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Usuario no encontrado.");

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "Cashier";

        return MapUserDto(user, role);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task<AuthResponseDto> BuildAuthResponseAsync(ApplicationUser user, string role)
    {
        var accessToken = GenerateAccessToken(user, role);
        var refreshToken = await CreateRefreshTokenAsync(user.Id);
        await _db.SaveChangesAsync();

        return new AuthResponseDto(
            accessToken,
            refreshToken.Token,
            MapUserDto(user, role)
        );
    }

    private string GenerateAccessToken(ApplicationUser user, string role)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, role),
            new Claim("tenantId", user.TenantId.ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwt.AccessTokenExpirationMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<RefreshToken> CreateRefreshTokenAsync(string userId)
    {
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpirationDays),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow,
        };
        _db.RefreshTokens.Add(token);
        return token;
    }

    private static UserDto MapUserDto(ApplicationUser user, string role) => new(
        user.Id,
        user.Email!,
        user.FirstName,
        user.LastName,
        role,
        user.TenantId.ToString(),
        user.CreatedAt
    );

    private async Task EnsureRoleExistsAsync(string roleName)
    {
        if (!await _roleManager.RoleExistsAsync(roleName))
            await _roleManager.CreateAsync(new IdentityRole(roleName));
    }

    private async Task<string> GenerateUniqueSlugAsync(string businessName)
    {
        var baseSlug = Regex.Replace(
            businessName.ToLower().Normalize().Trim(),
            @"[^a-z0-9]+", "-").Trim('-');

        var slug = baseSlug;
        var counter = 2;
        while (await _db.Tenants.AnyAsync(t => t.Slug == slug))
        {
            slug = $"{baseSlug}-{counter++}";
        }
        return slug;
    }
}
```

---

## Task 10.4 — AuthController (`POSI.Api`)

### `Src/POSI.Api/Controllers/AuthController.cs`

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POSI.Domain.DTOs.Auth;
using POSI.Domain.Exceptions;
using POSI.Domain.Interfaces;

namespace POSI.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
        => _authService = authService;

    // POST /api/auth/register
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        try
        {
            var response = await _authService.RegisterAsync(request);
            return StatusCode(201, response);
        }
        catch (DuplicateEmailException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        try
        {
            var response = await _authService.LoginAsync(request);
            return Ok(response);
        }
        catch (InvalidCredentialsException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    // POST /api/auth/refresh
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequestDto request)
    {
        try
        {
            var response = await _authService.RefreshAsync(request.RefreshToken);
            return Ok(response);
        }
        catch (InvalidRefreshTokenException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    // POST /api/auth/logout
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequestDto? request)
    {
        if (request?.RefreshToken is not null)
            await _authService.LogoutAsync(request.RefreshToken);
        return NoContent();
    }

    // GET /api/auth/profile
    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> Profile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (userId is null)
            return Unauthorized(new { message = "Token inválido." });

        try
        {
            var user = await _authService.GetProfileAsync(userId);
            return Ok(user);
        }
        catch (Exception ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
```

### `Src/POSI.Api/Controllers/RefreshRequestDto.cs` (DTO local del controller)

**IMPORTANTE:** En vez de crear un archivo separado, agregar este record al final del archivo `AuthController.cs`, dentro del namespace `POSI.Api.Controllers`:

```csharp
// Al final del archivo AuthController.cs, después de la clase:
public record RefreshRequestDto(string RefreshToken);
```

---

## Task 10.5 — Registrar IAuthService en Program.cs

Añadir estas dos líneas en `Src/POSI.Api/Program.cs`, justo antes de `builder.Services.AddControllers()`:

```csharp
// Añadir using al inicio del archivo:
using POSI.Services;
using POSI.Domain.Settings;

// Añadir antes de builder.Services.AddControllers():
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<IAuthService, AuthService>();
```

---

## Task 10.6 — Build y validación

```bash
cd /Users/sebastian-buitrago/Documents/Yo/POSI/POSI-Backend
dotnet build POSI.sln
```

Debe terminar con **Build succeeded. 0 Warning(s). 0 Error(s).**

---

## Estructura de archivos a crear/modificar

### Crear (nuevos):
```
Src/POSI.Domain/
  Exceptions/
    AuthException.cs            ← Task 10.0
  Settings/
    JwtSettings.cs              ← Task 10.1
  Interfaces/
    IAuthService.cs             ← Task 10.1
  DTOs/
    Auth/
      RegisterRequestDto.cs     ← Task 10.2
      LoginRequestDto.cs        ← Task 10.2
      UserDto.cs                ← Task 10.2
      AuthResponseDto.cs        ← Task 10.2
      TokenResponseDto.cs       ← Task 10.2

Src/POSI.Services/
  AuthService.cs                ← Task 10.3

Src/POSI.Api/
  Controllers/
    AuthController.cs           ← Task 10.4
```

### Modificar (existentes):
```
Src/POSI.Services/POSI.Services.csproj  ← Task 10.0 (añadir paquetes JWT)
Src/POSI.Api/Program.cs                 ← Task 10.5 (registrar IAuthService)
```

## Orden de ejecución OBLIGATORIO

1. Task 10.0 — Actualizar POSI.Services.csproj
2. Task 10.1 — Excepciones + JwtSettings + IAuthService
3. Task 10.2 — DTOs
4. Task 10.3 — AuthService
5. Task 10.4 — AuthController
6. Task 10.5 — Registrar en Program.cs
7. Task 10.6 — dotnet build

## Validación final
```bash
dotnet build POSI.sln
```
**0 errores, 0 warnings.**

## IMPORTANTE — No hacer
- NO usar `SignInManager` — usar `CheckPasswordAsync` directamente
- NO cambiar la estructura del JWT (el Flutter parsea `tenantId` como claim custom)
- NO crear endpoints adicionales — solo los 5 de auth
- NO modificar AppDbContext ni las entidades
- NO crear migraciones nuevas — no hay cambios al schema
- El `RefreshRequestDto` puede vivir al final de `AuthController.cs` en el mismo namespace
