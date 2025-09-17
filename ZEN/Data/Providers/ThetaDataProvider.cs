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