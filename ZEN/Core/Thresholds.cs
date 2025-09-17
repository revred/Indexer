namespace IndexContainment.Core;

public static class Thresholds
{
    public static readonly (string Label, decimal X)[] Grid = new[]
    {
        ("1.0%", 0.01m),
        ("1.5%", 0.015m),
        ("2.0%", 0.02m),
        ("3.0%", 0.03m),
        ("4.0%", 0.04m)
    };
}