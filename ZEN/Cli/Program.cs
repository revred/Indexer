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