namespace POSI.Domain.Constants;

/// <summary>
/// Define los posibles estados de un item de orden.
/// </summary>
public static class OrderItemStatuses
{
    public const string Pending = "Pendiente";
    public const string InProgress = "En preparación";
    public const string Ready = "Lista";
    public const string Delivered = "Entregada";
    public const string Cancelled = "Cancelada";
}
