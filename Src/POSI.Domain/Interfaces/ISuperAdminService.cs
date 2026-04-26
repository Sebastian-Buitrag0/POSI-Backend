using POSI.Domain.DTOs.SuperAdmin;

namespace POSI.Domain.Interfaces;

/// <summary>
/// Define operaciones de super administración del SAAS.
/// </summary>
public interface ISuperAdminService
{
    /// <summary>
    /// Autentica a un super admin y genera tokens.
    /// </summary>
    Task<SuperAdminLoginResponseDto?> LoginAsync(SuperAdminLoginRequestDto dto);

    /// <summary>
    /// Obtiene estadísticas globales del SAAS.
    /// </summary>
    Task<SuperAdminDashboardDto> GetDashboardAsync();

    /// <summary>
    /// Obtiene todos los tenants del sistema.
    /// </summary>
    Task<List<TenantSummaryDto>> GetAllTenantsAsync();

    /// <summary>
    /// Obtiene el detalle de un tenant específico.
    /// </summary>
    Task<TenantDetailDto?> GetTenantByIdAsync(Guid tenantId);

    /// <summary>
    /// Actualiza un tenant (plan, estado activo).
    /// </summary>
    Task<bool> UpdateTenantAsync(Guid tenantId, UpdateTenantDto dto);

    /// <summary>
    /// Elimina un tenant y todos sus datos asociados.
    /// </summary>
    Task<bool> DeleteTenantAsync(Guid tenantId);

    /// <summary>
    /// Obtiene todos los usuarios de todos los tenants.
    /// </summary>
    Task<List<GlobalUserDto>> GetAllUsersAsync(int page, int pageSize);

    /// <summary>
    /// Elimina un usuario globalmente.
    /// </summary>
    Task<bool> DeleteUserAsync(string userId);

    /// <summary>
    /// Obtiene estadísticas de un tenant específico.
    /// </summary>
    Task<TenantStatsDto?> GetTenantStatsAsync(Guid tenantId);

    /// <summary>
    /// Crea un nuevo tenant con su admin desde el panel super admin.
    /// </summary>
    Task<TenantSummaryDto> CreateTenantAsync(CreateTenantDto dto);
}
