using POSI.Domain.DTOs.Sync;

namespace POSI.Domain.Interfaces;

/// <summary>
/// Define operaciones de sincronización de datos para clientes offline.
/// </summary>
public interface ISyncService
{
    /// <summary>
    /// Sincroniza un lote de productos desde un dispositivo local.
    /// </summary>
    Task<SyncProductsResponseDto> SyncProductsAsync(SyncProductsRequestDto request);

    /// <summary>
    /// Sincroniza un lote de ventas desde un dispositivo local.
    /// </summary>
    Task<SyncSalesResponseDto> SyncSalesAsync(SyncSalesRequestDto request);
}
