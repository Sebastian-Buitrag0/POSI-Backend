# [016] Módulo Gastrobar — Backend (.NET 8)

## Objetivo
Implementar el módulo de mesas y comandas (órdenes) para negocios tipo gastrobar/restaurante.
Una mesa puede tener múltiples rondas de pedidos. Al cerrar la cuenta, la comanda se convierte
en una Sale existente.

## Contexto de la arquitectura existente
- Clean architecture: Domain → Data → Services → Api
- Multi-tenant con QueryFilters en AppDbContext (todos los queries se filtran por TenantId automáticamente)
- EF Core + PostgreSQL, migraciones en POSI.Data/Migrations/
- JWT auth con ITenantService que expone GetCurrentTenantId()
- Patrón existente: entidad en Domain/Entities, DbSet en AppDbContext, controller en Api/Controllers

## Archivos a CREAR

### POSI.Domain/Entities/Table.cs
```csharp
namespace POSI.Domain.Entities;

public class Table
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;   // "Mesa 1", "Barra 2"
    public int Capacity { get; set; }
    public string Status { get; set; } = "available";  // available | occupied | reserved
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public ICollection<Order> Orders { get; set; } = [];
}
```

### POSI.Domain/Entities/Order.cs
```csharp
namespace POSI.Domain.Entities;

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid TableId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "open";  // open | closed | cancelled
    public string? WaiterId { get; set; }          // ApplicationUser.Id
    public string? Notes { get; set; }
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    public Guid? SaleId { get; set; }              // null hasta que se cierra la cuenta

    public Tenant Tenant { get; set; } = null!;
    public Table Table { get; set; } = null!;
    public ApplicationUser? Waiter { get; set; }
    public Sale? Sale { get; set; }
    public ICollection<OrderItem> Items { get; set; } = [];
}
```

### POSI.Domain/Entities/OrderItem.cs
```csharp
namespace POSI.Domain.Entities;

public class OrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal Subtotal { get; set; }
    public string Status { get; set; } = "pending"; // pending | sent | delivered | cancelled
    public string? Notes { get; set; }              // "sin cebolla"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public Order Order { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
```

### POSI.Domain/DTOs/Gastrobar/TableDto.cs
```csharp
namespace POSI.Domain.DTOs.Gastrobar;

public record TableDto(
    Guid Id,
    string Name,
    int Capacity,
    string Status,
    bool IsActive,
    int ActiveOrderItemCount
);

public record CreateTableDto(string Name, int Capacity);
public record UpdateTableStatusDto(string Status);
```

### POSI.Domain/DTOs/Gastrobar/OrderDto.cs
```csharp
namespace POSI.Domain.DTOs.Gastrobar;

public record OrderDto(
    Guid Id,
    string OrderNumber,
    Guid TableId,
    string TableName,
    string Status,
    string? WaiterName,
    DateTime OpenedAt,
    List<OrderItemDto> Items,
    decimal Total
);

public record OrderItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal Subtotal,
    string Status,
    string? Notes
);

public record AddOrderItemsDto(List<NewOrderItemDto> Items);
public record NewOrderItemDto(Guid ProductId, int Quantity, string? Notes);
public record UpdateOrderItemStatusDto(string Status);
public record CloseOrderDto(string PaymentMethod, string? Notes);
```

### POSI.Domain/Interfaces/IGastrobarService.cs
```csharp
using POSI.Domain.DTOs.Gastrobar;

namespace POSI.Domain.Interfaces;

public interface IGastrobarService
{
    // Tables
    Task<List<TableDto>> GetTablesAsync();
    Task<TableDto> CreateTableAsync(CreateTableDto dto);
    Task<TableDto> UpdateTableStatusAsync(Guid tableId, UpdateTableStatusDto dto);
    Task DeleteTableAsync(Guid tableId);

    // Orders
    Task<OrderDto> GetOrderAsync(Guid orderId);
    Task<List<OrderDto>> GetActiveOrdersAsync();
    Task<OrderDto> OpenOrderAsync(Guid tableId);
    Task<OrderDto> AddItemsAsync(Guid orderId, AddOrderItemsDto dto);
    Task<OrderItemDto> UpdateItemStatusAsync(Guid itemId, UpdateOrderItemStatusDto dto);
    Task<Guid> CloseOrderAsync(Guid orderId, CloseOrderDto dto); // returns SaleId
    Task CancelOrderAsync(Guid orderId);
}
```

### POSI.Services/GastrobarService.cs
Implementar `IGastrobarService` con las siguientes reglas:

- `GetTablesAsync`: retorna todas las mesas activas del tenant, con `ActiveOrderItemCount` = items en estado pending/sent de la orden abierta de esa mesa.
- `OpenOrderAsync`: verifica que la mesa no tenga ya una orden `open`. Genera `OrderNumber` = "CMD-{año}{mes}{día}-{secuencia 4 dígitos}". Cambia `Table.Status` a "occupied".
- `AddItemsAsync`: agrega items a una orden `open`. Para cada item: resuelve el producto por ID, copia `ProductName` y `UnitPrice` del producto. Calcula `Subtotal = UnitPrice * Quantity`.
- `UpdateItemStatusAsync`: solo puede mover: pending → sent → delivered. No puede retroceder estados.
- `CloseOrderAsync`: 
  1. Calcula totales de todos los items no cancelados
  2. Genera una `Sale` con sus `SaleItems` (igual al patrón existente en SalesController)
  3. `SaleNumber` = "V-{año}{mes}{día}-{secuencia}" (mismo patrón que ventas normales)
  4. Asigna `Order.SaleId = sale.Id`, `Order.Status = "closed"`, `Order.ClosedAt = UtcNow`
  5. Cambia `Table.Status = "available"`
  6. Retorna el `SaleId`
- `CancelOrderAsync`: cambia `Order.Status = "cancelled"`, `Table.Status = "available"`. Solo si no hay items `delivered`.

### POSI.Api/Controllers/GastrobarController.cs
```
[Authorize]
[Route("api/gastrobar")]

GET    /tables                    → GetTablesAsync()
POST   /tables                    → CreateTableAsync()
PATCH  /tables/{id}/status        → UpdateTableStatusAsync()
DELETE /tables/{id}               → DeleteTableAsync()

GET    /orders                    → GetActiveOrdersAsync()
GET    /orders/{id}               → GetOrderAsync()
POST   /tables/{tableId}/orders   → OpenOrderAsync()
POST   /orders/{id}/items         → AddItemsAsync()
PATCH  /orders/{id}/items/{itemId}/status → UpdateItemStatusAsync()
POST   /orders/{id}/close         → CloseOrderAsync()
POST   /orders/{id}/cancel        → CancelOrderAsync()
```

## Archivos a MODIFICAR

### POSI.Data/AppDbContext.cs
Agregar DbSets:
```csharp
public DbSet<Table> Tables => Set<Table>();
public DbSet<Order> Orders => Set<Order>();
public DbSet<OrderItem> OrderItems => Set<OrderItem>();
```

Agregar en `OnModelCreating`:
```csharp
builder.Entity<Table>(e =>
{
    e.ToTable("tables");
    e.HasKey(t => t.Id);
    e.Property(t => t.Name).IsRequired().HasMaxLength(100);
    e.Property(t => t.Status).IsRequired().HasMaxLength(20);
    e.HasOne(t => t.Tenant).WithMany().HasForeignKey(t => t.TenantId).OnDelete(DeleteBehavior.Cascade);
    e.HasQueryFilter(t => CurrentTenantId == null || t.TenantId == CurrentTenantId);
});

builder.Entity<Order>(e =>
{
    e.ToTable("orders");
    e.HasKey(o => o.Id);
    e.Property(o => o.OrderNumber).IsRequired().HasMaxLength(50);
    e.Property(o => o.Status).IsRequired().HasMaxLength(20);
    e.HasOne(o => o.Tenant).WithMany().HasForeignKey(o => o.TenantId).OnDelete(DeleteBehavior.Cascade);
    e.HasOne(o => o.Table).WithMany(t => t.Orders).HasForeignKey(o => o.TableId).OnDelete(DeleteBehavior.Restrict);
    e.HasOne(o => o.Waiter).WithMany().HasForeignKey(o => o.WaiterId).OnDelete(DeleteBehavior.SetNull);
    e.HasOne(o => o.Sale).WithMany().HasForeignKey(o => o.SaleId).OnDelete(DeleteBehavior.SetNull);
    e.HasMany(o => o.Items).WithOne(i => i.Order).HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
    e.HasQueryFilter(o => CurrentTenantId == null || o.TenantId == CurrentTenantId);
});

builder.Entity<OrderItem>(e =>
{
    e.ToTable("order_items");
    e.HasKey(i => i.Id);
    e.Property(i => i.ProductName).IsRequired().HasMaxLength(200);
    e.Property(i => i.UnitPrice).HasPrecision(18, 2);
    e.Property(i => i.Subtotal).HasPrecision(18, 2);
    e.Property(i => i.Status).IsRequired().HasMaxLength(20);
    e.HasOne(i => i.Tenant).WithMany().HasForeignKey(i => i.TenantId).OnDelete(DeleteBehavior.Cascade);
    e.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
    e.HasQueryFilter(i => CurrentTenantId == null || i.TenantId == CurrentTenantId);
});
```

### POSI.Api/Program.cs
Registrar el servicio:
```csharp
builder.Services.AddScoped<IGastrobarService, GastrobarService>();
```

### POSI.Domain/Entities/Tenant.cs
Agregar colecciones:
```csharp
public ICollection<Table> Tables { get; set; } = [];
public ICollection<Order> Orders { get; set; } = [];
```

## Migración
Después de implementar todo, crear la migración:
```bash
cd Src/POSI.Data
dotnet ef migrations add AddGastrobarModule --startup-project ../POSI.Api
```

## Restricciones
- NO modificar la lógica de Sale/SaleItem existente excepto en CloseOrderAsync donde se crea una Sale nueva
- Seguir exactamente el patrón multi-tenant con QueryFilters (igual que Products, Sales)
- TenantId debe setearse siempre desde `_tenantService.GetCurrentTenantId()` en el service, no en el controller
- Usar el mismo patrón de inyección de dependencias que AuthService

## Definición de hecho
- [ ] `dotnet build POSI.sln` sin errores
- [ ] Migración creada correctamente
- [ ] Todos los endpoints en GastrobarController con sus rutas
- [ ] IGastrobarService implementado completamente en GastrobarService
- [ ] Servicio registrado en Program.cs
