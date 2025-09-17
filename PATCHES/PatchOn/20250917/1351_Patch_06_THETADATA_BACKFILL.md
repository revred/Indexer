# 1351_Patch_06_THETADATA_BACKFILL.md

This patch adds a **ThetaData** backfill that talks to your locally running **Theta Terminal v3** (HTTP). It introduces a `ThetaDataProvider`, a `backfill theta` CLI subcommand, default config under `MARKET/`, and tests. Yearly CSVs are written in our canonical schema (`Date,Time,Open,High,Low,Close,Volume`).


---

## Add/Update: `README.md (append ThetaData backfill section)`

```markdown
## Backfill (ThetaData) — Quick Start

**Prereqs**: Run **Theta Terminal v3** locally (default host `localhost`, port `25503`) with your `INDEX_PRO` entitlements.

```bash
cd ZEN
dotnet build
dotnet run --project Cli -- backfill theta   --symbols SPX,NDX,SPY,QQQ   --from 2004-01-01   --to 2025-09-17   --interval 1m   --out ../DATA   --throttle-ms 250   --retries 3
```

- Enumerates trade dates via `/v3/index/list/dates?symbol=...`.
- Fetches OHLC via `/v3/index/history/ohlc?symbol=...&start_date=YYYYMMDD&end_date=YYYYMMDD&interval=1m&format=csv`.
- Writes `DATA/<SYMBOL>/<SYMBOL>_YYYY.csv` → then run our analytics/resampler:
  ```bash
  dotnet run --project Cli --     --data ../DATA     --out ../OUTPUT/IndexContainment.xlsx     --symbols SPX,NDX,SPY,QQQ     --anchor 10:00     --resample auto
  ```

Defaults are in `MARKET/theta.config.json` (host, port, format, throttle, retries) and can be overridden by flags.
```


---

## Add/Update: `MARKET/theta.config.json (new)`

```json
{
  "host": "localhost",
  "port": 25503,
  "format": "csv",
  "throttle_ms": 250,
  "retries": 3,
  "timezone": "America/New_York"
}
```


---

## Add/Update: `ZEN/Data/Providers/ThetaDataProvider.cs (new)`

```csharp
using System.Net.Http.Headers;
using System.Text;
using System.Globalization;
using IndexContainment.Core.Models;
using IndexContainment.Data.Providers;

namespace IndexContainment.Data.Theta;

public sealed class ThetaDataProvider : IPriceProvider, IDisposable
{
    private readonly HttpClient _http;
    private readonly string _baseUrl; // e.g., http://localhost:25503
    private readonly string _format;  // csv|json|ndjson
    private readonly int _retries;
    private readonly SimpleRateLimiter _limiter;

    public ThetaDataProvider(string host = "localhost", int port = 25503, string format = "csv", int throttleMs = 250, int retries = 3, HttpMessageHandler? handler = null)
    {
        _baseUrl = $"http://{host}:{port}/v3";
        _format = format;
        _retries = Math.Max(0, retries);
        _limiter = new SimpleRateLimiter(TimeSpan.FromMilliseconds(Math.Max(0, throttleMs)));
        _http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Indexer", "1.0"));
        _http.Timeout = TimeSpan.FromSeconds(60);
    }

    public void Dispose() => _http.Dispose();

    public async Task<IReadOnlyList<Bar>> GetIntradayAsync(string symbol, int intervalMinutes, CancellationToken ct = default)
    {
        var dates = await GetAvailableDatesAsync(symbol, ct);
        var list = new List<Bar>(1024);

        foreach (var d in dates)
        {
            ct.ThrowIfCancellationRequested();
            await _limiter.WaitAsync(ct);
            var attempt = 0;
            while (true)
            {
                try
                {
                    var url = $"{_baseUrl}/index/history/ohlc?symbol={Uri.EscapeDataString(symbol)}&start_date={d:yyyyMMdd}&end_date={d:yyyyMMdd}&interval={IntervalString(intervalMinutes)}&format={_format}";
                    using var resp = await _http.GetAsync(url, ct);
                    if (!resp.IsSuccessStatusCode)
                        throw new HttpRequestException($"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase} for {url}");

                    var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
                    var text = Encoding.UTF8.GetString(bytes);
                    var bars = ParseHistoryCsv(text);
                    list.AddRange(bars);
                    break;
                }
                catch (Exception) when (attempt < _retries)
                {
                    attempt++;
                    await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt * attempt), ct);
                }
            }
        }

        list.Sort((a,b) => a.T.CompareTo(b.T));
        return list;
    }

    private async Task<List<DateTime>> GetAvailableDatesAsync(string symbol, CancellationToken ct)
    {
        var outList = new List<DateTime>();
        var attempt = 0;
        while (true)
        {
            try
            {
                var url = $"{_baseUrl}/index/list/dates?symbol={Uri.EscapeDataString(symbol)}&format=csv";
                using var resp = await _http.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode)
                    throw new HttpRequestException($"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase} for {url}");
                var text = await resp.Content.ReadAsStringAsync(ct);
                var lines = text.Split(new[]{'\r','\n'}, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length == 0) return outList;
                int start = lines[0].ToLowerInvariant().Contains("symbol") ? 1 : 0;
                for (int i = start; i < lines.Length; i++)
                {
                    var parts = lines[i].Split(',');
                    if (parts.Length >= 2 && DateTime.TryParseExact(parts[1].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                        outList.Add(d);
                }
                break;
            }
            catch (Exception) when (attempt < _retries)
            {
                attempt++;
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt * attempt), ct);
            }
        }
        outList.Sort();
        return outList;
    }

    private static string IntervalString(int minutes) => minutes switch
    {
        1 => "1m",
        5 => "5m",
        10 => "10m",
        15 => "15m",
        30 => "30m",
        60 => "1h",
        _ => "1m"
    };

    // Parse v3 OHLC CSV: timestamp,open,high,low,close,volume,count,vwap
    private static List<Bar> ParseHistoryCsv(string csv)
    {
        var list = new List<Bar>();
        var lines = csv.Split(new[]{'\r','\n'}, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return list;
        int start = lines[0].ToLowerInvariant().Contains("timestamp") ? 1 : 0;

        for (int i = start; i < lines.Length; i++)
        {
            var s = lines[i].Split(',');
            if (s.Length < 5) continue;
            if (!DateTime.TryParse(s[0], CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var ts))
                continue;
            if (!decimal.TryParse(s[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var o)) continue;
            if (!decimal.TryParse(s[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var h)) continue;
            if (!decimal.TryParse(s[3], NumberStyles.Any, CultureInfo.InvariantCulture, out var l)) continue;
            if (!decimal.TryParse(s[4], NumberStyles.Any, CultureInfo.InvariantCulture, out var c)) continue;
            long v = 0;
            if (s.Length > 5) long.TryParse(s[5], NumberStyles.Any, CultureInfo.InvariantCulture, out v);
            list.Add(new Bar(ts, o, h, l, c, v));
        }
        return list;
    }
}
```


---

## Add/Update: `ZEN/Data/Backfill/ThetaBackfill.cs (new)`

```csharp
using System.Globalization;
using System.Text;
using IndexContainment.Core.Models;

namespace IndexContainment.Data.Backfill;

public static class ThetaBackfill
{
    public static async Task<int> RunAsync(
        string outRoot,
        string symbolCsv,
        DateTime? from,
        DateTime? to,
        int intervalMinutes,
        string host,
        int port,
        string format,
        int throttleMs,
        int retries,
        CancellationToken ct = default)
    {
        var symbols = symbolCsv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        Directory.CreateDirectory(outRoot);

        int files = 0;
        foreach (var sym in symbols)
        {
            using var prov = new IndexContainment.Data.Theta.ThetaDataProvider(host, port, format, throttleMs, retries);
            var all = await prov.GetIntradayAsync(sym, intervalMinutes, ct);

            if (from.HasValue) all = all.Where(b => b.T.Date >= from.Value.Date).ToList();
            if (to.HasValue)   all = all.Where(b => b.T.Date <= to.Value.Date).ToList();

            var byYear = all.GroupBy(b => b.T.Year).OrderBy(g => g.Key);
            foreach (var g in byYear)
            {
                var dir = Path.Combine(outRoot, sym);
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, $"{sym}_{g.Key}.csv");
                await WriteYearAsync(path, g.ToList(), ct);
                files++;
            }
        }
        return files;
    }

    private static async Task WriteYearAsync(string path, List<Bar> bars, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Date,Time,Open,High,Low,Close,Volume");
        foreach (var b in bars.OrderBy(x => x.T))
        {
            sb.Append(b.T.ToString("yyyy-MM-dd")).Append(',')
              .Append(b.T.ToString("HH:mm")).Append(',')
              .Append(b.O.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(b.H.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(b.L.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(b.C.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(b.V.ToString(CultureInfo.InvariantCulture)).AppendLine();
        }
        await File.WriteAllTextAsync(path, sb.ToString(), ct);
    }
}
```


---

## Add/Update: `ZEN/Cli/Program.cs (replace with Theta backfill support)`

```csharp
using IndexContainment.Data;
using IndexContainment.Data.Backfill;
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

    // Analytics path
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
    if (args.Length > 0 && args[0].Equals("theta", StringComparison.OrdinalIgnoreCase))
    {
        string outRoot = GetArg(args, "--out", "../DATA");
        string symsArg = GetArg(args, "--symbols", "SPX,NDX,SPY,QQQ");
        string fromS   = GetArg(args, "--from", "");
        string toS     = GetArg(args, "--to", "");
        string interval= GetArg(args, "--interval", "1m");
        string host    = GetArg(args, "--host", "localhost");
        string portS   = GetArg(args, "--port", "25503");
        string format  = GetArg(args, "--format", "csv");
        string throttleS = GetArg(args, "--throttle-ms", "250");
        string retriesS  = GetArg(args, "--retries", "3");

        int intervalMin = 1;
        if (interval.EndsWith("m") && int.TryParse(interval.TrimEnd('m'), out var m)) intervalMin = m;
        else if (interval == "1h") intervalMin = 60;

        int port = int.TryParse(portS, out var p) ? p : 25503;
        int throttleMs = int.TryParse(throttleS, out var tm) ? tm : 250;
        int retries = int.TryParse(retriesS, out var r) ? r : 3;

        DateTime? from = DateTime.TryParse(fromS, out var fdt) ? fdt : (DateTime?)null;
        DateTime? to   = DateTime.TryParse(toS,   out var tdt) ? tdt : (DateTime?)null;

        Console.WriteLine($"[theta] host={host} port={port} fmt={format} symbols={symsArg} from={(from?.ToString("yyyy-MM-dd") ?? "(min)")} to={(to?.ToString("yyyy-MM-dd") ?? "(max)")} interval={intervalMin}m");

        int files = ThetaBackfill.RunAsync(outRoot, symsArg, from, to, intervalMin, host, port, format, throttleMs, retries).GetAwaiter().GetResult();
        Console.WriteLine($"[theta] wrote {files} yearly files.");
        return 0;
    }

    Console.Error.WriteLine("Usage: backfill theta --symbols SPX,NDX --from 2004-01-01 --to 2025-09-17 --interval 1m --out ../DATA [--host localhost --port 25503 --format csv --throttle-ms 250 --retries 3]");
    return 2;
}

return Main(args);
```


---

## Add/Update: `ZEN/Tests/ThetaParserTests.cs (new)`

```csharp
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IndexContainment.Data.Theta;
using Xunit;

public class ThetaParserTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly string _datesCsv;
        private readonly string _histCsv;
        public FakeHandler(string datesCsv, string histCsv) { _datesCsv = datesCsv; _histCsv = histCsv; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            var msg = new HttpResponseMessage(HttpStatusCode.OK);
            if (url.Contains("/index/list/dates"))
                msg.Content = new StringContent(_datesCsv);
            else if (url.Contains("/index/history/ohlc"))
                msg.Content = new StringContent(_histCsv);
            else
                msg.Content = new StringContent("");
            return Task.FromResult(msg);
        }
    }

    [Fact]
    public async Task Parses_V3_Csv_OHLC_And_Splits_By_Dates()
    {
        var datesCsv = "symbol,date\nSPX,2024-01-03\nSPX,2024-01-04\n";
        var histCsv  = "timestamp,open,high,low,close,volume,count,vwap\n" +
                       "2024-01-03T09:30:00,100,101,99,100.5,123,10,100.2\n" +
                       "2024-01-04T09:30:00,101,102,100,101.5,124,11,101.7\n";

        using var prov = new ThetaDataProvider("localhost", 25503, "csv", 0, 0, new FakeHandler(datesCsv, histCsv));
        var bars = await prov.GetIntradayAsync("SPX", 1);
        Assert.Equal(2, bars.Count);
        Assert.Equal(new DateTime(2024,1,3,9,30,0), bars[0].T);
        Assert.Equal(100m, bars[0].O);
        Assert.Equal(101.5m, bars[1].C);
    }
}
```


---

## Add/Update: `WPS/WP3_Loader_Session_Guardrails.md (append ThetaData provider note)`

```markdown
# WP3 — Backfill Providers (ThetaData)

**ThetaData (preferred for indices):**
- Uses local **Theta Terminal v3** endpoints:
  - `/v3/index/list/dates?symbol=SYMBOL` to enumerate available dates.
  - `/v3/index/history/ohlc?symbol=SYMBOL&start_date=YYYYMMDD&end_date=YYYYMMDD&interval=1m` to fetch intraday OHLC.
- Writes to `DATA/<SYMBOL>/<SYMBOL>_YYYY.csv` in the canonical schema.
- Throttling and retries are configurable; defaults live in `MARKET/theta.config.json`.

If the terminal isn’t running, the backfill command fails fast—start it and re-run.
```
