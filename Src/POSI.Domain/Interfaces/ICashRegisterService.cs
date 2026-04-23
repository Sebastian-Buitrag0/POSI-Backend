using POSI.Domain.DTOs.CashRegister;

namespace POSI.Domain.Interfaces;

/// <summary>
/// Define operaciones de gestión de sesiones de caja registradora.
/// </summary>
public interface ICashRegisterService
{
    /// <summary>
    /// Obtiene la sesión de caja abierta actual, o null si no hay sesión abierta.
    /// </summary>
    Task<CashRegisterSessionDto?> GetCurrentAsync();

    /// <summary>
    /// Abre una nueva sesión de caja registradora.
    /// </summary>
    Task<CashRegisterSessionDto> OpenAsync(OpenCashRegisterDto dto, string userId);

    /// <summary>
    /// Cierra la sesión de caja abierta actual.
    /// </summary>
    Task<CashRegisterSessionDto?> CloseAsync(CloseCashRegisterDto dto);
}
