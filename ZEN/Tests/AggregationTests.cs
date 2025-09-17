using IndexContainment.Core.Models;
using IndexContainment.Data;
using Xunit;

public class AggregationTests
{
    [Fact]
    public void FullDay_Resamples_To_Approx_42_Bars()
    {
        var date = new DateTime(2024, 1, 3);
        var open = date.AddHours(9).AddMinutes(30);
        var close = date.AddHours(16);
        var bars = new List<Bar>();
        var t = open;
        var price = 100m;
        while (t <= close)
        {
            var o = price; var c = price + 0.01m;
            var h = Math.Max(o, c) + 0.02m; var l = Math.Min(o, c) - 0.02m;
            bars.Add(new Bar(t, o, h, l, c, 100));
            price = c; t = t.AddMinutes(1);
        }
        var day = new DayBars(date, bars, 101m);
        var res = CompositeResampler.ResampleIfNeeded(day, ResampleMode.Composite);
        Assert.InRange(res.Bars.Count, 40, 44);
        var sess = SessionDetector.Detect(res);
        Assert.True((sess.Close - sess.Open) >= TimeSpan.FromHours(6));
    }

    [Fact]
    public void HalfDay_Resamples_To_Approx_30_Bars_And_Flags_EarlyClose()
    {
        var date = new DateTime(2024, 7, 3);
        var open = date.AddHours(9).AddMinutes(30);
        var close = date.AddHours(13);
        var bars = new List<Bar>();
        var t = open;
        var price = 100m;
        while (t <= close)
        {
            var o = price; var c = price + 0.01m;
            var h = Math.Max(o, c) + 0.02m; var l = Math.Min(o, c) - 0.02m;
            bars.Add(new Bar(t, o, h, l, c, 100));
            price = c; t = t.AddMinutes(1);
        }
        var day = new DayBars(date, bars, 101m);
        var res = CompositeResampler.ResampleIfNeeded(day, ResampleMode.Composite);
        Assert.InRange(res.Bars.Count, 28, 32);
        var sess = SessionDetector.Detect(res);
        Assert.True(SessionDetector.IsEarlyClose(sess, TimeSpan.FromHours(6)));
    }
}