using System.Globalization;
using IndexContainment.Core.Models;

namespace IndexContainment.Export;

public static class DailyCsvWriter
{
    public static string Write(string root, string symbol, List<DailyRow> rows)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, $"{symbol}.csv");
        using var sw = new StreamWriter(path, false, new System.Text.UTF8Encoding(false));
        sw.WriteLine("Date,PrevClose,Open,P10,LowAfter10,HighAfter10,Close,GapPct,ExtraDropPct,ExtraRisePct,TimeToLowMins,Qual_1.0%,Hold_1.0%,VR_1.0%,Qual_1.5%,Hold_1.5%,VR_1.5%,Qual_2.0%,Hold_2.0%,VR_2.0%,Qual_3.0%,Hold_3.0%,VR_3.0%,Qual_4.0%,Hold_4.0%,VR_4.0%");
        foreach (var r in rows.OrderBy(z => z.Date))
        {
            sw.WriteLine(string.Join(',', new[] {
                r.Date.ToString("yyyy-MM-dd"),
                r.PrevClose.ToString(CultureInfo.InvariantCulture),
                r.Open.ToString(CultureInfo.InvariantCulture),
                r.P10.ToString(CultureInfo.InvariantCulture),
                r.LowAfter10.ToString(CultureInfo.InvariantCulture),
                r.HighAfter10.ToString(CultureInfo.InvariantCulture),
                r.Close.ToString(CultureInfo.InvariantCulture),
                r.GapPct.ToString(CultureInfo.InvariantCulture),
                r.ExtraDropPct.ToString(CultureInfo.InvariantCulture),
                r.ExtraRisePct.ToString(CultureInfo.InvariantCulture),
                r.TimeToLowMins.ToString(CultureInfo.InvariantCulture),
                r.Qual_1_0.ToString(),
                r.Hold_1_0.ToString(),
                r.VR_1_0.ToString(CultureInfo.InvariantCulture),
                r.Qual_1_5.ToString(),
                r.Hold_1_5.ToString(),
                r.VR_1_5.ToString(CultureInfo.InvariantCulture),
                r.Qual_2_0.ToString(),
                r.Hold_2_0.ToString(),
                r.VR_2_0.ToString(CultureInfo.InvariantCulture),
                r.Qual_3_0.ToString(),
                r.Hold_3_0.ToString(),
                r.VR_3_0.ToString(CultureInfo.InvariantCulture),
                r.Qual_4_0.ToString(),
                r.Hold_4_0.ToString(),
                r.VR_4_0.ToString(CultureInfo.InvariantCulture),
            }));
        }
        return path;
    }
}