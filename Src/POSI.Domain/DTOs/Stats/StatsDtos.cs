namespace POSI.Domain.DTOs.Stats;

public record StatsDto(
    string Period,
    int TotalSales,
    decimal TotalRevenue,
    decimal AverageTicket,
    double RevenueChange,       // % vs periodo anterior
    double SalesCountChange,    // % vs periodo anterior
    IReadOnlyList<DailySalesDto> SalesByDay,
    IReadOnlyList<PaymentMethodDto> SalesByPaymentMethod,
    IReadOnlyList<TopProductDto> TopProducts,
    IReadOnlyList<LowStockDto> LowStockProducts
);

public record DailySalesDto(
    string Date,   // "dd/MM"
    decimal Revenue,
    int Count
);

public record PaymentMethodDto(
    string Method,
    decimal Revenue,
    int Count
);

public record TopProductDto(
    string Name,
    decimal Quantity,
    decimal Revenue
);

public record LowStockDto(
    string Id,
    string Name,
    int Stock,
    int MinStock
);
