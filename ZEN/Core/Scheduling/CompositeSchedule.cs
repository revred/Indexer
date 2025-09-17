namespace IndexContainment.Core.Scheduling;

public static class CompositeSchedule
{
    /// <summary>
    /// Builds composite windows: 5m first hour, 15m mid, 5m last hour.
    /// Windows are non-overlapping and ordered by time.
    /// </summary>
    public static IEnumerable<(DateTime Start, DateTime End)> BuildWindows(DateTime open, DateTime close)
    {
        if (close <= open) yield break;

        var firstHourEnd = open.AddHours(1);
        var lastHourStart = close.AddHours(-1);
        if (lastHourStart < open) lastHourStart = open; // pathological very short sessions

        // Phase 1: [open, min(firstHourEnd, close)] in 5m
        var p1End = Min(firstHourEnd, close);
        foreach (var w in Steps(open, p1End, TimeSpan.FromMinutes(5))) yield return w;

        // Phase 2: [p1End, lastHourStart] in 15m (only if non-empty)
        if (lastHourStart > p1End)
            foreach (var w in Steps(p1End, lastHourStart, TimeSpan.FromMinutes(15))) yield return w;

        // Phase 3: [max(lastHourStart, open), close] in 5m
        var p3Start = Max(lastHourStart, open);
        foreach (var w in Steps(p3Start, close, TimeSpan.FromMinutes(5))) yield return w;
    }

    private static IEnumerable<(DateTime Start, DateTime End)> Steps(DateTime start, DateTime end, TimeSpan step)
    {
        var cur = start;
        while (cur < end)
        {
            var next = cur.Add(step);
            if (next > end) next = end;
            yield return (cur, next);
            cur = next;
        }
    }

    private static DateTime Min(DateTime a, DateTime b) => a <= b ? a : b;
    private static DateTime Max(DateTime a, DateTime b) => a >= b ? a : b;
}