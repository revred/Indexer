using IndexContainment.Data.Stooq;
using IndexContainment.Data.Providers;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IndexContainment.Core.Models;
using Xunit;

public class StooqParserTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly string _payload;
        public FakeHandler(string payload) => _payload = payload;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var msg = new HttpResponseMessage(HttpStatusCode.OK);
            msg.Content = new StringContent(_payload);
            return Task.FromResult(msg);
        }
    }

    [Fact]
    public async Task Parses_Comma_Separated_With_Header()
    {
        var csv = "Date,Time,Open,High,Low,Close,Volume\n" +
                  "2024-01-03,09:35,100,101,99,100.5,1000\n";
        var map = new Dictionary<string,string> { { "SPY", "spy.us" } };
        using var prov = new StooqProvider(map, TimeSpan.Zero, 0, handler: new FakeHandler(csv));
        var bars = await prov.GetIntradayAsync("SPY", 1);
        Assert.Single(bars);
        Assert.Equal(new DateTime(2024,1,3,9,35,0), bars[0].T);
    }

    [Fact]
    public async Task Parses_Semicolon_Separated_With_Split_Date_Time()
    {
        var csv = "DATE;TIME;OPEN;HIGH;LOW;CLOSE;VOLUME\n" +
                  "20240103;09:35;100;101;99;100.5;1000\n";
        var map = new Dictionary<string,string> { { "SPY", "spy.us" } };
        using var prov = new StooqProvider(map, TimeSpan.Zero, 0, handler: new FakeHandler(csv));
        var bars = await prov.GetIntradayAsync("SPY", 1);
        Assert.Single(bars);
        Assert.Equal(new DateTime(2024,1,3,9,35,0), bars[0].T);
    }
}