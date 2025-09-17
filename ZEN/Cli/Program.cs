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

int Main(string[] args)
{
    string dataRoot = GetArg(args, "--data", "../DATA");
    string outPath  = GetArg(args, "--out",  $"../OUTPUT/IndexContainment_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    string symsArg  = GetArg(args, "--symbols", "");
    string anchorS  = GetArg(args, "--anchor",  "10:00");
    if (!TimeSpan.TryParse(anchorS, out var anchor)) anchor = new TimeSpan(10,0,0);

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
        try
        {
            var dir = Path.Combine(dataRoot, sym);
            var days = CsvLoader.LoadAll(sym, dir);
            var rows = DailyReducer.BuildRows(days, anchor);
            perSymbolRows[sym] = rows;
            Console.WriteLine($"[{sym}] days={rows.Count} (daily rows)");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[{sym}] ERROR: {ex.Message}");
            return 3;
        }
    }

    WorkbookWriter.Write(outPath, symbols, dataRoot, anchor, perSymbolRows);
    Console.WriteLine($"Wrote {outPath}");
    return 0;
}

return Main(args);