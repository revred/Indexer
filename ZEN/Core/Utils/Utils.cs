namespace IndexContainment.Core.Utils;

public static class Wilson
{
    public static double Lower95(int hits, int n)
    {
        if (n <= 0) return 0d;
        double p = (double)hits / n, z = 1.96, N = n;
        double denom = 1 + z*z/N;
        double center = p + z*z/(2*N);
        double margin = z * Math.Sqrt((p*(1-p) + z*z/(4*N))/N);
        return (center - margin) / denom;
    }
}

public static class Mathx
{
    public static decimal Round6(decimal d) => Math.Round(d, 6, MidpointRounding.AwayFromZero);
}

public static class Sheet
{
    public static string SafeName(string s) => s.Length <= 31 ? s : s.Substring(0,31);
}