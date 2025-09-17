using ClosedXML.Excel;
using IndexContainment.Core;
using IndexContainment.Core.Models;
using IndexContainment.Core.Utils;
using IndexContainment.Analysis;

namespace IndexContainment.ExcelIO;

public static class WorkbookWriter
{
    public static void Write(string outPath, string[] symbols, string dataRoot, TimeSpan anchor, Dictionary<string, List<DailyRow>> perSymbolRows)
    {
        using var wb = new XLWorkbook();
        WriteConfigSheet(wb, symbols, dataRoot, anchor);

        foreach (var sym in symbols)
        {
            perSymbolRows.TryGetValue(sym, out var rows);
            rows ??= new List<DailyRow>();
            WriteSymbolSheet(wb, sym, rows);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        wb.SaveAs(outPath);
    }

    static void WriteConfigSheet(XLWorkbook wb, string[] symbols, string dataRoot, TimeSpan anchor)
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

    static void WriteSymbolSheet(XLWorkbook wb, string symbol, List<DailyRow> rows)
    {
        var ws = wb.Worksheets.Add(Sheet.SafeName(symbol));
        int r = 1;

        // Summary block
        ws.Cell(r,1).Value = "Threshold"; ws.Cell(r,2).Value = "n"; ws.Cell(r,3).Value = "Hits"; ws.Cell(r,4).Value = "HitRate";
        ws.Cell(r,5).Value = "WilsonLower95"; ws.Cell(r,6).Value = "p99ViolationRatio"; ws.Cell(r,7).Value = "MedianTimeToLow(min)";
        r++;

        foreach (var (label, X) in Thresholds.Grid)
        {
            var sum = SummaryBuilder.Build(rows).FirstOrDefault(s => s.Threshold == label);
            if (sum is null) continue;
            ws.Cell(r,1).Value = sum.Threshold;
            ws.Cell(r,2).Value = sum.N;
            ws.Cell(r,3).Value = sum.Hits;
            ws.Cell(r,4).Value = sum.HitRate;
            ws.Cell(r,5).Value = sum.WilsonLower95;
            ws.Cell(r,6).Value = (double)sum.P99ViolationRatio;
            ws.Cell(r,7).Value = sum.MedianTimeToLow;
            r++;
        }

        r += 1;

        // Daily table
        var headers = new[]
        {
            "Date","PrevClose","Open","P10","LowAfter10","HighAfter10","Close",
            "GapPct","ExtraDropPct","ExtraRisePct","TimeToLowMins",
            "Qual_1.0%","Hold_1.0%","VR_1.0%",
            "Qual_1.5%","Hold_1.5%","VR_1.5%",
            "Qual_2.0%","Hold_2.0%","VR_2.0%",
            "Qual_3.0%","Hold_3.0%","VR_3.0%",
            "Qual_4.0%","Hold_4.0%","VR_4.0%"
        };
        for (int c = 0; c < headers.Length; c++) ws.Cell(r, c + 1).Value = headers[c];
        r++;

        foreach (var x in rows.OrderBy(z => z.Date))
        {
            int c = 1;
            ws.Cell(r,c++).Value = x.Date; ws.Cell(r, c-1).Style.DateFormat.Format = "yyyy-mm-dd";
            ws.Cell(r,c++).Value = x.PrevClose;
            ws.Cell(r,c++).Value = x.Open;
            ws.Cell(r,c++).Value = x.P10;
            ws.Cell(r,c++).Value = x.LowAfter10;
            ws.Cell(r,c++).Value = x.HighAfter10;
            ws.Cell(r,c++).Value = x.Close;

            ws.Cell(r,c++).Value = (double)x.GapPct;
            ws.Cell(r,c++).Value = (double)x.ExtraDropPct;
            ws.Cell(r,c++).Value = (double)x.ExtraRisePct;
            ws.Cell(r,c++).Value = x.TimeToLowMins;

            ws.Cell(r,c++).Value = x.Qual_1_0; ws.Cell(r,c++).Value = x.Hold_1_0; ws.Cell(r,c++).Value = (double)x.VR_1_0;
            ws.Cell(r,c++).Value = x.Qual_1_5; ws.Cell(r,c++).Value = x.Hold_1_5; ws.Cell(r,c++).Value = (double)x.VR_1_5;
            ws.Cell(r,c++).Value = x.Qual_2_0; ws.Cell(r,c++).Value = x.Hold_2_0; ws.Cell(r,c++).Value = (double)x.VR_2_0;
            ws.Cell(r,c++).Value = x.Qual_3_0; ws.Cell(r,c++).Value = x.Hold_3_0; ws.Cell(r,c++).Value = (double)x.VR_3_0;
            ws.Cell(r,c++).Value = x.Qual_4_0; ws.Cell(r,c++).Value = x.Hold_4_0; ws.Cell(r,c++).Value = (double)x.VR_4_0;
            r++;
        }

        var lastCol = headers.Length;
        ws.Range(1,1,1+Thresholds.Grid.Length,7).Style.Font.Bold = true;
        ws.Range(1,1,1+Thresholds.Grid.Length,7).Style.Fill.BackgroundColor = XLColor.FromTheme(XLThemeColor.Accent1, 0.8);
        ws.Range(1+Thresholds.Grid.Length+1,1,1+Thresholds.Grid.Length+1, lastCol).Style.Font.Bold = true;
        ws.SheetView.FreezeRows(1 + Thresholds.Grid.Length + 1);
        ws.Range(1+Thresholds.Grid.Length+1,1, r-1, lastCol).SetAutoFilter();
        ws.Columns().AdjustToContents();
    }
}