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