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