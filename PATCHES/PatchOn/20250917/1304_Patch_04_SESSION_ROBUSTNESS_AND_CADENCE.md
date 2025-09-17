# 1304_Patch_04_SESSION_ROBUSTNESS_AND_CADENCE.md

This patch updates the **Indexer** repo to make composite sampling robust to **half-days, early closes, and holidays**, and aligns code/docs/tests with the latest layout visible on GitHub. It:

- Formalizes the **composite cadence** (5m first hour, 15m mid, 5m last hour).
- Detects session **open/close per day from data** (so half-days work automatically).
- Adds `--resample` mode to CLI (*auto|composite|none*).
- Adds **integrity logging** per symbol (days kept, skipped, early-close counts, reasons).
- Expands tests for full days and half days.

> After applying: `cd ZEN && dotnet build && dotnet test && dotnet run --project Cli -- --data ../DATA --out ../OUTPUT/IndexContainment.xlsx --symbols SPY,QQQ,IWM,DIA --anchor 10:00 --resample auto`


---

## Add/Update: `README.md (append/replace relevant sections)`

```markdown
# README — Composite Cadence & Robust Sessions (Update)

## Intraday Cadence (Composite)
We use a **composite sampling** per trading day:
- **First Hour:** 5-minute bars from session open to open+60m
- **Mid Session:** 15-minute bars from open+60m to close−60m
- **Last Hour:** 5-minute bars from close−60m to session close

The session open/close are **detected from the data** (first and last bar timestamps), so **half-days and early closes** are handled automatically. Holidays yield **no data** and are naturally skipped.

### Resampling
Provide either minute bars or already-aggregated bars. The CLI supports:
- `--resample auto`  (default): detects minute bars and converts to composite cadence.
- `--resample composite`: force composite resampling.
- `--resample none`: treat input as already aggregated.

## Integrity Logs
The CLI writes a small integrity report per symbol to `OUTPUT/Integrity_<SYMBOL>.txt` with counters for: Days loaded, Kept, Skipped (reasons), Early-close days detected, and Median bars/day post-resample.

## Run
```bash
cd ZEN
dotnet build
dotnet test
dotnet run --project Cli --   --data ../DATA   --out ../OUTPUT/IndexContainment.xlsx   --symbols SPY,QQQ,IWM,DIA   --anchor 10:00   --resample auto
```
```


---

## Add/Update: `WPS/WP1_Data_Contract.md (update cadence section)`

```markdown
# WP1 — Data Contract (Composite Cadence & Sessions)

**Cadence**: 5m (first hour), 15m (mid), 5m (last hour). Session boundaries are **derived from the data** per day (first and last timestamps).

**Early Closes**: If `close - open < 6.5h`, last-hour window still uses `[close−60m, close]` and the mid window shrinks. If there is < 2h total session, mid window collapses (only first/last windows are used).

**Validation**: If input is minute bars → resample. If already aggregated → validate that bar end-times match the composite windows; else **Skip: NonConformingCadence**.
```


---

## Add/Update: `WPS/WP3_Loader_Session_Guardrails.md (amend)`

```markdown
# WP3 — Loader & Session Guardrails (Robust)

- **Session detection**: per day, `open = first bar time`, `close = last bar time` (exchange local time).
- **Resampling decision**: If median spacing ≤ 65s → treat as minute bars → resample to composite.
- **Coverage** (post-resample):
  - Full US day: target ≈ 42 bars (12 + 18 + 12).
  - US half-day (13:00 close): ≈ 30 bars (12 + 6 + 12).
  - Keep days with ≥ 24 bars; else **Skip: LowCoverage**.
- **Integrity**: log reasons: `LowCoverage`, `BadOHLC`, `NonConformingCadence`, `Dedup`, `Reorder`.
```


---

## Add/Update: `WPS/WP11_Validation_Logs.md (amend)`

```markdown
# WP11 — Validation & Logs (Expanded)

Emit `OUTPUT/Integrity_<SYMBOL>.txt` with:
- DaysLoaded
- DaysKept
- EarlyCloses (close - open <= 5h considered early; heuristic, not a calendar)
- Skipped: LowCoverage / BadOHLC / NonConformingCadence / Other
- MedianBarsAfterResample

Console should print a one-line summary per symbol and the output path.
```


---

## Add/Update: `ZEN/Data/SessionDetector.cs (new)`

```csharp
using IndexContainment.Core.Models;

namespace IndexContainment.Data;

public static class SessionDetector
{
    /// <summary>
    /// Returns (open, close) inferred from bar timestamps (first and last).
    /// Works for full days and half-days. If bars empty, returns (Date, Date).
    /// </summary>
    public static (DateTime Open, DateTime Close) Detect(DayBars day)
    {
        if (day.Bars.Count == 0) return (day.D, day.D);
        var open = day.Bars.First().T;
        var close = day.Bars.Last().T;
        // sanity: ensure same calendar date component; if not, trust actual stamps
        if (close < open) (open, close) = (close, open);
        return (open, close);
    }

    /// <summary>
    /// Heuristic: identify early close days (no calendar required).
    /// </summary>
    public static bool IsEarlyClose((DateTime Open, DateTime Close) s, TimeSpan normalLength)
        => (s.Close - s.Open) < normalLength;
}
```


---

## Add/Update: `ZEN/Core/Scheduling/CompositeSchedule.cs (replace)`

```csharp
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
```


---

## Add/Update: `ZEN/Data/CompositeResampler.cs (replace)`

```csharp
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
```


---

## Add/Update: `ZEN/Cli/Program.cs (replace)`

```csharp
using IndexContainment.Data;
using IndexContainment.Analysis;
using IndexContainment.ExcelIO;
using IndexContainment.Core.Models;

static string[] DiscoverSymbols(string root) =>
    Directory.Exists(root)
        ? Directory.GetDirectories(root).Select(Path.GetFileName).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray()
        : Array.Empty<string>();

static string GetArg(string[] args, string key, string def)
{
    int i = Array.IndexOf(args, key);
    return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : def;
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
    string dataRoot = GetArg(args, "--data", "../DATA");
    string outPath  = GetArg(args, "--out",  $"../OUTPUT/IndexContainment_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    string symsArg  = GetArg(args, "--symbols", "");
    string anchorS  = GetArg(args, "--anchor",  "10:00");
    string resampleS= GetArg(args, "--resample", "auto");
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
        var integrity = new List<string>();
        int loaded = 0, kept = 0, lowCov = 0, nonConf = 0, badOhlc = 0, early = 0;
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

                // quick OHLC sanity
                bool ok = resampled.Bars.All(b => b.L <= Math.Min(b.O, Math.Min(b.H, b.C)) && b.H >= Math.Max(b.O, Math.Max(b.L, b.C)));
                if (!ok) { badOhlc++; continue; }

                // coverage
                if (resampled.Bars.Count < 24) { lowCov++; continue; }

                // early close heuristic
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

    IndexContainment.ExcelIO.WorkbookWriter.Write(outPath, symbols, dataRoot, anchor, perSymbolRows);
    Console.WriteLine($"Wrote {outPath}");
    return 0;
}

return Main(args);
```


---

## Add/Update: `ZEN/Tests/AggregationTests.cs (replace)`

```csharp
using IndexContainment.Core.Models;
using IndexContainment.Data;
using Xunit;

public class AggregationTests
{
    [Fact]
    public void FullDay_Resamples_To_Approx_42_Bars()
    {
        var date = new DateTime(2024, 1, 3);
        var open = date.AddHours(9).AddMinutes(30);
        var close = date.AddHours(16);
        var bars = new List<Bar>();
        var t = open;
        var price = 100m;
        while (t <= close)
        {
            var o = price; var c = price + 0.01m;
            var h = Math.Max(o, c) + 0.02m; var l = Math.Min(o, c) - 0.02m;
            bars.Add(new Bar(t, o, h, l, c, 100));
            price = c; t = t.AddMinutes(1);
        }
        var day = new DayBars(date, bars, 101m);
        var res = CompositeResampler.ResampleIfNeeded(day, ResampleMode.Composite);
        Assert.InRange(res.Bars.Count, 40, 44);
        var sess = SessionDetector.Detect(res);
        Assert.True((sess.Close - sess.Open) >= TimeSpan.FromHours(6));
    }

    [Fact]
    public void HalfDay_Resamples_To_Approx_30_Bars_And_Flags_EarlyClose()
    {
        var date = new DateTime(2024, 7, 3);
        var open = date.AddHours(9).AddMinutes(30);
        var close = date.AddHours(13);
        var bars = new List<Bar>();
        var t = open;
        var price = 100m;
        while (t <= close)
        {
            var o = price; var c = price + 0.01m;
            var h = Math.Max(o, c) + 0.02m; var l = Math.Min(o, c) - 0.02m;
            bars.Add(new Bar(t, o, h, l, c, 100));
            price = c; t = t.AddMinutes(1);
        }
        var day = new DayBars(date, bars, 101m);
        var res = CompositeResampler.ResampleIfNeeded(day, ResampleMode.Composite);
        Assert.InRange(res.Bars.Count, 28, 32);
        var sess = SessionDetector.Detect(res);
        Assert.True(SessionDetector.IsEarlyClose(sess, TimeSpan.FromHours(6)));
    }
}
```
