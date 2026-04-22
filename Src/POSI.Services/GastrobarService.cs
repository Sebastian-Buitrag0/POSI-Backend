using Microsoft.EntityFrameworkCore;
using POSI.Data;
using POSI.Domain.DTOs.Gastrobar;
using POSI.Domain.Entities;
using POSI.Domain.Interfaces;

namespace POSI.Services;

public class GastrobarService : IGastrobarService
{
    private readonly AppDbContext _db;
    private readonly ITenantService _tenantService;

    public GastrobarService(AppDbContext db, ITenantService tenantService)
    {
        _db = db;
        _tenantService = tenantService;
    }

    private Guid? TenantId => _tenantService.GetCurrentTenantId();

    private Guid RequireTenantId() => TenantId ?? throw new InvalidOperationException("Tenant no autenticado.");

    // Tables
    public async Task<List<TableDto>> GetTablesAsync()
    {
        var tenantId = RequireTenantId();

        var tables = await _db.Tables
            .Where(t => t.TenantId == tenantId && t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync();

        var result = new List<TableDto>();

        foreach (var table in tables)
        {
            var activeOrderItemCount = await _db.Orders
                .Where(o => o.TenantId == tenantId && o.TableId == table.Id && o.Status == "open")
                .SelectMany(o => o.Items)
                .CountAsync(i => i.Status == "pending" || i.Status == "sent");

            result.Add(new TableDto(
                table.Id,
                table.Name,
                table.Capacity,
                table.Status,
                table.IsActive,
                activeOrderItemCount));
        }

        return result;
    }

    public async Task<TableDto> CreateTableAsync(CreateTableDto dto)
    {
        var tenantId = RequireTenantId();

        var table = new Table
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = dto.Name,
            Capacity = dto.Capacity,
            Status = "available",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        _db.Tables.Add(table);
        await _db.SaveChangesAsync();

        return new TableDto(table.Id, table.Name, table.Capacity, table.Status, table.IsActive, 0);
    }

    public async Task<TableDto> UpdateTableStatusAsync(Guid tableId, UpdateTableStatusDto dto)
    {
        var tenantId = RequireTenantId();

        var table = await _db.Tables
            .FirstOrDefaultAsync(t => t.Id == tableId && t.TenantId == tenantId);

        if (table is null) throw new InvalidOperationException("Mesa no encontrada.");

        table.Status = dto.Status;
        await _db.SaveChangesAsync();

        var activeOrderItemCount = await _db.Orders
            .Where(o => o.TenantId == tenantId && o.TableId == table.Id && o.Status == "open")
            .SelectMany(o => o.Items)
            .CountAsync(i => i.Status == "pending" || i.Status == "sent");

        return new TableDto(table.Id, table.Name, table.Capacity, table.Status, table.IsActive, activeOrderItemCount);
    }

    public async Task DeleteTableAsync(Guid tableId)
    {
        var tenantId = RequireTenantId();

        var table = await _db.Tables
            .FirstOrDefaultAsync(t => t.Id == tableId && t.TenantId == tenantId);

        if (table is null) throw new InvalidOperationException("Mesa no encontrada.");

        table.IsActive = false;
        await _db.SaveChangesAsync();
    }

    // Orders
    public async Task<OrderDto> GetOrderAsync(Guid orderId)
    {
        var tenantId = RequireTenantId();

        var order = await _db.Orders
            .Include(o => o.Table)
            .Include(o => o.Waiter)
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId);

        if (order is null) throw new InvalidOperationException("Comanda no encontrada.");

        return MapToOrderDto(order);
    }

    public async Task<List<OrderDto>> GetActiveOrdersAsync()
    {
        var tenantId = RequireTenantId();

        var orders = await _db.Orders
            .Include(o => o.Table)
            .Include(o => o.Waiter)
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Where(o => o.TenantId == tenantId && o.Status == "open")
            .OrderByDescending(o => o.OpenedAt)
            .ToListAsync();

        return orders.Select(MapToOrderDto).ToList();
    }

    public async Task<OrderDto> OpenOrderAsync(Guid tableId)
    {
        var tenantId = RequireTenantId();

        var table = await _db.Tables
            .FirstOrDefaultAsync(t => t.Id == tableId && t.TenantId == tenantId);

        if (table is null) throw new InvalidOperationException("Mesa no encontrada.");

        var existingOpenOrder = await _db.Orders
            .AnyAsync(o => o.TenantId == tenantId && o.TableId == tableId && o.Status == "open");

        if (existingOpenOrder)
            throw new InvalidOperationException("La mesa ya tiene una comanda abierta.");

        var orderNumber = await GenerateOrderNumberAsync(tenantId);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TableId = tableId,
            OrderNumber = orderNumber,
            Status = "open",
            OpenedAt = DateTime.UtcNow,
        };

        table.Status = "occupied";

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        return MapToOrderDto(order);
    }

    public async Task<OrderDto> AddItemsAsync(Guid orderId, AddOrderItemsDto dto)
    {
        var tenantId = RequireTenantId();

        var order = await _db.Orders
            .Include(o => o.Table)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId);

        if (order is null) throw new InvalidOperationException("Comanda no encontrada.");
        if (order.Status != "open")
            throw new InvalidOperationException("Solo se pueden agregar items a comandas abiertas.");

        foreach (var itemDto in dto.Items)
        {
            var product = await _db.Products
                .FirstOrDefaultAsync(p => p.Id == itemDto.ProductId && p.TenantId == tenantId);

            if (product is null)
                throw new InvalidOperationException($"Producto {itemDto.ProductId} no encontrado.");

            var unitPrice = product.Price;
            var quantity = itemDto.Quantity;
            var subtotal = unitPrice * quantity;

            var orderItem = new OrderItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                OrderId = orderId,
                ProductId = itemDto.ProductId,
                ProductName = product.Name,
                UnitPrice = unitPrice,
                Quantity = quantity,
                Subtotal = subtotal,
                Status = "pending",
                Notes = itemDto.Notes,
                CreatedAt = DateTime.UtcNow,
            };

            order.Items.Add(orderItem);
        }

        await _db.SaveChangesAsync();

        // Reload with all related data for mapping
        var refreshedOrder = await _db.Orders
            .Include(o => o.Table)
            .Include(o => o.Waiter)
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .FirstAsync(o => o.Id == orderId);

        return MapToOrderDto(refreshedOrder);
    }

    public async Task<OrderItemDto> UpdateItemStatusAsync(Guid itemId, UpdateOrderItemStatusDto dto)
    {
        var tenantId = RequireTenantId();

        var item = await _db.OrderItems
            .Include(i => i.Order)
            .Include(i => i.Product)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.TenantId == tenantId);

        if (item is null) throw new InvalidOperationException("Item no encontrado.");

        var current = item.Status;
        var next = dto.Status;

        var validTransitions = new Dictionary<string, List<string>>
        {
            ["pending"] = ["sent"],
            ["sent"] = ["delivered"],
        };

        if (!validTransitions.TryGetValue(current, out var allowed) || !allowed.Contains(next))
            throw new InvalidOperationException($"Transición de estado inválida: {current} → {next}");

        item.Status = next;
        await _db.SaveChangesAsync();

        return new OrderItemDto(
            item.Id,
            item.ProductId,
            item.ProductName,
            item.UnitPrice,
            item.Quantity,
            item.Subtotal,
            item.Status,
            item.Notes);
    }

    public async Task<Guid> CloseOrderAsync(Guid orderId, CloseOrderDto dto)
    {
        var tenantId = RequireTenantId();

        var order = await _db.Orders
            .Include(o => o.Table)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId);

        if (order is null) throw new InvalidOperationException("Comanda no encontrada.");
        if (order.Status != "open")
            throw new InvalidOperationException("Solo se pueden cerrar comandas abiertas.");

        var nonCancelledItems = order.Items.Where(i => i.Status != "cancelled").ToList();
        var subtotal = nonCancelledItems.Sum(i => i.Subtotal);
        var tax = 0m; // no tax logic specified
        var total = subtotal + tax;

        var saleNumber = await GenerateSaleNumberAsync(tenantId);

        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SaleNumber = saleNumber,
            Subtotal = subtotal,
            Tax = tax,
            Total = total,
            PaymentMethod = dto.PaymentMethod,
            Status = "completed",
            Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow,
        };

        foreach (var item in nonCancelledItems)
        {
            sale.Items.Add(new SaleItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SaleId = sale.Id,
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                UnitPrice = item.UnitPrice,
                Quantity = item.Quantity,
                Subtotal = item.Subtotal,
            });
        }

        _db.Sales.Add(sale);

        order.SaleId = sale.Id;
        order.Status = "closed";
        order.ClosedAt = DateTime.UtcNow;
        order.Table.Status = "available";

        await _db.SaveChangesAsync();

        return sale.Id;
    }

    public async Task CancelOrderAsync(Guid orderId)
    {
        var tenantId = RequireTenantId();

        var order = await _db.Orders
            .Include(o => o.Table)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId);

        if (order is null) throw new InvalidOperationException("Comanda no encontrada.");
        if (order.Status != "open")
            throw new InvalidOperationException("Solo se pueden cancelar comandas abiertas.");

        var hasDelivered = order.Items.Any(i => i.Status == "delivered");
        if (hasDelivered)
            throw new InvalidOperationException("No se puede cancelar una comanda con items entregados.");

        order.Status = "cancelled";
        order.Table.Status = "available";
        order.ClosedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    // Helpers
    private static OrderDto MapToOrderDto(Order order)
    {
        var items = order.Items.Select(i => new OrderItemDto(
            i.Id,
            i.ProductId,
            i.ProductName,
            i.UnitPrice,
            i.Quantity,
            i.Subtotal,
            i.Status,
            i.Notes)).ToList();

        var total = items.Where(i => i.Status != "cancelled").Sum(i => i.Subtotal);

        return new OrderDto(
            order.Id,
            order.OrderNumber,
            order.TableId,
            order.Table.Name,
            order.Status,
            order.Waiter != null ? $"{order.Waiter.FirstName} {order.Waiter.LastName}" : null,
            order.OpenedAt,
            items,
            total);
    }

    private async Task<string> GenerateOrderNumberAsync(Guid tenantId)
    {
        var today = DateTime.UtcNow;
        var prefix = $"CMD-{today:yyyyMMdd}-";

        var countToday = await _db.Orders
            .CountAsync(o => o.TenantId == tenantId && o.OrderNumber.StartsWith(prefix));

        return $"{prefix}{countToday + 1:D4}";
    }

    private async Task<string> GenerateSaleNumberAsync(Guid tenantId)
    {
        var today = DateTime.UtcNow;
        var prefix = $"V-{today:yyyyMMdd}-";

        var countToday = await _db.Sales
            .CountAsync(s => s.TenantId == tenantId && s.SaleNumber.StartsWith(prefix));

        return $"{prefix}{countToday + 1:D4}";
    }
}
