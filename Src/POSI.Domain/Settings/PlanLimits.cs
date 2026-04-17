namespace POSI.Domain.Settings;

public static class PlanLimits
{
    public const int Unlimited = -1;

    private static readonly Dictionary<string, int> ProductLimits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["free"]     = 50,
        ["pro"]      = 500,
        ["business"] = Unlimited,
    };

    public static int GetProductLimit(string plan) =>
        ProductLimits.TryGetValue(plan, out var limit) ? limit : 50;

    public static bool IsWithinProductLimit(string plan, int currentCount) =>
        GetProductLimit(plan) == Unlimited || currentCount < GetProductLimit(plan);

    private static readonly Dictionary<string, int> UserLimits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["free"]     = 1,
        ["pro"]      = 5,
        ["business"] = Unlimited,
    };

    public static int GetUserLimit(string plan) =>
        UserLimits.TryGetValue(plan, out var limit) ? limit : 1;

    public static bool IsWithinUserLimit(string plan, int currentCount) =>
        GetUserLimit(plan) == Unlimited || currentCount < GetUserLimit(plan);
}
