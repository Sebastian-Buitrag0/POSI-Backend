namespace POSI.Domain.Entities;

public class CashRegisterSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string OpenedByUserId { get; set; } = string.Empty;
    public decimal OpeningCash { get; set; }
    public decimal? ClosingCash { get; set; }
    public decimal? ActualCash { get; set; }
    public string Status { get; set; } = "open"; // open | closed
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    public string? Notes { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public ApplicationUser OpenedBy { get; set; } = null!;
}
