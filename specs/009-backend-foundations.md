# Spec 009 — Backend: Cimientos (EF Core + PostgreSQL + Identity + JWT)

## Objetivo
Configurar la infraestructura base del backend .NET 8:
- Entidades de dominio que reflejan el modelo del frontend Flutter
- AppDbContext con multi-tenant (global query filters por TenantId)
- ASP.NET Core Identity para manejo de usuarios
- JWT authentication listo para los endpoints de auth
- Migración inicial de base de datos

**Este paso NO incluye endpoints** — solo la infraestructura. Los endpoints van en Paso 10.

## Estado actual
- `POSI.Api/Program.cs` → plantilla WeatherForecast (borrar ese código)
- `POSI.Domain/Class1.cs`, `POSI.Data/Class1.cs`, `POSI.Services/Class1.cs` → vacíos
- `Src/POSSI.Domain/` → carpeta con typo, ignorar (no está en references)
- Paquetes ya en `POSI.Api.csproj`: Serilog, Swashbuckle, System.IdentityModel.Tokens.Jwt

## Estructura de proyectos (NO cambiar)
```
POSI.sln
Src/
  POSI.Api/       → Entry point, Controllers, Middleware
  POSI.Services/  → Business logic (referencias: Domain + Data)
  POSI.Data/      → EF Core, DbContext, Migrations (referencia: Domain)
  POSI.Domain/    → Entidades, Interfaces. Sin dependencias.
```

---

## Task 9.0 — Actualizar paquetes NuGet

### `Src/POSI.Domain/POSI.Domain.csproj`
Sin cambios (no necesita paquetes externos).

### `Src/POSI.Data/POSI.Data.csproj`
Reemplazar contenido completo:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.11" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.11" />
    <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="8.0.11" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.11">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\POSI.Domain\POSI.Domain.csproj" />
  </ItemGroup>

</Project>
```

### `Src/POSI.Api/POSI.Api.csproj`
Reemplazar contenido completo:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.11" />
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="8.0.11" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.11">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Serilog.AspNetCore" Version="8.0.3" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.6.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\POSI.Services\POSI.Services.csproj" />
    <ProjectReference Include="..\POSI.Data\POSI.Data.csproj" />
  </ItemGroup>

</Project>
```

**Nota:** Se elimina `System.IdentityModel.Tokens.Jwt` — viene incluido en `Microsoft.AspNetCore.Authentication.JwtBearer`.
Se baja `Serilog.AspNetCore` de 10.0.0 a 8.0.3 (compatible con net8.0).

### `Src/POSI.Services/POSI.Services.csproj`
Sin cambios.

Después de editar los .csproj, ejecutar:
```bash
cd /Users/sebastian-buitrago/Documents/Yo/POSI/POSI-Backend
dotnet restore POSI.sln
```

---

## Task 9.1 — Entidades de dominio (`POSI.Domain`)

Eliminar `Src/POSI.Domain/Class1.cs` y crear los siguientes archivos:

### `Src/POSI.Domain/Entities/Tenant.cs`
```csharp
namespace POSI.Domain.Entities;

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;       // businessName del registro
    public string Slug { get; set; } = string.Empty;       // URL-friendly, único
    public string Plan { get; set; } = "free";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<ApplicationUser> Users { get; set; } = [];
    public ICollection<Product> Products { get; set; } = [];
    public ICollection<Category> Categories { get; set; } = [];
    public ICollection<Sale> Sales { get; set; } = [];
}
```

### `Src/POSI.Domain/Entities/ApplicationUser.cs`
```csharp
using Microsoft.AspNetCore.Identity;

namespace POSI.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
```

### `Src/POSI.Domain/Entities/RefreshToken.cs`
```csharp
namespace POSI.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Token { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ApplicationUser User { get; set; } = null!;
}
```

### `Src/POSI.Domain/Entities/Product.cs`
```csharp
namespace POSI.Domain.Entities;

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string? Barcode { get; set; }
    public decimal Price { get; set; }
    public decimal? Cost { get; set; }
    public int Stock { get; set; }
    public int MinStock { get; set; }
    public Guid? CategoryId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public Category? Category { get; set; }
}
```

### `Src/POSI.Domain/Entities/Category.cs`
```csharp
namespace POSI.Domain.Entities;

public class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public ICollection<Product> Products { get; set; } = [];
}
```

### `Src/POSI.Domain/Entities/Sale.cs`
```csharp
namespace POSI.Domain.Entities;

public class Sale
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string SaleNumber { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public string PaymentMethod { get; set; } = "cash";   // cash | card | transfer
    public string Status { get; set; } = "completed";     // completed | cancelled | refunded
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public ICollection<SaleItem> Items { get; set; } = [];
}
```

### `Src/POSI.Domain/Entities/SaleItem.cs`
```csharp
namespace POSI.Domain.Entities;

public class SaleItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid SaleId { get; set; }
    public Guid? ProductId { get; set; }   // nullable: producto puede ser eliminado después
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal Subtotal { get; set; }

    // Navigation
    public Sale Sale { get; set; } = null!;
    public Tenant Tenant { get; set; } = null!;
}
```

### `Src/POSI.Domain/Interfaces/ITenantService.cs`
```csharp
namespace POSI.Domain.Interfaces;

public interface ITenantService
{
    Guid? GetCurrentTenantId();
}
```

**NOTA:** `ApplicationUser` extiende `IdentityUser` de `Microsoft.AspNetCore.Identity`.
El proyecto `POSI.Domain` NO tiene esa dependencia en su .csproj actual.
Agregar al `POSI.Domain.csproj`:
```xml
<ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
</ItemGroup>
```

---

## Task 9.2 — AppDbContext (`POSI.Data`)

Eliminar `Src/POSI.Data/Class1.cs` y crear:

### `Src/POSI.Data/AppDbContext.cs`
```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using POSI.Domain.Entities;
using POSI.Domain.Interfaces;

namespace POSI.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly ITenantService _tenantService;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantService tenantService)
        : base(options)
    {
        _tenantService = tenantService;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    private Guid? CurrentTenantId => _tenantService.GetCurrentTenantId();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Renombrar tablas de Identity (snake_case)
        builder.Entity<ApplicationUser>().ToTable("users");
        builder.Entity<IdentityRole>().ToTable("roles");
        builder.Entity<IdentityUserRole<string>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<string>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<string>>().ToTable("user_logins");
        builder.Entity<IdentityRoleClaim<string>>().ToTable("role_claims");
        builder.Entity<IdentityUserToken<string>>().ToTable("user_tokens");

        // Tenant
        builder.Entity<Tenant>(e =>
        {
            e.ToTable("tenants");
            e.HasKey(t => t.Id);
            e.Property(t => t.Name).IsRequired().HasMaxLength(200);
            e.Property(t => t.Slug).IsRequired().HasMaxLength(100);
            e.HasIndex(t => t.Slug).IsUnique();
        });

        // ApplicationUser → Tenant
        builder.Entity<ApplicationUser>(e =>
        {
            e.HasOne(u => u.Tenant)
             .WithMany(t => t.Users)
             .HasForeignKey(u => u.TenantId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // RefreshToken
        builder.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(r => r.Id);
            e.HasOne(r => r.User)
             .WithMany(u => u.RefreshTokens)
             .HasForeignKey(r => r.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Product
        builder.Entity<Product>(e =>
        {
            e.ToTable("products");
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).IsRequired().HasMaxLength(200);
            e.Property(p => p.Price).HasPrecision(18, 2);
            e.Property(p => p.Cost).HasPrecision(18, 2);
            e.HasOne(p => p.Tenant)
             .WithMany(t => t.Products)
             .HasForeignKey(p => p.TenantId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.Category)
             .WithMany(c => c.Products)
             .HasForeignKey(p => p.CategoryId)
             .OnDelete(DeleteBehavior.SetNull);
            // Global filter multi-tenant
            e.HasQueryFilter(p => CurrentTenantId == null || p.TenantId == CurrentTenantId);
        });

        // Category
        builder.Entity<Category>(e =>
        {
            e.ToTable("categories");
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).IsRequired().HasMaxLength(100);
            e.HasOne(c => c.Tenant)
             .WithMany(t => t.Categories)
             .HasForeignKey(c => c.TenantId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(c => CurrentTenantId == null || c.TenantId == CurrentTenantId);
        });

        // Sale
        builder.Entity<Sale>(e =>
        {
            e.ToTable("sales");
            e.HasKey(s => s.Id);
            e.Property(s => s.SaleNumber).IsRequired().HasMaxLength(50);
            e.Property(s => s.Subtotal).HasPrecision(18, 2);
            e.Property(s => s.Tax).HasPrecision(18, 2);
            e.Property(s => s.Total).HasPrecision(18, 2);
            e.HasOne(s => s.Tenant)
             .WithMany(t => t.Sales)
             .HasForeignKey(s => s.TenantId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(s => s.Items)
             .WithOne(i => i.Sale)
             .HasForeignKey(i => i.SaleId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(s => CurrentTenantId == null || s.TenantId == CurrentTenantId);
        });

        // SaleItem
        builder.Entity<SaleItem>(e =>
        {
            e.ToTable("sale_items");
            e.HasKey(si => si.Id);
            e.Property(si => si.ProductName).IsRequired().HasMaxLength(200);
            e.Property(si => si.UnitPrice).HasPrecision(18, 2);
            e.Property(si => si.Subtotal).HasPrecision(18, 2);
            e.HasOne(si => si.Tenant)
             .WithMany()
             .HasForeignKey(si => si.TenantId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(si => CurrentTenantId == null || si.TenantId == CurrentTenantId);
        });
    }
}
```

### `Src/POSI.Data/AppDbContextFactory.cs`
Permite ejecutar migraciones desde CLI sin un servidor corriendo.

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using POSI.Domain.Interfaces;

namespace POSI.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=posi_db;Username=postgres;Password=postgres");

        return new AppDbContext(optionsBuilder.Options, new NullTenantService());
    }

    // Servicio nulo para design-time (migraciones no tienen contexto HTTP)
    private sealed class NullTenantService : ITenantService
    {
        public Guid? GetCurrentTenantId() => null;
    }
}
```

---

## Task 9.3 — TenantService (`POSI.Api`)

### `Src/POSI.Api/Services/TenantService.cs`
```csharp
using POSI.Domain.Interfaces;

namespace POSI.Api.Services;

public class TenantService : ITenantService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantService(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public Guid? GetCurrentTenantId()
    {
        var claim = _httpContextAccessor.HttpContext?.User?.FindFirst("tenantId");
        if (claim == null) return null;
        return Guid.TryParse(claim.Value, out var id) ? id : null;
    }
}
```

---

## Task 9.4 — Configuración: `appsettings.json` y `appsettings.Development.json`

### `Src/POSI.Api/appsettings.json`
Reemplazar contenido completo:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=posi_db;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "Key": "posi-super-secret-key-minimum-32-characters-long-2024",
    "Issuer": "POSI-API",
    "Audience": "POSI-App",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  }
}
```

### `Src/POSI.Api/appsettings.Development.json`
Reemplazar contenido completo:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

---

## Task 9.5 — `Program.cs` limpio con todos los servicios

Reemplazar `Src/POSI.Api/Program.cs` completo:

```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using POSI.Data;
using POSI.Domain.Entities;
using POSI.Domain.Interfaces;
using POSI.Api.Services;
using Serilog;

// ── Serilog ────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console());

// ── Database ───────────────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantService, TenantService>();

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// ── Identity ───────────────────────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// ── JWT Authentication ─────────────────────────────────────────────────────
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// ── Swagger / API Explorer ─────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "POSI API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Ingresa el token JWT: Bearer {token}"
    });
    c.AddSecurityRequirement(new()
    {
        {
            new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } },
            []
        }
    });
});

builder.Services.AddControllers();

var app = builder.Build();

// ── Middleware ─────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

---

## Task 9.6 — Generar migración inicial

Ejecutar en orden (desde la raíz del repo):

```bash
cd /Users/sebastian-buitrago/Documents/Yo/POSI/POSI-Backend
dotnet build POSI.sln
```

Si el build pasa sin errores, generar la migración:
```bash
dotnet ef migrations add InitialCreate \
  --project Src/POSI.Data/POSI.Data.csproj \
  --startup-project Src/POSI.Api/POSI.Api.csproj \
  --output-dir Migrations
```

**NOTA:** Si `dotnet ef` no está instalado, ejecutar primero:
```bash
dotnet tool install --global dotnet-ef --version 8.0.11
```

El comando de migración **no requiere PostgreSQL corriendo** — solo genera los archivos C# de migración.
Para aplicar la migración a la base de datos (requiere PostgreSQL):
```bash
dotnet ef database update \
  --project Src/POSI.Data/POSI.Data.csproj \
  --startup-project Src/POSI.Api/POSI.Api.csproj
```

---

## Estructura de archivos a crear/modificar

### Crear (nuevos):
```
Src/POSI.Domain/
  Entities/
    Tenant.cs
    ApplicationUser.cs
    RefreshToken.cs
    Product.cs
    Category.cs
    Sale.cs
    SaleItem.cs
  Interfaces/
    ITenantService.cs

Src/POSI.Data/
  AppDbContext.cs
  AppDbContextFactory.cs

Src/POSI.Api/
  Services/
    TenantService.cs
```

### Modificar (existentes):
```
Src/POSI.Domain/POSI.Domain.csproj   ← añadir FrameworkReference
Src/POSI.Data/POSI.Data.csproj       ← reemplazar con paquetes EF Core
Src/POSI.Api/POSI.Api.csproj         ← reemplazar con JwtBearer + EF Design
Src/POSI.Api/Program.cs              ← reemplazar por completo
Src/POSI.Api/appsettings.json        ← añadir ConnectionStrings + Jwt
Src/POSI.Api/appsettings.Development.json → reemplazar
```

### Eliminar:
```
Src/POSI.Domain/Class1.cs
Src/POSI.Data/Class1.cs
Src/POSI.Services/Class1.cs
```

## Orden de ejecución OBLIGATORIO

1. Task 9.0 — .csproj files + `dotnet restore`
2. Task 9.1 — Domain entities (POSI.Domain)
3. Task 9.2 — AppDbContext + Factory (POSI.Data)
4. Task 9.3 — TenantService (POSI.Api)
5. Task 9.4 — appsettings
6. Task 9.5 — Program.cs
7. Task 9.6 — `dotnet build` → migración

## Validación
```bash
dotnet build POSI.sln
```
Debe compilar con **0 errores**.

## IMPORTANTE — No hacer
- NO crear controllers ni endpoints (eso es Paso 10)
- NO crear servicios de negocio (eso es Paso 10)
- NO modificar la solución `.sln` ni agregar proyectos
- NO tocar `POSSI.Domain/` (carpeta con typo — ignorar)
- El JWT Key en appsettings.json es para desarrollo — en producción va en variables de entorno
