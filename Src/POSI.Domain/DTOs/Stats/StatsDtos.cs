namespace POSI.Domain.DTOs.Stats;

/// <summary>
/// Estadísticas agregadas para un período de tiempo dado.
/// </summary>
public record StatsDto(
    string Period,
    int TotalSales,
    decimal TotalRevenue,
    decimal AverageTicket,
    double RevenueChange,
    double SalesCountChange,
    IReadOnlyList<DailySalesDto> SalesByDay,
    IReadOnlyList<PaymentMethodDto> SalesByPaymentMethod,
    IReadOnlyList<TopProductDto> TopProducts,
    IReadOnlyList<LowStockDto> LowStockProducts
);

/// <summary>
/// Resumen de ventas diarias.
/// </summary>
public record DailySalesDto(
    string Date,
    decimal Revenue,
    int Count
);

/// <summary>
/// Resumen de ventas para un método de pago.
/// </summary>
public record PaymentMethodDto(
    string Method,
    decimal Revenue,
    int Count
);

/// <summary>
/// Producto más vendido.
/// </summary>
public record TopProductDto(
    string Name,
    int Quantity,
    decimal Revenue
);

/// <summary>
/// Producto con stock bajo.
/// </summary>
public record LowStockDto(
    string Id,
    string Name,
    int Stock,
    int MinStock
);
