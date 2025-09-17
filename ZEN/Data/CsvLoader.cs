using System.Globalization;
using IndexContainment.Core.Models;

namespace IndexContainment.Data;

public static class CsvLoader
{
    public static IEnumerable<DayBars> LoadAll(string symbol, string dir)
    {
        if (!Directory.Exists(dir))
            throw new DirectoryNotFoundException(dir);

        var files = Directory.EnumerateFiles(dir, $"{symbol}_*.csv").OrderBy(p => p).ToList();
        if (files.Count == 0)
            throw new FileNotFoundException($"No CSVs for {symbol} in {dir}");

        var byDay = new SortedDictionary<DateTime, List<Bar>>();

        foreach (var path in files)
        {
            using var sr = new StreamReader(path);
            string? line = sr.ReadLine(); // header
            while ((line = sr.ReadLine()) != null)
            {
                var s = line.Split(',');
                var dt = DateTime.ParseExact($"{s[0]} {s[1]}", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                var b = new Bar(dt,
                    decimal.Parse(s[2], CultureInfo.InvariantCulture),
                    decimal.Parse(s[3], CultureInfo.InvariantCulture),
                    decimal.Parse(s[4], CultureInfo.InvariantCulture),
                    decimal.Parse(s[5], CultureInfo.InvariantCulture),
                    long.Parse(s[6], CultureInfo.InvariantCulture));
                var d = dt.Date;
                if (!byDay.TryGetValue(d, out var list)) byDay[d] = list = new List<Bar>();
                list.Add(b);
            }
        }

        decimal prevClose = 0m;
        foreach (var (date, bars0) in byDay)
        {
            var bars = bars0.OrderBy(b => b.T).ToList();
            if (bars.Count < 20) continue; // session guardrail

            var day = new DayBars(date, bars, prevClose);
            prevClose = bars.Last().C;
            yield return day;
        }
    }
}