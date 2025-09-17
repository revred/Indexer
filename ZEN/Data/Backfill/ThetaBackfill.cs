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