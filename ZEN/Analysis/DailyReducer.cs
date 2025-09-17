using IndexContainment.Core;
using IndexContainment.Core.Models;
using IndexContainment.Core.Utils;

namespace IndexContainment.Analysis;

public static class DailyReducer
{
    public static List<DailyRow> BuildRows(IEnumerable<DayBars> days, TimeSpan anchor)
    {
        var rows = new List<DailyRow>();
        foreach (var d in days)
        {
            if (d.PrevClose <= 0) continue;

            var open = d.Bars.First().O;
            var gap  = (open / d.PrevClose) - 1m;

            var anchorBar = d.Bars.Where(b => b.T.TimeOfDay <= anchor).LastOrDefault();
            if (anchorBar is null) continue;

            var after = d.Bars.Where(b => b.T.TimeOfDay > anchor).ToList();
            if (after.Count == 0) continue;

            var p10 = anchorBar.C;
            var lowAfter = after.Min(b => b.L);
            var highAfter = after.Max(b => b.H);
            var close = d.Bars.Last().C;

            var extraDrop = p10 == 0 ? 0 : (p10 - lowAfter) / p10;
            var extraRise = p10 == 0 ? 0 : (highAfter - p10) / p10;

            // Time to low after anchor
            var minBar = after.OrderBy(b => b.L).ThenBy(b => b.T).First();
            var ttl = (int)Math.Round((minBar.T - new DateTime(d.D.Year, d.D.Month, d.D.Day, anchor.Hours, anchor.Minutes, 0)).TotalMinutes);

            var map = Thresholds.Grid.ToDictionary(x => x.X, x => QualHoldVR(gap, extraDrop, x.X));

            rows.Add(new DailyRow(
                d.D, d.PrevClose, open, p10, lowAfter, highAfter, close,
                Mathx.Round6(gap), Mathx.Round6(extraDrop), Mathx.Round6(extraRise), ttl,
                map[0.01m].Qual, map[0.01m].Hold, Mathx.Round6(map[0.01m].VR),
                map[0.015m].Qual, map[0.015m].Hold, Mathx.Round6(map[0.015m].VR),
                map[0.02m].Qual, map[0.02m].Hold, Mathx.Round6(map[0.02m].VR),
                map[0.03m].Qual, map[0.03m].Hold, Mathx.Round6(map[0.03m].VR),
                map[0.04m].Qual, map[0.04m].Hold, Mathx.Round6(map[0.04m].VR)
            ));
        }
        return rows;
    }

    static (int Qual, int Hold, decimal VR) QualHoldVR(decimal gap, decimal extraDrop, decimal X)
    {
        bool qualifies = gap <= -X;
        bool holds = qualifies && extraDrop <= (X / 2m);
        decimal vr = (X == 0) ? 0 : (extraDrop / X);
        return (qualifies ? 1 : 0, holds ? 1 : 0, vr);
    }
}