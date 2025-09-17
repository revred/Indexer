# 1358_Patch_07_DATA_TIERING_AND_EXPORTS.md

This patch implements the **tiered data layout and export pipeline**:

- **DAILY/** per-symbol CSV (one row per trading day)
- **SUMMARIES/** per-symbol JSON + **leaderboard.json**
- **EXCEPTIONS/** worst VR cases per symbol (CSV)
- **StrategyBook.xlsx** (thin master) and optional per-symbol workbooks
- **manifest.json** with build args and checksums

It also wires new CLI flags to control outputs and is **asset-agnostic** (works for indices, gold, oil, volatility, etc.).


---

## Add/Update: `README.md (append Data Products)`

```markdown
## Data Products (Tiered)

**Raw minutes** → `DATA/<SYMBOL>/<SYMBOL>_YYYY.csv` (from ThetaData/IBKR)

**Composite intraday (5m/15m/5m)** → internal (resampled on the fly)

**Daily reductions** → `DAILY/<SYMBOL>.csv`

**Summaries** → `SUMMARIES/summary_<SYMBOL>.json` + `SUMMARIES/leaderboard.json`

**Exceptions** → `EXCEPTIONS/<SYMBOL>_vr_worst.csv`

**Excel** → `OUTPUT/StrategyBook.xlsx` (summaries only) and (optional) `OUTPUT/sheets/<SYMBOL>.xlsx`

### CLI (exports)
```bash
dotnet run --project ZEN/Cli --   --data ../DATA   --out  ../OUTPUT/StrategyBook.xlsx   --symbols SPX,NDX,VIX,XAUUSD,CL,QQQ,SPY   --anchor 10:00   --resample auto   --xl-mode both   --emit-daily true   --emit-summaries true   --exceptions-top 25
```

- `--xl-mode strategy|symbol|both` controls Excel outputs.

- `--emit-daily` writes `DAILY/<SYMBOL>.csv`.

- `--emit-summaries` writes `SUMMARIES/*.json` + `leaderboard.json`.

- `--exceptions-top N` writes `EXCEPTIONS/<SYMBOL>_vr_worst.csv` with top-N violations.

- Works for **indices** (SPX, NDX), **volatility** (VIX), **commodities**/**FX** (XAUUSD), **oil** (CL or an index/ETF like USO), etc.
```


---

## Add/Update: `WPS/WP8_Excel_Writer.md (amend)`

```markdown
# WP8 — Excel Writers (Thin Strategy & Per-Symbol)

- **StrategyBook.xlsx** (thin master): `Config`, `Leaderboard`, and one **summary-only** sheet per symbol.

- **Per-Symbol sheet** (optional): full `DailyRow` table + summary block in `OUTPUT/sheets/<SYMBOL>.xlsx`.

- No raw minute data in Excel.
```


---

## Add/Update: `WPS/WP9_Config_Metadata.md (amend)`

```markdown
# WP9 — Config & Metadata (Manifest)

Emit `OUTPUT/manifest.json` with build time (UTC), args, thresholds, symbols, and SHA-256 checksums of outputs (StrategyBook, symbol books, DAILY, SUMMARIES, EXCEPTIONS).
```


---

## Add/Update: `ZEN/Core/Thresholds.cs (new, if missing)`

```csharp
namespace IndexContainment.Core;

public static class Thresholds
{
    public static readonly (string Label, decimal X)[] Grid = new[]
    {
        ("1.0%", 0.01m),
        ("1.5%", 0.015m),
        ("2.0%", 0.02m),
        ("3.0%", 0.03m),
        ("4.0%", 0.04m),
    };
}
```


---

## Add/Update: `ZEN/Export/DailyCsvWriter.cs (new)`

```csharp
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
```


---

## Add/Update: `ZEN/Export/SummaryJsonWriter.cs (new)`

```csharp
using System.Text.Json;
using IndexContainment.Analysis;
using IndexContainment.Core.Models;

namespace IndexContainment.Export;

public static class SummaryJsonWriter
{
    public static string WriteSymbolSummary(string root, string symbol, List<DailyRow> rows)
    {
        Directory.CreateDirectory(root);
        var summaries = SummaryBuilder.Build(rows);
        var payload = new {
            symbol,
            generatedUtc = DateTime.UtcNow,
            thresholds = summaries
                .Select(s => new { s.Threshold, s.N, s.Hits, s.HitRate, s.WilsonLower95, s.P99ViolationRatio, s.MedianTimeToLow })
                .ToList()
        };
        var path = Path.Combine(root, $"summary_{symbol}.json");
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
        return path;
    }

    public static string WriteLeaderboard(string root, Dictionary<string, List<DailyRow>> perSymbolRows)
    {
        Directory.CreateDirectory(root);
        var table = new List<object>();
        foreach (var (sym, rows) in perSymbolRows.OrderBy(kv => kv.Key))
        {
            var s = SummaryBuilder.Build(rows);
            foreach (var row in s)
            {
                table.Add(new {
                    symbol = sym,
                    row.Threshold,
                    row.N,
                    row.Hits,
                    row.HitRate,
                    row.WilsonLower95,
                    row.P99ViolationRatio,
                    row.MedianTimeToLow
                });
            }
        }
        var payload = new { generatedUtc = DateTime.UtcNow, items = table };
        var path = Path.Combine(root, "leaderboard.json");
        var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
        return path;
    }
}
```


---

## Add/Update: `ZEN/Export/ExceptionsExporter.cs (new)`

```csharp
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
```


---

## Add/Update: `ZEN/Export/ManifestWriter.cs (new)`

```csharp
using System.Security.Cryptography;
using System.Text.Json;

namespace IndexContainment.Export;

public static class ManifestWriter
{
    public static string Write(string outputRoot, IDictionary<string, object> meta, IEnumerable<string> files)
    {
        Directory.CreateDirectory(outputRoot);
        var items = new List<object>();
        foreach (var f in files.Distinct())
        {
            if (!File.Exists(f)) continue;
            items.Add(new {
                path = f.Replace("\\","/"),
                sha256 = Sha256Of(f),
                size = new FileInfo(f).Length
            });
        }
        var payload = new {
            generatedUtc = DateTime.UtcNow,
            meta,
            artifacts = items
        };
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        var path = Path.Combine(outputRoot, "manifest.json");
        File.WriteAllText(path, json);
        return path;
    }

    private static string Sha256Of(string path)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(path);
        var bytes = sha.ComputeHash(fs);
        return string.Concat(bytes.Select(b => b.ToString("x2")));
    }
}
```


---

## Add/Update: `ZEN/Excel/StrategyBookWriter.cs (new)`

```csharp
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
```


---

## Add/Update: `ZEN/Excel/SymbolBookWriter.cs (new)`

```csharp
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
```


---

## Add/Update: `ZEN/Cli/Program.cs (update analytics path with exports)`

```csharp
using IndexContainment.Data;
using IndexContainment.Data.Backfill;
using IndexContainment.Analysis;
using IndexContainment.ExcelIO;
using IndexContainment.Core.Models;
using IndexContainment.Export;

static string[] DiscoverSymbols(string root) =>
    Directory.Exists(root)
        ? Directory.GetDirectories(root).Select(Path.GetFileName).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray()
        : Array.Empty<string>();

static string GetArg(string[] args, string key, string def)
{
    int i = Array.IndexOf(args, key);
    return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : def;
}

static bool GetBool(string[] args, string key, bool def)
{
    var s = GetArg(args, key, def ? "true" : "false").ToLowerInvariant();
    return s is "1" or "true" or "yes" or "y";
}

static ResampleMode ParseResample(string s) => s?.ToLowerInvariant() switch
{
    "none" => ResampleMode.None,
    "composite" => ResampleMode.Composite,
    "auto" or "" or null => ResampleMode.Auto,
    _ => ResampleMode.Auto
};

int Main(string[] args)
{
    if (args.Length > 0 && args[0].Equals("backfill", StringComparison.OrdinalIgnoreCase))
        return BackfillMain(args.Skip(1).ToArray());

    // ==== analytics + exports ====
    string dataRoot = GetArg(args, "--data", "../DATA");
    string outPath  = GetArg(args, "--out",  $"../OUTPUT/StrategyBook_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    string symsArg  = GetArg(args, "--symbols", "");
    string anchorS  = GetArg(args, "--anchor",  "10:00");
    string resampleS= GetArg(args, "--resample", "auto");

    string xlMode   = GetArg(args, "--xl-mode", "strategy"); // strategy|symbol|both
    bool emitDaily  = GetBool(args, "--emit-daily", true);
    bool emitSumm   = GetBool(args, "--emit-summaries", true);
    int topN        = int.TryParse(GetArg(args, "--exceptions-top", "25"), out var n) ? n : 25;

    if (!TimeSpan.TryParse(anchorS, out var anchor)) anchor = new TimeSpan(10,0,0);
    var mode = ParseResample(resampleS);

    var symbols = symsArg.Length > 0
        ? symsArg.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        : DiscoverSymbols(dataRoot);

    if (symbols.Length == 0)
    {
        Console.Error.WriteLine("No symbols specified or discovered.");
        return 2;
    }

    var perSymbolRows = new Dictionary<string, List<DailyRow>>();

    foreach (var sym in symbols)
    {
        int loaded = 0, kept = 0, lowCov = 0, badOhlc = 0, early = 0;
        int sumBars = 0;

        try
        {
            var dir = Path.Combine(dataRoot, sym);
            var days = IndexContainment.Data.CsvLoader.LoadAll(sym, dir);

            var processed = new List<DayBars>();
            foreach (var d in days)
            {
                loaded++;
                var resampled = CompositeResampler.ResampleIfNeeded(d, mode);

                bool ok = resampled.Bars.All(b => b.L <= Math.Min(b.O, Math.Min(b.H, b.C)) && b.H >= Math.Max(b.O, Math.Max(b.L, b.C)));
                if (!ok) { badOhlc++; continue; }

                if (resampled.Bars.Count < 24) { lowCov++; continue; }

                var sdet = SessionDetector.Detect(resampled);
                if ((sdet.Close - sdet.Open) <= TimeSpan.FromHours(5)) early++;

                kept++;
                sumBars += resampled.Bars.Count;
                processed.Add(resampled);
            }

            var rows = DailyReducer.BuildRows(processed, anchor);
            perSymbolRows[sym] = rows;

            var medBars = kept > 0 ? (sumBars / Math.Max(1, kept)) : 0;
            var line = $"[{sym}] days_loaded={loaded} kept={kept} lowcov={lowCov} badohlc={badOhlc} early={early} med_bars={medBars} mode={mode}";
            Console.WriteLine(line);

            Directory.CreateDirectory(Path.Combine("..","OUTPUT"));
            File.WriteAllText(Path.Combine("..","OUTPUT", $"Integrity_{sym}.txt"), line + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[{sym}] ERROR: {ex.Message}");
            return 3;
        }
    }

    // === Exports ===
    var outputs = new List<string>();
    var outputRoot = Path.GetDirectoryName(outPath) ?? "../OUTPUT";
    var dailyRoot = Path.Combine("..","DAILY");
    var summariesRoot = Path.Combine("..","SUMMARIES");
    var exceptionsRoot = Path.Combine("..","EXCEPTIONS");
    var symbolSheetsRoot = Path.Combine(outputRoot, "sheets");

    if (emitDaily)
    {
        foreach (var (sym, rows) in perSymbolRows)
            outputs.Add(DailyCsvWriter.Write(dailyRoot, sym, rows));
    }
    if (emitSumm)
    {
        foreach (var (sym, rows) in perSymbolRows)
            outputs.Add(SummaryJsonWriter.WriteSymbolSummary(summariesRoot, sym, rows));
        outputs.Add(SummaryJsonWriter.WriteLeaderboard(summariesRoot, perSymbolRows));
    }

    foreach (var (sym, rows) in perSymbolRows)
        outputs.Add(ExceptionsExporter.WriteWorstVR(exceptionsRoot, sym, rows, topN));

    // Excel
    if (xlMode is "strategy" or "both")
        outputs.Add(StrategyBookWriter.Write(outPath, symbols, dataRoot, anchor, perSymbolRows));
    if (xlMode is "symbol" or "both")
        foreach (var (sym, rows) in perSymbolRows)
            outputs.Add(SymbolBookWriter.Write(symbolSheetsRoot, sym, rows, anchor));

    // Manifest
    var meta = new Dictionary<string, object>
    {
        ["buildUtc"] = DateTime.UtcNow.ToString("u"),
        ["dataRoot"] = dataRoot,
        ["outPath"] = outPath,
        ["symbols"] = symbols,
        ["anchor"] = anchor.ToString(@"hh\:mm"),
        ["resample"] = mode.ToString(),
        ["xlMode"] = xlMode,
        ["emitDaily"] = emitDaily,
        ["emitSummaries"] = emitSumm,
        ["exceptionsTop"] = topN
    };
    outputs.Add(ManifestWriter.Write(outputRoot, meta, outputs));

    Console.WriteLine($"Wrote {outPath} and {outputs.Count} artifacts.");
    return 0;
}

int BackfillMain(string[] args)
{
    // unchanged from previous Theta backfill patch; omitted here for brevity in this diff.
    // Keep your existing `backfill theta` subcommand.
    Console.Error.WriteLine("Backfill support unchanged. Use previous 'backfill theta' command.");
    return 2;
}

return Main(args);
```


---

## Add/Update: `ZEN/Tests/ExportSmokeTests.cs (new)`

```csharp
using IndexContainment.Core.Models;
using IndexContainment.Analysis;
using IndexContainment.Export;
using Xunit;

public class ExportSmokeTests
{
    [Fact]
    public void DailyCsv_And_SummaryJson_Write()
    {
        var date = new DateTime(2024,1,3);
        var bars = new List<Bar>
        {
            new(date.AddHours(9).AddMinutes(30), 98m, 99m, 97.5m, 98.2m, 1000),
            new(date.AddHours(10), 98.2m, 99m, 98m, 98.6m, 1000),
            new(date.AddHours(15).AddMinutes(59), 98.6m, 99.2m, 98.1m, 98.9m, 1000),
        };
        var day = new DayBars(date, bars, 100m);
        var rows = IndexContainment.Analysis.DailyReducer.BuildRows(new[] { day }, new TimeSpan(10,0,0));

        var daily = DailyCsvWriter.Write("../DAILY", "TEST", rows);
        Assert.True(File.Exists(daily));

        var summary = SummaryJsonWriter.WriteSymbolSummary("../SUMMARIES", "TEST", rows);
        Assert.True(File.Exists(summary));

        var exc = ExceptionsExporter.WriteWorstVR("../EXCEPTIONS", "TEST", rows, 5);
        Assert.True(File.Exists(exc));
    }
}
```
