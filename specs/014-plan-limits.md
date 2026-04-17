# Spec 014 — Planes + Límites (Backend)

## Objetivo
Enforcer límites por plan antes de crear productos.
- `free`: 50 productos máximo
- `pro`: 500 productos máximo
- `business`: ilimitado

## Infraestructura existente
- `Tenant.Plan` — string, default `"free"`
- `AppDbContext` con global query filter por TenantId
- `ProductsController.Create` — `POST /api/products`
- `SyncController.SyncProducts` — `POST /api/products/sync` (upsert batch)
- `ITenantService.GetCurrentTenantId()`

## Regla de negocio
Si el tenant alcanza el límite de productos → responder `402 Payment Required`
con `{ message: "Has alcanzado el límite de tu plan. Actualiza a Pro para continuar." }`

---

## Task 14.0 — PlanLimits

### `Src/POSI.Domain/Settings/PlanLimits.cs`
```csharp
namespace POSI.Domain.Settings;

public static class PlanLimits
{
    public const int Unlimited = -1;

    private static readonly Dictionary<string, int> ProductLimits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["free"]     = 50,
        ["pro"]      = 500,
        ["business"] = Unlimited,
    };

    public static int GetProductLimit(string plan) =>
        ProductLimits.TryGetValue(plan, out var limit) ? limit : 50; // default free

    public static bool IsWithinProductLimit(string plan, int currentCount) =>
        GetProductLimit(plan) == Unlimited || currentCount < GetProductLimit(plan);
}
```

---

## Task 14.1 — Enforce en ProductsController

En `Src/POSI.Api/Controllers/ProductsController.cs`, modificar el endpoint `Create`:

Después de `var tenantId = _tenantService.GetCurrentTenantId(); if (tenantId is null) return Unauthorized();`, agregar:

```csharp
// Enforce plan limit
var tenant = await _db.Tenants.FindAsync(tenantId.Value);
if (tenant is not null)
{
    var productCount = await _db.Products.CountAsync(p => p.TenantId == tenantId.Value);
    if (!PlanLimits.IsWithinProductLimit(tenant.Plan, productCount))
        return StatusCode(402, new { message = "Has alcanzado el límite de productos de tu plan. Actualiza a Pro para continuar." });
}
```

Agregar using:
```csharp
using POSI.Domain.Settings;
```

---

## Task 14.2 — Enforce en SyncController (productos nuevos)

En `Src/POSI.Api/Controllers/SyncController.cs`, en el método `SyncProducts`,
dentro del `foreach` antes de crear un producto nuevo (`if (existing is null)`):

```csharp
if (existing is null)
{
    // Check plan limit before inserting
    var tenant = await _db.Tenants.FindAsync(tenantId.Value);
    if (tenant is not null)
    {
        var productCount = await _db.Products.CountAsync(p => p.TenantId == tenantId.Value);
        if (!PlanLimits.IsWithinProductLimit(tenant.Plan, productCount))
        {
            // Skip this product — no agregar al mapping
            continue;
        }
    }

    var product = new Product { ... }; // el código existente sigue igual
```

Agregar using:
```csharp
using POSI.Domain.Settings;
```

---

## Task 14.3 — Validación

```bash
cd /Users/sebastian-buitrago/Documents/Yo/POSI/POSI-Backend
dotnet build POSI.sln
```

**0 errores, 0 warnings.**

---

## Archivos a crear
```
Src/POSI.Domain/Settings/PlanLimits.cs   ← Task 14.0
```

## Archivos a modificar
```
Src/POSI.Api/Controllers/ProductsController.cs  ← Task 14.1
Src/POSI.Api/Controllers/SyncController.cs      ← Task 14.2
```

## IMPORTANTE — No hacer
- NO crear migraciones — Plan ya existe como columna string en Tenant
- NO modificar entidades ni AppDbContext
- NO agregar límites a ventas — solo productos por ahora
- En SyncController: si el límite se alcanza, el producto se salta silenciosamente
  (no falla el batch entero, simplemente no se sincroniza ese producto)
- El límite de usuarios se enforceará cuando implementemos multi-usuario (Paso 15)
