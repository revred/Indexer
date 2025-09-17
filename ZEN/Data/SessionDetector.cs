using IndexContainment.Core.Models;

namespace IndexContainment.Data;

public static class SessionDetector
{
    /// <summary>
    /// Returns (open, close) inferred from bar timestamps (first and last).
    /// Works for full days and half-days. If bars empty, returns (Date, Date).
    /// </summary>
    public static (DateTime Open, DateTime Close) Detect(DayBars day)
    {
        if (day.Bars.Count == 0) return (day.D, day.D);
        var open = day.Bars.First().T;
        var close = day.Bars.Last().T;
        // sanity: ensure same calendar date component; if not, trust actual stamps
        if (close < open) (open, close) = (close, open);
        return (open, close);
    }

    /// <summary>
    /// Heuristic: identify early close days (no calendar required).
    /// </summary>
    public static bool IsEarlyClose((DateTime Open, DateTime Close) s, TimeSpan normalLength)
        => (s.Close - s.Open) < normalLength;
}