namespace POSI.Domain.Interfaces;

/// <summary>
/// Define operaciones de gestión de ventas.
/// </summary>
public interface ISalesService
{
    /// <summary>
    /// Anula una venta completada y restaura el stock del producto.
    /// </summary>
    /// <returns><c>true</c> si se anuló; <c>false</c> si no se encontró o no se puede anular.</returns>
    Task<bool> VoidAsync(Guid saleId);
}
