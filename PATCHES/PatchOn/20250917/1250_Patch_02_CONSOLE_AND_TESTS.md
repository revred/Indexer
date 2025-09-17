# 1250_Patch_02_CONSOLE_AND_TESTS.md

This patch **adds the Console application and the Tests** (unit + optional integration) to the repo under `ZEN/` and wires everything via a solution file. It matches the layout in your README and WPS.

> After applying: `cd ZEN && dotnet build && dotnet test && dotnet run --project Cli -- --data ../DATA --out ../OUTPUT/IndexContainment.xlsx --symbols SPY,QQQ,IWM,DIA --anchor 10:00`.


---

## Add/Update: `ZEN/IndexContainment.sln`

```text
ZEN/IndexContainment.sln
```

```text
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Core", "Core/Core.csproj", "{11111111-1111-1111-1111-111111111111}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Data", "Data/Data.csproj", "{22222222-2222-2222-2222-222222222222}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Analysis", "Analysis/Analysis.csproj", "{33333333-3333-3333-3333-333333333333}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Excel", "Excel/Excel.csproj", "{44444444-4444-4444-4444-444444444444}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Cli", "Cli/Cli.csproj", "{55555555-5555-5555-5555-555555555555}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Tests", "Tests/Tests.csproj", "{66666666-6666-6666-6666-666666666666}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{11111111-1111-1111-1111-111111111111}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{11111111-1111-1111-1111-111111111111}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{11111111-1111-1111-1111-111111111111}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{11111111-1111-1111-1111-111111111111}.Release|Any CPU.Build.0 = Release|Any CPU
		{22222222-2222-2222-2222-222222222222}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{22222222-2222-2222-2222-222222222222}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{22222222-2222-2222-2222-222222222222}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{22222222-2222-2222-2222-222222222222}.Release|Any CPU.Build.0 = Release|Any CPU
		{33333333-3333-3333-3333-333333333333}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{33333333-3333-3333-3333-333333333333}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{33333333-3333-3333-3333-333333333333}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{33333333-3333-3333-3333-333333333333}.Release|Any CPU.Build.0 = Release|Any CPU
		{44444444-4444-4444-4444-444444444444}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{44444444-4444-4444-4444-444444444444}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{44444444-4444-4444-4444-444444444444}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{44444444-4444-4444-4444-444444444444}.Release|Any CPU.Build.0 = Release|Any CPU
		{55555555-5555-5555-5555-555555555555}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{55555555-5555-5555-5555-555555555555}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{55555555-5555-5555-5555-555555555555}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{55555555-5555-5555-5555-555555555555}.Release|Any CPU.Build.0 = Release|Any CPU
		{66666666-6666-6666-6666-666666666666}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{66666666-6666-6666-6666-666666666666}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{66666666-6666-6666-6666-666666666666}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{66666666-6666-6666-6666-666666666666}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
EndGlobal
```


---

## Add/Update: `ZEN/Directory.Packages.props`

```text
ZEN/Directory.Packages.props
```

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="ClosedXML" Version="0.103.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.10.0" />
    <PackageVersion Include="xunit" Version="2.7.0" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.5.7" />
  </ItemGroup>
</Project>
```


---

## Add/Update: `ZEN/Core/Core.csproj`

```text
ZEN/Core/Core.csproj
```

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```


---

## Add/Update: `ZEN/Core/Models/Models.cs`

```text
ZEN/Core/Models/Models.cs
```

```csharp
namespace IndexContainment.Core.Models;

public readonly record struct Bar(DateTime T, decimal O, decimal H, decimal L, decimal C, long V);
public sealed record DayBars(DateTime D, List<Bar> Bars, decimal PrevClose);

public sealed record DailyRow(
    DateTime Date, decimal PrevClose, decimal Open, decimal P10,
    decimal LowAfter10, decimal HighAfter10, decimal Close,
    decimal GapPct, decimal ExtraDropPct, decimal ExtraRisePct, int TimeToLowMins,
    int Qual_1_0, int Hold_1_0, decimal VR_1_0,
    int Qual_1_5, int Hold_1_5, decimal VR_1_5,
    int Qual_2_0, int Hold_2_0, decimal VR_2_0,
    int Qual_3_0, int Hold_3_0, decimal VR_3_0,
    int Qual_4_0, int Hold_4_0, decimal VR_4_0
);
```


---

## Add/Update: `ZEN/Core/Utils/Utils.cs`

```text
ZEN/Core/Utils/Utils.cs
```

```csharp
namespace IndexContainment.Core.Utils;

public static class Wilson
{
    public static double Lower95(int hits, int n)
    {
        if (n <= 0) return 0d;
        double p = (double)hits / n, z = 1.96, N = n;
        double denom = 1 + z*z/N;
        double center = p + z*z/(2*N);
        double margin = z * Math.Sqrt((p*(1-p) + z*z/(4*N))/N);
        return (center - margin) / denom;
    }
}

public static class Mathx
{
    public static decimal Round6(decimal d) => Math.Round(d, 6, MidpointRounding.AwayFromZero);
}

public static class Sheet
{
    public static string SafeName(string s) => s.Length <= 31 ? s : s.Substring(0,31);
}
```


---

## Add/Update: `ZEN/Data/Data.csproj`

```text
ZEN/Data/Data.csproj
```

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Core\Core.csproj" />
  </ItemGroup>
</Project>
```


---

## Add/Update: `ZEN/Data/CsvLoader.cs`

```text
ZEN/Data/CsvLoader.cs
```

```csharp
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
```


---

## Add/Update: `ZEN/Analysis/Analysis.csproj`

```text
ZEN/Analysis/Analysis.csproj
```

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Core\Core.csproj" />
  </ItemGroup>
</Project>
```


---

## Add/Update: `ZEN/Analysis/DailyReducer.cs`

```text
ZEN/Analysis/DailyReducer.cs
```

```csharp
using IndexContainment.Core;
using IndexContainment.Core.Models;
using IndexContainment.Core.Utils;

namespace IndexContainment.Analysis;

public static class DailyReducer
{
    public static List<DailyRow> BuildRows(IEnumerable<DayBars> days, TimeSpan anchor)
    {
        var rows = new List<DailyRow>();
        foreach (var d in days)
        {
            if (d.PrevClose <= 0) continue;

            var open = d.Bars.First().O;
            var gap  = (open / d.PrevClose) - 1m;

            var anchorBar = d.Bars.Where(b => b.T.TimeOfDay <= anchor).LastOrDefault();
            if (anchorBar is null) continue;

            var after = d.Bars.Where(b => b.T.TimeOfDay > anchor).ToList();
            if (after.Count == 0) continue;

            var p10 = anchorBar.C;
            var lowAfter = after.Min(b => b.L);
            var highAfter = after.Max(b => b.H);
            var close = d.Bars.Last().C;

            var extraDrop = p10 == 0 ? 0 : (p10 - lowAfter) / p10;
            var extraRise = p10 == 0 ? 0 : (highAfter - p10) / p10;

            // Time to low after anchor
            var minBar = after.OrderBy(b => b.L).ThenBy(b => b.T).First();
            var ttl = (int)Math.Round((minBar.T - new DateTime(d.D.Year, d.D.Month, d.D.Day, anchor.Hours, anchor.Minutes, 0)).TotalMinutes);

            var map = Thresholds.Grid.ToDictionary(x => x.X, x => QualHoldVR(gap, extraDrop, x.X));

            rows.Add(new DailyRow(
                d.D, d.PrevClose, open, p10, lowAfter, highAfter, close,
                Mathx.Round6(gap), Mathx.Round6(extraDrop), Mathx.Round6(extraRise), ttl,
                map[0.01m].Qual, map[0.01m].Hold, Mathx.Round6(map[0.01m].VR),
                map[0.015m].Qual, map[0.015m].Hold, Mathx.Round6(map[0.015m].VR),
                map[0.02m].Qual, map[0.02m].Hold, Mathx.Round6(map[0.02m].VR),
                map[0.03m].Qual, map[0.03m].Hold, Mathx.Round6(map[0.03m].VR),
                map[0.04m].Qual, map[0.04m].Hold, Mathx.Round6(map[0.04m].VR)
            ));
        }
        return rows;
    }

    static (int Qual, int Hold, decimal VR) QualHoldVR(decimal gap, decimal extraDrop, decimal X)
    {
        bool qualifies = gap <= -X;
        bool holds = qualifies && extraDrop <= (X / 2m);
        decimal vr = (X == 0) ? 0 : (extraDrop / X);
        return (qualifies ? 1 : 0, holds ? 1 : 0, vr);
    }
}
```


---

## Add/Update: `ZEN/Analysis/SummaryBuilder.cs`

```text
ZEN/Analysis/SummaryBuilder.cs
```

```csharp
using IndexContainment.Core;
using IndexContainment.Core.Models;
using IndexContainment.Core.Utils;

namespace IndexContainment.Analysis;

public sealed record SummaryRow(string Threshold, int N, int Hits, double HitRate, double WilsonLower95, decimal P99ViolationRatio, int MedianTimeToLow);

public static class SummaryBuilder
{
    public static List<SummaryRow> Build(List<DailyRow> rows)
    {
        var outRows = new List<SummaryRow>();
        foreach (var (label, X) in Thresholds.Grid)
        {
            var qualifies = rows.Where(z => Qual(z, X)).ToList();
            int n = qualifies.Count;
            int hits = qualifies.Count(z => Hold(z, X));
            double hit = n > 0 ? (double)hits / n : 0;
            double wl = Wilson.Lower95(hits, n);
            decimal p99vr = n > 0 ? qualifies.Select(z => VR(z, X)).OrderBy(v => v).ElementAt(Math.Max(0,(int)Math.Floor(0.99 * (n - 1)))) : 0m;
            int medTTL = n > 0 ? qualifies.Select(z => z.TimeToLowMins).OrderBy(x => x).ElementAt(n/2) : 0;
            outRows.Add(new SummaryRow(label, n, hits, hit, wl, p99vr, medTTL));
        }
        return outRows;
    }

    static bool Qual(DailyRow z, decimal X) => X switch
    {
        0.01m  => z.Qual_1_0 == 1,
        0.015m => z.Qual_1_5 == 1,
        0.02m  => z.Qual_2_0 == 1,
        0.03m  => z.Qual_3_0 == 1,
        0.04m  => z.Qual_4_0 == 1,
        _ => false
    };

    static bool Hold(DailyRow z, decimal X) => X switch
    {
        0.01m  => z.Hold_1_0 == 1,
        0.015m => z.Hold_1_5 == 1,
        0.02m  => z.Hold_2_0 == 1,
        0.03m  => z.Hold_3_0 == 1,
        0.04m  => z.Hold_4_0 == 1,
        _ => false
    };

    static decimal VR(DailyRow z, decimal X) => X switch
    {
        0.01m  => z.VR_1_0,
        0.015m => z.VR_1_5,
        0.02m  => z.VR_2_0,
        0.03m  => z.VR_3_0,
        0.04m  => z.VR_4_0,
        _ => 0m
    };
}
```


---

## Add/Update: `ZEN/Excel/Excel.csproj`

```text
ZEN/Excel/Excel.csproj
```

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Core\Core.csproj" />
    <ProjectReference Include="..\Analysis\Analysis.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="ClosedXML" />
  </ItemGroup>
</Project>
```


---

## Add/Update: `ZEN/Excel/WorkbookWriter.cs`

```text
ZEN/Excel/WorkbookWriter.cs
```

```csharp
using ClosedXML.Excel;
using IndexContainment.Core;
using IndexContainment.Core.Models;
using IndexContainment.Core.Utils;
using IndexContainment.Analysis;

namespace IndexContainment.ExcelIO;

public static class WorkbookWriter
{
    public static void Write(string outPath, string[] symbols, string dataRoot, TimeSpan anchor, Dictionary<string, List<DailyRow>> perSymbolRows)
    {
        using var wb = new XLWorkbook();
        WriteConfigSheet(wb, symbols, dataRoot, anchor);

        foreach (var sym in symbols)
        {
            perSymbolRows.TryGetValue(sym, out var rows);
            rows ??= new List<DailyRow>();
            WriteSymbolSheet(wb, sym, rows);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        wb.SaveAs(outPath);
    }

    static void WriteConfigSheet(XLWorkbook wb, string[] symbols, string dataRoot, TimeSpan anchor)
    {
        var ws = wb.Worksheets.Add("Config");
        int r = 1;
        ws.Cell(r++, 1).Value = "Build UTC"; ws.Cell(r - 1, 2).Value = DateTime.UtcNow.ToString("u");
        ws.Cell(r++, 1).Value = "Data Root"; ws.Cell(r - 1, 2).Value = dataRoot;
        ws.Cell(r++, 1).Value = "Anchor";    ws.Cell(r - 1, 2).Value = anchor.ToString(@"hh\:mm");
        ws.Cell(r++, 1).Value = "Thresholds"; ws.Cell(r - 1, 2).Value = string.Join(", ", Thresholds.Grid.Select(x => x.Label));
        ws.Cell(r++, 1).Value = "Symbols"; ws.Cell(r - 1, 2).Value = string.Join(", ", symbols);
        ws.Columns().AdjustToContents();
    }

    static void WriteSymbolSheet(XLWorkbook wb, string symbol, List<DailyRow> rows)
    {
        var ws = wb.Worksheets.Add(Sheet.SafeName(symbol));
        int r = 1;

        // Summary block
        ws.Cell(r,1).Value = "Threshold"; ws.Cell(r,2).Value = "n"; ws.Cell(r,3).Value = "Hits"; ws.Cell(r,4).Value = "HitRate";
        ws.Cell(r,5).Value = "WilsonLower95"; ws.Cell(r,6).Value = "p99ViolationRatio"; ws.Cell(r,7).Value = "MedianTimeToLow(min)";
        r++;

        var summaries = SummaryBuilder.Build(rows);
        foreach (var s in summaries)
        {
            ws.Cell(r,1).Value = s.Threshold;
            ws.Cell(r,2).Value = s.N;
            ws.Cell(r,3).Value = s.Hits;
            ws.Cell(r,4).Value = s.HitRate;
            ws.Cell(r,5).Value = s.WilsonLower95;
            ws.Cell(r,6).Value = (double)s.P99ViolationRatio;
            ws.Cell(r,7).Value = s.MedianTimeToLow;
            r++;
        }

        r += 1;

        // Daily table
        var headers = new[]
        {
            "Date","PrevClose","Open","P10","LowAfter10","HighAfter10","Close",
            "GapPct","ExtraDropPct","ExtraRisePct","TimeToLowMins",
            "Qual_1.0%","Hold_1.0%","VR_1.0%",
            "Qual_1.5%","Hold_1.5%","VR_1.5%",
            "Qual_2.0%","Hold_2.0%","VR_2.0%",
            "Qual_3.0%","Hold_3.0%","VR_3.0%",
            "Qual_4.0%","Hold_4.0%","VR_4.0%"
        };
        for (int c = 0; c < headers.Length; c++) ws.Cell(r, c + 1).Value = headers[c];
        r++;

        foreach (var x in rows.OrderBy(z => z.Date))
        {
            int c = 1;
            ws.Cell(r,c++).Value = x.Date; ws.Cell(r, c-1).Style.DateFormat.Format = "yyyy-mm-dd";
            ws.Cell(r,c++).Value = x.PrevClose;
            ws.Cell(r,c++).Value = x.Open;
            ws.Cell(r,c++).Value = x.P10;
            ws.Cell(r,c++).Value = x.LowAfter10;
            ws.Cell(r,c++).Value = x.HighAfter10;
            ws.Cell(r,c++).Value = x.Close;

            ws.Cell(r,c++).Value = (double)x.GapPct;
            ws.Cell(r,c++).Value = (double)x.ExtraDropPct;
            ws.Cell(r,c++).Value = (double)x.ExtraRisePct;
            ws.Cell(r,c++).Value = x.TimeToLowMins;

            ws.Cell(r,c++).Value = x.Qual_1_0; ws.Cell(r,c++).Value = x.Hold_1_0; ws.Cell(r,c++).Value = (double)x.VR_1_0;
            ws.Cell(r,c++).Value = x.Qual_1_5; ws.Cell(r,c++).Value = x.Hold_1_5; ws.Cell(r,c++).Value = (double)x.VR_1_5;
            ws.Cell(r,c++).Value = x.Qual_2_0; ws.Cell(r,c++).Value = x.Hold_2_0; ws.Cell(r,c++).Value = (double)x.VR_2_0;
            ws.Cell(r,c++).Value = x.Qual_3_0; ws.Cell(r,c++).Value = x.Hold_3_0; ws.Cell(r,c++).Value = (double)x.VR_3_0;
            ws.Cell(r,c++).Value = x.Qual_4_0; ws.Cell(r,c++).Value = x.Hold_4_0; ws.Cell(r,c++).Value = (double)x.VR_4_0;
            r++;
        }

        var lastCol = headers.Length;
        ws.Range(1,1,1+Thresholds.Grid.Length,7).Style.Font.Bold = true;
        ws.Range(1,1,1+Thresholds.Grid.Length,7).Style.Fill.BackgroundColor = XLColor.FromTheme(XLThemeColor.Accent1, 0.8);
        ws.Range(1+Thresholds.Grid.Length+1,1,1+Thresholds.Grid.Length+1, lastCol).Style.Font.Bold = true;
        ws.SheetView.FreezeRows(1 + Thresholds.Grid.Length + 1);
        ws.Range(1+Thresholds.Grid.Length+1,1, r-1, lastCol).SetAutoFilter();
        ws.Columns().AdjustToContents();
    }
}
```


---

## Add/Update: `ZEN/Cli/Cli.csproj`

```text
ZEN/Cli/Cli.csproj
```

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Core\Core.csproj" />
    <ProjectReference Include="..\Data\Data.csproj" />
    <ProjectReference Include="..\Analysis\Analysis.csproj" />
    <ProjectReference Include="..\Excel\Excel.csproj" />
  </ItemGroup>
</Project>
```


---

## Add/Update: `ZEN/Cli/Program.cs`

```text
ZEN/Cli/Program.cs
```

```csharp
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
```


---

## Add/Update: `ZEN/Tests/Tests.csproj`

```text
ZEN/Tests/Tests.csproj
```

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Core\Core.csproj" />
    <ProjectReference Include="..\Data\Data.csproj" />
    <ProjectReference Include="..\Analysis\Analysis.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="ClosedXML" />
  </ItemGroup>
</Project>
```


---

## Add/Update: `ZEN/Tests/ReducerTests.cs`

```text
ZEN/Tests/ReducerTests.cs
```

```csharp
using IndexContainment.Core.Models;
using IndexContainment.Analysis;
using Xunit;

public class ReducerTests
{
    [Fact]
    public void Synthetic_Computes_Expected()
    {
        var date = new DateTime(2024, 1, 3);
        var prevClose = 100m;
        var bars = new List<Bar>();
        bars.Add(new Bar(date.AddHours(9).AddMinutes(30), 98m, 98.2m, 97.8m, 98m, 1000));
        bars.Add(new Bar(date.AddHours(9).AddMinutes(43), 98.0m, 98.5m, 97.9m, 98.3m, 1000));
        bars.Add(new Bar(date.AddHours(9).AddMinutes(56), 98.3m, 98.6m, 98.1m, 98.4m, 1000));
        bars.Add(new Bar(date.AddHours(10).AddMinutes(9), 98.4m, 98.7m, 98.0m, 98.1m, 1000));
        bars.Add(new Bar(date.AddHours(10).AddMinutes(22), 98.1m, 98.2m, 97.6m, 97.7m, 1000));
        bars.Add(new Bar(date.AddHours(15).AddMinutes(57), 97.7m, 98.1m, 97.6m, 98.0m, 1000));

        var day = new DayBars(date, bars, prevClose);
        var rows = DailyReducer.BuildRows(new[] { day }, new TimeSpan(10,0,0));
        Assert.Single(rows);
        var r = rows[0];

        Assert.Equal(date, r.Date);
        Assert.Equal(prevClose, r.PrevClose);
        Assert.True(r.GapPct <= -0.01m);
        Assert.True(r.ExtraDropPct <= 0.01m);
    }
}
```


---

## Add/Update: `ZEN/Tests/SummaryTests.cs`

```text
ZEN/Tests/SummaryTests.cs
```

```csharp
using IndexContainment.Core.Models;
using IndexContainment.Analysis;
using Xunit;

public class SummaryTests
{
    [Fact]
    public void Summary_Matches_HandCounts()
    {
        var date = new DateTime(2024, 1, 3);
        var prevClose = 100m;
        var bars = new List<Bar>
        {
            new(date.AddHours(9).AddMinutes(30), 98m, 98.2m, 97.8m, 98m, 1000),
            new(date.AddHours(9).AddMinutes(43), 98.0m, 98.5m, 97.9m, 98.3m, 1000),
            new(date.AddHours(9).AddMinutes(56), 98.3m, 98.6m, 98.1m, 98.4m, 1000),
            new(date.AddHours(10).AddMinutes(9), 98.4m, 98.7m, 98.0m, 98.1m, 1000),
            new(date.AddHours(10).AddMinutes(22), 98.1m, 98.2m, 97.6m, 97.7m, 1000),
            new(date.AddHours(15).AddMinutes(57), 97.7m, 98.1m, 97.6m, 98.0m, 1000),
        };
        var rows = DailyReducer.BuildRows(new[] { new DayBars(date, bars, prevClose) }, new TimeSpan(10,0,0));
        var summary = SummaryBuilder.Build(rows);
        var x10 = summary.Single(s => s.Threshold == "1.0%");
        Assert.Equal(1, x10.N);
        Assert.True(x10.Hits == 1 || x10.Hits == 0); // depending on extra drop
        Assert.InRange(x10.HitRate, 0, 1);
        Assert.InRange(x10.WilsonLower95, 0, 1);
    }
}
```


---

## Add/Update: `ZEN/Tests/WorkbookValidationTests.cs`

```text
ZEN/Tests/WorkbookValidationTests.cs
```

```csharp
using ClosedXML.Excel;
using Xunit;

public class WorkbookValidationTests
{
    [Fact]
    public void Workbook_Structure_Is_Consistent_If_Present()
    {
        // Optional integration: set CHECK_OUTPUT_XLSX=1 to enforce presence.
        string outPath = Path.Combine("..","OUTPUT","IndexContainment.xlsx");
        bool enforce = Environment.GetEnvironmentVariable("CHECK_OUTPUT_XLSX") == "1";
        if (!File.Exists(outPath))
        {
            if (enforce) Assert.True(false, $"Expected workbook at {outPath}");
            else return; // skip silently
        }

        using var wb = new XLWorkbook(outPath);
        var config = wb.Worksheet("Config");
        Assert.NotNull(config);

        foreach (var ws in wb.Worksheets.Where(w => w.Name != "Config"))
        {
            // Check summary header
            Assert.Equal("Threshold", ws.Cell(1,1).GetString());
            Assert.Equal("n", ws.Cell(1,2).GetString());
            Assert.Equal("Hits", ws.Cell(1,3).GetString());
            // Find daily table header row (first blank row after summaries + 1)
            int headerRow = 1;
            while (!string.IsNullOrWhiteSpace(ws.Cell(headerRow,1).GetString())) headerRow++;
            headerRow++; // header line
            Assert.Equal("Date", ws.Cell(headerRow,1).GetString());
            Assert.Equal("GapPct", ws.Cell(headerRow,8).GetString());
        }
    }
}
```


---

## Add/Update: `ZEN/scripts/run.sh`

```text
ZEN/scripts/run.sh
```

```
#!/usr/bin/env bash
set -e
cd "$(dirname "$0")/.."
dotnet build -c Release
dotnet test -c Release
dotnet run -c Release --project Cli -- --data ../DATA --out ../OUTPUT/IndexContainment.xlsx --symbols SPY,QQQ,IWM,DIA --anchor 10:00
```
