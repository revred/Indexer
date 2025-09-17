using IndexContainment.Core.Models;
using IndexContainment.Core.Scheduling;

namespace IndexContainment.Data;

public enum ResampleMode { None, Composite, Auto }

public static class CompositeResampler
{
    public static DayBars ResampleIfNeeded(DayBars day, ResampleMode mode)
    {
        return mode switch
        {
            ResampleMode.None => day,
            ResampleMode.Composite => Resample(day),
            ResampleMode.Auto => NeedsResample(day) ? Resample(day) : day,
            _ => day
        };
    }

    static bool NeedsResample(DayBars day)
    {
        if (day.Bars.Count < 5) return false;
        var diffs = new List<TimeSpan>(day.Bars.Count - 1);
        for (int i = 1; i < day.Bars.Count; i++)
            diffs.Add(day.Bars[i].T - day.Bars[i-1].T);
        diffs.Sort();
        var med = diffs[diffs.Count/2];
        return med <= TimeSpan.FromSeconds(65); // minute-ish
    }

    public static DayBars Resample(DayBars day)
    {
        if (day.Bars.Count == 0) return day;
        var (open, close) = SessionDetector.Detect(day);

        var windows = CompositeSchedule.BuildWindows(open, close).ToList();
        if (windows.Count == 0) return day;

        var src = day.Bars;
        var outBars = new List<Bar>(windows.Count);

        int idx = 0;
        foreach (var (start, end) in windows)
        {
            // Treat bar timestamp as bar END time. Aggregate (start, end] inclusive of end.
            decimal? o = null, h = null, l = null, c = null;
            long v = 0;
            while (idx < src.Count && src[idx].T <= end)
            {
                var b = src[idx];
                if (b.T <= start) { idx++; continue; } // before window
                if (o is null) o = b.O;
                h = h is null ? b.H : Math.Max(h.Value, b.H);
                l = l is null ? b.L : Math.Min(l.Value, b.L);
                c = b.C;
                v += b.V;
                idx++;
            }
            if (o is null) continue; // no data for this window
            outBars.Add(new Bar(end, o.Value, h!.Value, l!.Value, c!.Value, v));
        }

        // Validate coverage: if out too small, fallback to original day
        if (outBars.Count < Math.Max(24, windows.Count / 2)) return day;

        return new DayBars(day.D, outBars, day.PrevClose);
    }
}