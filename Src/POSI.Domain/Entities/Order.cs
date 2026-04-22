namespace POSI.Domain.Entities;

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid TableId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "open";
    public string? WaiterId { get; set; }
    public string? Notes { get; set; }
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    public Guid? SaleId { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Table Table { get; set; } = null!;
    public ApplicationUser? Waiter { get; set; }
    public Sale? Sale { get; set; }
    public ICollection<OrderItem> Items { get; set; } = [];
}
