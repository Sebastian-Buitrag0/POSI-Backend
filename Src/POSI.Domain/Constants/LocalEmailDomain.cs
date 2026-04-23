namespace POSI.Domain.Constants;

/// <summary>
/// Define el sufijo de dominio usado para cuentas de usuario locales.
/// </summary>
public static class LocalEmailDomain
{
    public const string Suffix = "@local.posi";

    /// <summary>
    /// Determina si el correo especificado pertenece a una cuenta local.
    /// </summary>
    public static bool IsLocal(string email) => email.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase);
}
