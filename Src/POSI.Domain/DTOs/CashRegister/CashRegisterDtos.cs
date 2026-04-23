using System.ComponentModel.DataAnnotations;

namespace POSI.Domain.DTOs.CashRegister;

/// <summary>
/// Solicitud para abrir una sesión de caja registradora.
/// </summary>
public record OpenCashRegisterDto(
    [property: Range(0, double.MaxValue, ErrorMessage = "El efectivo de apertura no puede ser negativo.")]
    decimal OpeningCash,
    string? Notes
);

/// <summary>
/// Solicitud para cerrar una sesión de caja registradora.
/// </summary>
public record CloseCashRegisterDto(
    [property: Range(0, double.MaxValue, ErrorMessage = "El efectivo real no puede ser negativo.")]
    decimal ActualCash,
    string? Notes
);

/// <summary>
/// Objeto de transferencia de datos de sesión de caja registradora.
/// </summary>
public record CashRegisterSessionDto(
    Guid Id,
    decimal OpeningCash,
    decimal? ClosingCash,
    decimal? ActualCash,
    string Status,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    string? Notes
);
