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