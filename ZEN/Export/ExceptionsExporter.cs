using System.Globalization;
using IndexContainment.Core;
using IndexContainment.Core.Models;

namespace IndexContainment.Export;

public static class ExceptionsExporter
{
    private sealed record VRRow(DateTime Date, string Threshold, decimal VR, decimal GapPct, decimal ExtraDropPct, decimal P10, decimal LowAfter10, int TimeToLowMins);

    public static string WriteWorstVR(string root, string symbol, List<DailyRow> rows, int topN = 25)
    {
        Directory.CreateDirectory(root);
        var list = new List<VRRow>();

        foreach (var r in rows)
        {
            foreach (var (label, X) in Thresholds.Grid)
            {
                decimal vr = GetVR(r, X);
                if (vr > 0m)
                {
                    list.Add(new VRRow(r.Date, label, vr, r.GapPct, r.ExtraDropPct, r.P10, r.LowAfter10, r.TimeToLowMins));
                }
            }
        }

        var worst = list.OrderByDescending(x => x.VR).Take(topN).ToList();
        var path = Path.Combine(root, $"{symbol}_vr_worst.csv");
        using var sw = new StreamWriter(path, false, new System.Text.UTF8Encoding(false));
        sw.WriteLine("Date,Threshold,VR,GapPct,ExtraDropPct,P10,LowAfter10,TimeToLowMins");
        foreach (var w in worst)
        {
            sw.WriteLine(string.Join(',', new[] {
                w.Date.ToString("yyyy-MM-dd"),
                w.Threshold,
                w.VR.ToString(CultureInfo.InvariantCulture),
                w.GapPct.ToString(CultureInfo.InvariantCulture),
                w.ExtraDropPct.ToString(CultureInfo.InvariantCulture),
                w.P10.ToString(CultureInfo.InvariantCulture),
                w.LowAfter10.ToString(CultureInfo.InvariantCulture),
                w.TimeToLowMins.ToString(CultureInfo.InvariantCulture)
            }));
        }
        return path;
    }

    private static decimal GetVR(DailyRow r, decimal X) => X switch
    {
        0.01m => r.VR_1_0,
        0.015m => r.VR_1_5,
        0.02m => r.VR_2_0,
        0.03m => r.VR_3_0,
        0.04m => r.VR_4_0,
        _ => 0m
    };
}