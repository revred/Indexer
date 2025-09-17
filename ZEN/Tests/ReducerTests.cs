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