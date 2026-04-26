namespace POSI.Domain.DTOs.SuperAdmin;

/// <summary>
/// Estadísticas globales del SAAS para el dashboard de super admin.
/// </summary>
public record SuperAdminDashboardDto(
    int TotalTenants,
    int ActiveTenants,
    int TotalUsers,
    int TotalSales,
    decimal TotalRevenue,
    int TenantsCreatedThisMonth,
    int UsersCreatedThisMonth,
    decimal RevenueThisMonth,
    IReadOnlyList<TenantSummaryDto> RecentTenants,
    IReadOnlyList<DailyGlobalSalesDto> SalesByDay
);

/// <summary>
/// Resumen de un tenant para listados.
/// </summary>
public record TenantSummaryDto(
    Guid Id,
    string Name,
    string Slug,
    string Plan,
    bool IsActive,
    int UserCount,
    int ProductCount,
    int SaleCount,
    DateTime CreatedAt
);

/// <summary>
/// Detalle completo de un tenant.
/// </summary>
public record TenantDetailDto(
    Guid Id,
    string Name,
    string Slug,
    string Plan,
    bool IsActive,
    DateTime CreatedAt,
    int UserCount,
    int ProductCount,
    int CategoryCount,
    int SaleCount,
    decimal TotalRevenue,
    IReadOnlyList<TenantUserSummaryDto> Users
);

/// <summary>
/// Usuario resumido dentro de un tenant.
/// </summary>
public record TenantUserSummaryDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    DateTime CreatedAt
);

/// <summary>
/// Usuario global del sistema para super admin.
/// </summary>
public record GlobalUserDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    Guid TenantId,
    string TenantName,
    DateTime CreatedAt
);

/// <summary>
/// Solicitud para actualizar un tenant desde super admin.
/// </summary>
public record UpdateTenantDto(
    string? Plan,
    bool? IsActive
);

/// <summary>
/// Solicitud para crear un tenant desde super admin.
/// </summary>
public record CreateTenantDto(
    string Name,
    string AdminEmail,
    string AdminFirstName,
    string AdminLastName,
    string AdminPassword
);

/// <summary>
/// Ventas diarias agregadas globalmente.
/// </summary>
public record DailyGlobalSalesDto(
    string Date,
    decimal Revenue,
    int Count
);

/// <summary>
/// Estadísticas de un tenant específico.
/// </summary>
public record TenantStatsDto(
    Guid TenantId,
    string TenantName,
    int TotalSales,
    decimal TotalRevenue,
    decimal AverageTicket,
    int TotalProducts,
    int TotalCategories,
    int TotalUsers,
    IReadOnlyList<DailyGlobalSalesDto> SalesByDay,
    IReadOnlyList<TopProductGlobalDto> TopProducts
);

/// <summary>
/// Producto más vendido a nivel global o por tenant.
/// </summary>
public record TopProductGlobalDto(
    string Name,
    int Quantity,
    decimal Revenue
);

/// <summary>
/// Solicitud de login para super admin.
/// </summary>
public record SuperAdminLoginRequestDto(
    string Email,
    string Password
);

/// <summary>
/// Respuesta de login para super admin.
/// </summary>
public record SuperAdminLoginResponseDto(
    string AccessToken,
    string RefreshToken,
    string Email,
    string FirstName,
    string LastName
);
