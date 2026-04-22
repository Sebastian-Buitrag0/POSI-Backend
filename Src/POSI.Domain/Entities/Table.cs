namespace POSI.Domain.Entities;

public class Table
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string Status { get; set; } = "available";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public ICollection<Order> Orders { get; set; } = [];
}
