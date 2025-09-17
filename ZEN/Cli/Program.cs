using IndexContainment.Data;
using IndexContainment.Data.Backfill;
using IndexContainment.Analysis;
using IndexContainment.ExcelIO;
using IndexContainment.Core.Models;
using IndexContainment.Export;

static string[] DiscoverSymbols(string root) =>
    Directory.Exists(root)
        ? Directory.GetDirectories(root).Select(Path.GetFileName).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray()
        : Array.Empty<string>();

static string GetArg(string[] args, string key, string def)
{
    int i = Array.IndexOf(args, key);
    return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : def;
}

static bool GetBool(string[] args, string key, bool def)
{
    var s = GetArg(args, key, def ? "true" : "false").ToLowerInvariant();
    return s is "1" or "true" or "yes" or "y";
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

    // ==== analytics + exports ====
    string dataRoot = GetArg(args, "--data", "../DATA");
    string outPath  = GetArg(args, "--out",  $"../OUTPUT/StrategyBook_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    string symsArg  = GetArg(args, "--symbols", "");
    string anchorS  = GetArg(args, "--anchor",  "10:00");
    string resampleS= GetArg(args, "--resample", "auto");

    string xlMode   = GetArg(args, "--xl-mode", "strategy"); // strategy|symbol|both
    bool emitDaily  = GetBool(args, "--emit-daily", true);
    bool emitSumm   = GetBool(args, "--emit-summaries", true);
    int topN        = int.TryParse(GetArg(args, "--exceptions-top", "25"), out var n) ? n : 25;

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

    // === Exports ===
    var outputs = new List<string>();
    var outputRoot = Path.GetDirectoryName(outPath) ?? "../OUTPUT";
    var dailyRoot = Path.Combine("..","DAILY");
    var summariesRoot = Path.Combine("..","SUMMARIES");
    var exceptionsRoot = Path.Combine("..","EXCEPTIONS");
    var symbolSheetsRoot = Path.Combine(outputRoot, "sheets");

    if (emitDaily)
    {
        foreach (var (sym, rows) in perSymbolRows)
            outputs.Add(DailyCsvWriter.Write(dailyRoot, sym, rows));
    }
    if (emitSumm)
    {
        foreach (var (sym, rows) in perSymbolRows)
            outputs.Add(SummaryJsonWriter.WriteSymbolSummary(summariesRoot, sym, rows));
        outputs.Add(SummaryJsonWriter.WriteLeaderboard(summariesRoot, perSymbolRows));
    }

    foreach (var (sym, rows) in perSymbolRows)
        outputs.Add(ExceptionsExporter.WriteWorstVR(exceptionsRoot, sym, rows, topN));

    // Excel
    if (xlMode is "strategy" or "both")
        outputs.Add(StrategyBookWriter.Write(outPath, symbols, dataRoot, anchor, perSymbolRows));
    if (xlMode is "symbol" or "both")
        foreach (var (sym, rows) in perSymbolRows)
            outputs.Add(SymbolBookWriter.Write(symbolSheetsRoot, sym, rows, anchor));

    // Manifest
    var meta = new Dictionary<string, object>
    {
        ["buildUtc"] = DateTime.UtcNow.ToString("u"),
        ["dataRoot"] = dataRoot,
        ["outPath"] = outPath,
        ["symbols"] = symbols,
        ["anchor"] = anchor.ToString(@"hh\:mm"),
        ["resample"] = mode.ToString(),
        ["xlMode"] = xlMode,
        ["emitDaily"] = emitDaily,
        ["emitSummaries"] = emitSumm,
        ["exceptionsTop"] = topN
    };
    outputs.Add(ManifestWriter.Write(outputRoot, meta, outputs));

    Console.WriteLine($"Wrote {outPath} and {outputs.Count} artifacts.");
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