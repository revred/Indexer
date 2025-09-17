using ClosedXML.Excel;
using IndexContainment.Analysis;
using IndexContainment.Core;
using IndexContainment.Core.Models;
using IndexContainment.Core.Utils;

namespace IndexContainment.ExcelIO;

public static class SymbolBookWriter
{
    public static string Write(string outDir, string symbol, List<DailyRow> rows, TimeSpan anchor)
    {
        Directory.CreateDirectory(outDir);
        var outPath = Path.Combine(outDir, $"{symbol}.xlsx");
        using var wb = new XLWorkbook();
        // Summary sheet
        var ws = wb.Worksheets.Add("Summary");
        int r = 1;
        ws.Cell(r,1).Value = "Threshold"; ws.Cell(r,2).Value = "n"; ws.Cell(r,3).Value = "Hits"; ws.Cell(r,4).Value = "HitRate"; ws.Cell(r,5).Value = "WilsonLower95"; ws.Cell(r,6).Value = "p99ViolationRatio"; ws.Cell(r,7).Value = "MedianTimeToLow(min)";
        r++;
        var summaries = SummaryBuilder.Build(rows);
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

        // Daily sheet
        var wd = wb.Worksheets.Add("Daily");
        int rd = 1;
        var headers = new[] { "Date","PrevClose","Open","P10","LowAfter10","HighAfter10","Close","GapPct","ExtraDropPct","ExtraRisePct","TimeToLowMins","Qual_1.0%","Hold_1.0%","VR_1.0%","Qual_1.5%","Hold_1.5%","VR_1.5%","Qual_2.0%","Hold_2.0%","VR_2.0%","Qual_3.0%","Hold_3.0%","VR_3.0%","Qual_4.0%","Hold_4.0%","VR_4.0%" };
        for (int c = 0; c < headers.Length; c++) wd.Cell(rd, c + 1).Value = headers[c];
        rd++;
        foreach (var x in rows.OrderBy(z => z.Date))
        {
            int c = 1;
            wd.Cell(rd,c++).Value = x.Date; wd.Cell(rd, c-1).Style.DateFormat.Format = "yyyy-mm-dd";
            wd.Cell(rd,c++).Value = x.PrevClose;
            wd.Cell(rd,c++).Value = x.Open;
            wd.Cell(rd,c++).Value = x.P10;
            wd.Cell(rd,c++).Value = x.LowAfter10;
            wd.Cell(rd,c++).Value = x.HighAfter10;
            wd.Cell(rd,c++).Value = x.Close;
            wd.Cell(rd,c++).Value = (double)x.GapPct;
            wd.Cell(rd,c++).Value = (double)x.ExtraDropPct;
            wd.Cell(rd,c++).Value = (double)x.ExtraRisePct;
            wd.Cell(rd,c++).Value = x.TimeToLowMins;
            wd.Cell(rd,c++).Value = x.Qual_1_0; wd.Cell(rd,c++).Value = x.Hold_1_0; wd.Cell(rd,c++).Value = (double)x.VR_1_0;
            wd.Cell(rd,c++).Value = x.Qual_1_5; wd.Cell(rd,c++).Value = x.Hold_1_5; wd.Cell(rd,c++).Value = (double)x.VR_1_5;
            wd.Cell(rd,c++).Value = x.Qual_2_0; wd.Cell(rd,c++).Value = x.Hold_2_0; wd.Cell(rd,c++).Value = (double)x.VR_2_0;
            wd.Cell(rd,c++).Value = x.Qual_3_0; wd.Cell(rd,c++).Value = x.Hold_3_0; wd.Cell(rd,c++).Value = (double)x.VR_3_0;
            wd.Cell(rd,c++).Value = x.Qual_4_0; wd.Cell(rd,c++).Value = x.Hold_4_0; wd.Cell(rd,c++).Value = (double)x.VR_4_0;
            rd++;
        }
        wd.SheetView.FreezeRows(1);
        wd.Range(1,1,rd-1,headers.Length).SetAutoFilter();
        wd.Columns().AdjustToContents();

        wb.SaveAs(outPath);
        return outPath;
    }
}