namespace POSI.Domain.Constants;

/// <summary>
/// Define los posibles estados de una orden.
/// </summary>
public static class OrderStatuses
{
    public const string Pending = "Pendiente";
    public const string InProgress = "En preparación";
    public const string Ready = "Lista";
    public const string Delivered = "Entregada";
}
