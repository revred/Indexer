using IndexContainment.Core;
using IndexContainment.Core.Models;
using IndexContainment.Core.Utils;

namespace IndexContainment.Analysis;

public sealed record SummaryRow(string Threshold, int N, int Hits, double HitRate, double WilsonLower95, decimal P99ViolationRatio, int MedianTimeToLow);

public static class SummaryBuilder
{
    public static List<SummaryRow> Build(List<DailyRow> rows)
    {
        var outRows = new List<SummaryRow>();
        foreach (var (label, X) in Thresholds.Grid)
        {
            var qualifies = rows.Where(z => Qual(z, X)).ToList();
            int n = qualifies.Count;
            int hits = qualifies.Count(z => Hold(z, X));
            double hit = n > 0 ? (double)hits / n : 0;
            double wl = Wilson.Lower95(hits, n);
            decimal p99vr = n > 0 ? qualifies.Select(z => VR(z, X)).OrderBy(v => v).ElementAt(Math.Max(0,(int)Math.Floor(0.99 * (n - 1)))) : 0m;
            int medTTL = n > 0 ? qualifies.Select(z => z.TimeToLowMins).OrderBy(x => x).ElementAt(n/2) : 0;
            outRows.Add(new SummaryRow(label, n, hits, hit, wl, p99vr, medTTL));
        }
        return outRows;
    }

    static bool Qual(DailyRow z, decimal X) => X switch
    {
        0.01m  => z.Qual_1_0 == 1,
        0.015m => z.Qual_1_5 == 1,
        0.02m  => z.Qual_2_0 == 1,
        0.03m  => z.Qual_3_0 == 1,
        0.04m  => z.Qual_4_0 == 1,
        _ => false
    };

    static bool Hold(DailyRow z, decimal X) => X switch
    {
        0.01m  => z.Hold_1_0 == 1,
        0.015m => z.Hold_1_5 == 1,
        0.02m  => z.Hold_2_0 == 1,
        0.03m  => z.Hold_3_0 == 1,
        0.04m  => z.Hold_4_0 == 1,
        _ => false
    };

    static decimal VR(DailyRow z, decimal X) => X switch
    {
        0.01m  => z.VR_1_0,
        0.015m => z.VR_1_5,
        0.02m  => z.VR_2_0,
        0.03m  => z.VR_3_0,
        0.04m  => z.VR_4_0,
        _ => 0m
    };
}