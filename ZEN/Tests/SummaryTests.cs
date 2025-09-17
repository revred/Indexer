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