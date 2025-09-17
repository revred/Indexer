using ClosedXML.Excel;
using IndexContainment.Analysis;
using IndexContainment.Core;
using IndexContainment.Core.Models;
using IndexContainment.Core.Utils;

namespace IndexContainment.ExcelIO;

public static class StrategyBookWriter
{
    public static string Write(string outPath, string[] symbols, string dataRoot, TimeSpan anchor, Dictionary<string, List<DailyRow>> perSymbolRows)
    {
        using var wb = new XLWorkbook();
        WriteConfig(wb, symbols, dataRoot, anchor);
        WriteLeaderboard(wb, perSymbolRows);
        foreach (var sym in symbols)
        {
            var ws = wb.Worksheets.Add(Sheet.SafeName(sym));
            int r = 1;
            ws.Cell(r,1).Value = "Threshold"; ws.Cell(r,2).Value = "n"; ws.Cell(r,3).Value = "Hits"; ws.Cell(r,4).Value = "HitRate";
            ws.Cell(r,5).Value = "WilsonLower95"; ws.Cell(r,6).Value = "p99ViolationRatio"; ws.Cell(r,7).Value = "MedianTimeToLow(min)";
            r++;
            var rows = perSymbolRows.TryGetValue(sym, out var list) ? list : new List<DailyRow>();
            var summaries = SummaryBuilder.Build(list ?? new List<DailyRow>());
            foreach (var s in summaries)
            {
                ws.Cell(r,1).Value = s.Threshold;
                ws.Cell(r,2).Value = s.N;
                ws.Cell(r,3).Value = s.Hits;
                ws.Cell(r,4).Value = s.HitRate;
                ws.Cell(r,5).Value = s.WilsonLower95;
                ws.Cell(r,6).Value = (double)s.P99ViolationRatio;
                ws.Cell(r,7).Value = s.MedianTimeToLow;
                r++;
            }
            ws.Columns().AdjustToContents();
        }
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        wb.SaveAs(outPath);
        return outPath;
    }

    private static void WriteConfig(XLWorkbook wb, string[] symbols, string dataRoot, TimeSpan anchor)
    {
        var ws = wb.Worksheets.Add("Config");
        int r = 1;
        ws.Cell(r++, 1).Value = "Build UTC"; ws.Cell(r - 1, 2).Value = DateTime.UtcNow.ToString("u");
        ws.Cell(r++, 1).Value = "Data Root"; ws.Cell(r - 1, 2).Value = dataRoot;
        ws.Cell(r++, 1).Value = "Anchor";    ws.Cell(r - 1, 2).Value = anchor.ToString(@"hh\:mm");
        ws.Cell(r++, 1).Value = "Thresholds"; ws.Cell(r - 1, 2).Value = string.Join(", ", Thresholds.Grid.Select(x => x.Label));
        ws.Cell(r++, 1).Value = "Symbols"; ws.Cell(r - 1, 2).Value = string.Join(", ", symbols);
        ws.Columns().AdjustToContents();
    }

    private static void WriteLeaderboard(XLWorkbook wb, Dictionary<string, List<DailyRow>> perSymbolRows)
    {
        var ws = wb.Worksheets.Add("Leaderboard");
        int r = 1;
        ws.Cell(r,1).Value = "Symbol"; ws.Cell(r,2).Value = "Threshold"; ws.Cell(r,3).Value = "n"; ws.Cell(r,4).Value = "Hits"; ws.Cell(r,5).Value = "HitRate"; ws.Cell(r,6).Value = "WilsonLower95";
        r++;
        foreach (var sym in perSymbolRows.Keys.OrderBy(k => k))
        {
            var list = perSymbolRows[sym];
            var summaries = SummaryBuilder.Build(list);
            foreach (var s in summaries.OrderByDescending(z => z.WilsonLower95 * Math.Sqrt(Math.Max(1,z.N))))
            {
                ws.Cell(r,1).Value = sym;
                ws.Cell(r,2).Value = s.Threshold;
                ws.Cell(r,3).Value = s.N;
                ws.Cell(r,4).Value = s.Hits;
                ws.Cell(r,5).Value = s.HitRate;
                ws.Cell(r,6).Value = s.WilsonLower95;
                r++;
            }
        }
        ws.Columns().AdjustToContents();
    }
}