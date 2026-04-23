using POSI.Domain.DTOs.Stats;

namespace POSI.Domain.Interfaces;

/// <summary>
/// Define operaciones de consulta de estadísticas y datos de panel de control.
/// </summary>
public interface IStatsService
{
    /// <summary>
    /// Obtiene estadísticas agregadas incluyendo ingresos, cantidad de ventas y productos principales.
    /// </summary>
    Task<StatsDto> GetAsync(string period);
}
