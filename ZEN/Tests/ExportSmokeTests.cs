using IndexContainment.Core.Models;
using IndexContainment.Analysis;
using IndexContainment.Export;
using Xunit;

public class ExportSmokeTests
{
    [Fact]
    public void DailyCsv_And_SummaryJson_Write()
    {
        var date = new DateTime(2024,1,3);
        var bars = new List<Bar>
        {
            new(date.AddHours(9).AddMinutes(30), 98m, 99m, 97.5m, 98.2m, 1000),
            new(date.AddHours(10), 98.2m, 99m, 98m, 98.6m, 1000),
            new(date.AddHours(15).AddMinutes(59), 98.6m, 99.2m, 98.1m, 98.9m, 1000),
        };
        var day = new DayBars(date, bars, 100m);
        var rows = IndexContainment.Analysis.DailyReducer.BuildRows(new[] { day }, new TimeSpan(10,0,0));

        var daily = DailyCsvWriter.Write("../DAILY", "TEST", rows);
        Assert.True(File.Exists(daily));

        var summary = SummaryJsonWriter.WriteSymbolSummary("../SUMMARIES", "TEST", rows);
        Assert.True(File.Exists(summary));

        var exc = ExceptionsExporter.WriteWorstVR("../EXCEPTIONS", "TEST", rows, 5);
        Assert.True(File.Exists(exc));
    }
}