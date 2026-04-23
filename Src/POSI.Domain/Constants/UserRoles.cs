namespace POSI.Domain.Constants;

/// <summary>
/// Centraliza los roles de la aplicación y proporciona método de validación.
/// </summary>
public static class UserRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Cashier = "Cashier";
    public const string Mesero = "Mesero";
    public const string Default = Cashier;

    public static readonly IReadOnlyList<string> All = [Admin, Manager, Cashier, Mesero];

    public static bool IsValid(string role) => All.Contains(role);
}
