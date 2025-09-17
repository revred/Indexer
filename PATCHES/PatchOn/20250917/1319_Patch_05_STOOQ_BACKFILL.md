# 1319_Patch_05_STOOQ_BACKFILL.md

This patch adds a **Stooq backfill path** (rate-limited, retry-friendly) and a CLI subcommand to fetch intraday data and write `DATA/<SYMBOL>/<SYMBOL>_YYYY.csv` in our schema. It keeps the rest of the pipeline unchanged (resampler, Excel, tests).

**Highlights**
- `stooq` provider: tolerant CSV parser (comma/semicolon; Date+Time combined or split), retries, throttle.
- One-shot fetch per symbol/interval, then **split by year** to our schema.
- Mapping `SPY→spy.us`, `QQQ→qqq.us`, etc. (editable JSON).
- CLI: `backfill stooq --symbols SPY,QQQ --interval 1 --out ../DATA` (interval minutes: 1,5,15,60).
- WPS/README updated with backfill instructions and limitations.


---

## Add/Update: `README.md (append Backfill section)`

```markdown
## Backfill (Stooq) — Quick Start

Fetch intraday bars from **Stooq** (public endpoints), then split into yearly CSVs our pipeline can read.

```bash
cd ZEN
dotnet build
dotnet run --project Cli -- backfill stooq   --symbols SPY,QQQ,IWM,DIA   --interval 1   --out ../DATA   --throttle-ms 1200   --retries 3
```

- `--interval` can be `1,5,15,60` (minutes). Our analytics will resample to the composite cadence (5m/15m/5m) with `--resample auto`.
- This is **best-effort**: Stooq intraday history depth varies and may not cover many years. Use IBKR for **incremental daily top-ups**.
```


---

## Add/Update: `WPS/WP2_Directory_Layout.md & WPS/WP3_Loader_Session_Guardrails.md (append backfill notes)`

```markdown
# WP2/WP3 — Backfill (Stooq) Notes

- Provider: `stooq` with tolerant CSV parsing and rate limiting.
- One request per symbol/interval; data is then split by calendar year into `DATA/<SYMBOL>/<SYMBOL>_YYYY.csv`.
- Expect limited historical depth; treat as seed data and validate with `OUTPUT/Integrity_<SYMBOL>.txt`.
```


---

## Add/Update: `ZEN/Data/Providers/IPriceProvider.cs (new)`

```csharp
namespace IndexContainment.Data.Providers;

using IndexContainment.Core.Models;

public interface IPriceProvider
{
    /// <summary>
    /// Fetch intraday bars for a symbol and interval (minutes).
    /// Implementations may ignore date ranges if endpoint returns a fixed span.
    /// Bars must be returned in chronological order and use exchange-local time.
    /// </summary>
    Task<IReadOnlyList<Bar>> GetIntradayAsync(string symbol, int intervalMinutes, CancellationToken ct = default);
}
```


---

## Add/Update: `ZEN/Data/SimpleRateLimiter.cs (new)`

```csharp
namespace IndexContainment.Data;

public sealed class SimpleRateLimiter
{
    private readonly TimeSpan _delay;
    private DateTime _next = DateTime.MinValue;

    public SimpleRateLimiter(TimeSpan delay) => _delay = delay;

    public async Task WaitAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        if (now < _next)
        {
            var wait = _next - now;
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, ct);
        }
        _next = DateTime.UtcNow + _delay;
    }
}
```


---

## Add/Update: `ZEN/Data/SymbolMaps/StooqMap.json (new)`

```json
{
  "SPY": "spy.us",
  "QQQ": "qqq.us",
  "DIA": "dia.us",
  "IWM": "iwm.us"
}
```


---

## Add/Update: `ZEN/Data/Providers/StooqProvider.cs (new)`

```csharp
using System.Net.Http.Headers;
using System.Text;
using IndexContainment.Core.Models;
using IndexContainment.Data.Providers;

namespace IndexContainment.Data.Stooq;

public sealed class StooqProvider : IPriceProvider, IDisposable
{
    private readonly HttpClient _http;
    private readonly IDictionary<string,string> _map;
    private readonly SimpleRateLimiter _limiter;
    private readonly int _retries;

    // Base template: many stooq mirrors expose intraday via a path like:
    //   q/d/l/?s=<symbol>&i=<interval>
    // We keep this configurable; default below works for common mirrors.
    private readonly string _baseTemplate;

    public StooqProvider(IDictionary<string,string> map, TimeSpan throttle, int retries = 3, string? baseTemplate = null, HttpMessageHandler? handler = null)
    {
        _map = map;
        _limiter = new SimpleRateLimiter(throttle);
        _retries = Math.Max(0, retries);
        _baseTemplate = baseTemplate ?? "https://stooq.com/q/d/l/?s={SYMBOL}&i={INTERVAL}";
        _http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Indexer", "1.0"));
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public void Dispose() => _http.Dispose();

    public async Task<IReadOnlyList<Bar>> GetIntradayAsync(string symbol, int intervalMinutes, CancellationToken ct = default)
    {
        if (!_map.TryGetValue(symbol, out var stooqSym))
            stooqSym = symbol.ToLowerInvariant() + ".us"; // naive fallback

        var url = _baseTemplate.Replace("{SYMBOL}", stooqSym).Replace("{INTERVAL}", intervalMinutes.ToString());
        var attempt = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            await _limiter.WaitAsync(ct);
            try
            {
                using var resp = await _http.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode)
                    throw new HttpRequestException($"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}");

                var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
                var text = DetectEncodingAndDecode(bytes);
                var bars = ParseCsv(text);
                // Ensure chronological
                bars.Sort((a,b) => a.T.CompareTo(b.T));
                return bars;
            }
            catch (Exception ex) when (attempt < _retries)
            {
                attempt++;
                await Task.Delay(TimeSpan.FromSeconds(1 * Math.Pow(2, attempt)), ct);
                continue;
            }
        }
    }

    private static string DetectEncodingAndDecode(byte[] bytes)
    {
        // Stooq often serves ASCII/UTF-8; be liberal
        try { return Encoding.UTF8.GetString(bytes); } catch { }
        return Encoding.Latin1.GetString(bytes);
    }

    private static List<Bar> ParseCsv(string text)
    {
        // Accept both ';' and ',' separators; detect header row.
        // Columns may be: Date,Time,Open,High,Low,Close,Volume
        // or Date,Open,High,Low,Close,Volume with time embedded in Date for intraday.
        var lines = text.Split(new[] {'\r','\n'}, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return new List<Bar>();

        var sep = lines[0].Contains(';') ? ';' : ',';
        int start = 0;
        var headers = lines[0].Split(sep);
        bool hasHeader = headers.Any(h => h.Equals("Date", StringComparison.OrdinalIgnoreCase) || h.Equals("TIME", StringComparison.OrdinalIgnoreCase));
        if (hasHeader) start = 1;

        var outBars = new List<Bar>(Math.Max(0, lines.Length - start));
        for (int i = start; i < lines.Length; i++)
        {
            var s = lines[i].Split(sep);
            if (s.Length < 6) continue;

            DateTime? dt = null;
            decimal o,h,l,c; long v;

            // Case A: Date + Time separate
            if (s.Length >= 7 && TryParseDateTime(s[0], s[1], out var dt1))
                dt = dt1;
            // Case B: single DateTime field
            else if (TryParseDateTimeCombined(s[0], out var dt2))
                dt = dt2;

            if (dt is null) continue;

            if (!decimal.TryParse(s[s.Length-5], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out o)) continue;
            if (!decimal.TryParse(s[s.Length-4], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out h)) continue;
            if (!decimal.TryParse(s[s.Length-3], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out l)) continue;
            if (!decimal.TryParse(s[s.Length-2], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out c)) continue;
            if (!long.TryParse(s[s.Length-1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out v)) v = 0;

            // Basic sanity
            var lo = Math.Min(Math.Min(o,h), Math.Min(l,c));
            var hi = Math.Max(Math.Max(o,h), Math.Max(l,c));
            if (l < lo - 1000 || h > hi + 1000) continue;

            outBars.Add(new Bar(dt.Value, o,h,l,c,v));
        }
        return outBars;
    }

    private static bool TryParseDateTime(string date, string time, out DateTime dt)
    {
        // Accept yyyy-MM-dd and yyyyMMdd, Time HH:mm or HH:mm:ss
        var d = date.Trim();
        var t = time.Trim();
        string[] dateFormats = { "yyyy-MM-dd", "yyyyMMdd" };
        string[] timeFormats = { "HH:mm", "HH:mm:ss" };
        foreach (var df in dateFormats)
            if (DateTime.TryParseExact(d + " " + t, df + " " + "HH:mm", null, System.Globalization.DateTimeStyles.None, out dt))
                return true;
        // second pass with seconds
        foreach (var df in dateFormats)
            if (DateTime.TryParseExact(d + " " + t, df + " " + "HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out dt))
                return true;
        dt = default;
        return false;
    }

    private static bool TryParseDateTimeCombined(string dateTime, out DateTime dt)
    {
        var s = dateTime.Trim();
        string[] fmts = { "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyyMMdd HH:mm", "yyyyMMdd HH:mm:ss" };
        foreach (var f in fmts)
            if (DateTime.TryParseExact(s, f, null, System.Globalization.DateTimeStyles.None, out dt))
                return true;
        dt = default;
        return false;
    }
}
```


---

## Add/Update: `ZEN/Data/Backfill/BackfillRunner.cs (new)`

```csharp
using IndexContainment.Core.Models;
using IndexContainment.Data.Providers;
using System.Text;

namespace IndexContainment.Data.Backfill;

public static class BackfillRunner
{
    /// <summary>
    /// Fetch once for a symbol/interval and split into yearly CSVs in our schema.
    /// </summary>
    public static async Task<int> RunStooqAsync(string dataRoot, string symbol, int intervalMinutes, TimeSpan throttle, int retries, CancellationToken ct = default)
    {
        // Load symbol map
        var mapPath = Path.Combine(AppContext.BaseDirectory, "Data", "SymbolMaps", "StooqMap.json");
        if (!File.Exists(mapPath))
        {
            // also check repo-relative path
            var alt = Path.Combine("..","ZEN","Data","SymbolMaps","StooqMap.json");
            if (File.Exists(alt)) mapPath = alt;
        }
        var map = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(mapPath))
        {
            var json = await File.ReadAllTextAsync(mapPath, ct);
            map = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string,string>>(json) ?? map;
        }

        using var provider = new IndexContainment.Data.Stooq.StooqProvider(map, throttle, retries);
        var bars = await provider.GetIntradayAsync(symbol, intervalMinutes, ct);

        if (bars.Count == 0) return 0;

        var byYear = bars.GroupBy(b => b.T.Year).OrderBy(g => g.Key);
        int files = 0;
        foreach (var g in byYear)
        {
            var dir = Path.Combine(dataRoot, symbol);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{symbol}_{g.Key}.csv");
            await WriteYearAsync(path, g.ToList(), ct);
            files++;
        }
        return files;
    }

    private static async Task WriteYearAsync(string path, List<Bar> bars, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Date,Time,Open,High,Low,Close,Volume");
        foreach (var b in bars)
        {
            sb.Append(b.T.ToString("yyyy-MM-dd")).Append(',')
              .Append(b.T.ToString("HH:mm")).Append(',')
              .Append(b.O.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
              .Append(b.H.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
              .Append(b.L.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
              .Append(b.C.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
              .Append(b.V.ToString(System.Globalization.CultureInfo.InvariantCulture)).AppendLine();
        }
        await File.WriteAllTextAsync(path, sb.ToString(), ct);
    }
}
```


---

## Add/Update: `ZEN/Cli/Program.cs (replace with backfill support)`

```csharp
using IndexContainment.Data;
using IndexContainment.Data.Backfill;
using IndexContainment.Data.Providers;
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
    if (args.Length > 0 && args[0].Equals("backfill", StringComparison.OrdinalIgnoreCase))
        return BackfillMain(args.Skip(1).ToArray());

    // === existing analytics path ===
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

    IndexContainment.ExcelIO.WorkbookWriter.Write(outPath, symbols, dataRoot, anchor, perSymbolRows);
    Console.WriteLine($"Wrote {outPath}");
    return 0;
}

int BackfillMain(string[] args)
{
    if (args.Length > 0 && args[0].Equals("stooq", StringComparison.OrdinalIgnoreCase))
    {
        string outRoot = GetArg(args, "--out", "../DATA");
        string symsArg = GetArg(args, "--symbols", "SPY,QQQ,IWM,DIA");
        string intervalS = GetArg(args, "--interval", "1");
        string throttleS = GetArg(args, "--throttle-ms", "1200");
        string retriesS  = GetArg(args, "--retries", "3");

        if (!int.TryParse(intervalS, out var interval)) interval = 1;
        if (!int.TryParse(throttleS, out var throttleMs)) throttleMs = 1200;
        if (!int.TryParse(retriesS, out var retries)) retries = 3;

        var symbols = symsArg.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        int files = 0;
        foreach (var sym in symbols)
        {
            Console.WriteLine($"[stooq] fetching {sym} interval={interval}m ...");
            files += BackfillRunner.RunStooqAsync(outRoot, sym, interval, TimeSpan.FromMilliseconds(throttleMs), retries).GetAwaiter().GetResult();
        }
        Console.WriteLine($"[stooq] wrote {files} yearly files.");
        return 0;
    }

    Console.Error.WriteLine("Usage: backfill stooq --symbols SPY,QQQ --interval 1 --out ../DATA [--throttle-ms 1200] [--retries 3]");
    return 2;
}

return Main(args);
```


---

## Add/Update: `ZEN/Tests/StooqParserTests.cs (new)`

```csharp
using IndexContainment.Data.Stooq;
using IndexContainment.Data.Providers;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IndexContainment.Core.Models;
using Xunit;

public class StooqParserTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly string _payload;
        public FakeHandler(string payload) => _payload = payload;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var msg = new HttpResponseMessage(HttpStatusCode.OK);
            msg.Content = new StringContent(_payload);
            return Task.FromResult(msg);
        }
    }

    [Fact]
    public async Task Parses_Comma_Separated_With_Header()
    {
        var csv = "Date,Time,Open,High,Low,Close,Volume\n" +
                  "2024-01-03,09:35,100,101,99,100.5,1000\n";
        var map = new Dictionary<string,string> { { "SPY", "spy.us" } };
        using var prov = new StooqProvider(map, TimeSpan.Zero, 0, handler: new FakeHandler(csv));
        var bars = await prov.GetIntradayAsync("SPY", 1);
        Assert.Single(bars);
        Assert.Equal(new DateTime(2024,1,3,9,35,0), bars[0].T);
    }

    [Fact]
    public async Task Parses_Semicolon_Separated_With_Split_Date_Time()
    {
        var csv = "DATE;TIME;OPEN;HIGH;LOW;CLOSE;VOLUME\n" +
                  "20240103;09:35;100;101;99;100.5;1000\n";
        var map = new Dictionary<string,string> { { "SPY", "spy.us" } };
        using var prov = new StooqProvider(map, TimeSpan.Zero, 0, handler: new FakeHandler(csv));
        var bars = await prov.GetIntradayAsync("SPY", 1);
        Assert.Single(bars);
        Assert.Equal(new DateTime(2024,1,3,9,35,0), bars[0].T);
    }
}
```
