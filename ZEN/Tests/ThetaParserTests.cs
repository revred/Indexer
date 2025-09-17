using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IndexContainment.Data.Theta;
using Xunit;

public class ThetaParserTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly string _datesCsv;
        private readonly string _histCsv;
        public FakeHandler(string datesCsv, string histCsv) { _datesCsv = datesCsv; _histCsv = histCsv; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            var msg = new HttpResponseMessage(HttpStatusCode.OK);
            if (url.Contains("/index/list/dates"))
                msg.Content = new StringContent(_datesCsv);
            else if (url.Contains("/index/history/ohlc"))
                msg.Content = new StringContent(_histCsv);
            else
                msg.Content = new StringContent("");
            return Task.FromResult(msg);
        }
    }

    private sealed class FakeHandlerWithDates : HttpMessageHandler
    {
        private readonly string _datesCsv;
        private readonly string _histCsv1;
        private readonly string _histCsv2;
        public FakeHandlerWithDates(string datesCsv, string histCsv1, string histCsv2) { _datesCsv = datesCsv; _histCsv1 = histCsv1; _histCsv2 = histCsv2; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            var msg = new HttpResponseMessage(HttpStatusCode.OK);
            if (url.Contains("/index/list/dates"))
                msg.Content = new StringContent(_datesCsv);
            else if (url.Contains("/index/history/ohlc"))
            {
                if (url.Contains("start_date=20240103"))
                    msg.Content = new StringContent(_histCsv1);
                else if (url.Contains("start_date=20240104"))
                    msg.Content = new StringContent(_histCsv2);
                else
                    msg.Content = new StringContent("");
            }
            else
                msg.Content = new StringContent("");
            return Task.FromResult(msg);
        }
    }

    [Fact]
    public async Task Parses_V3_Csv_OHLC_And_Splits_By_Dates()
    {
        var datesCsv = "symbol,date\nSPX,2024-01-03\nSPX,2024-01-04\n";
        var histCsv1 = "timestamp,open,high,low,close,volume,count,vwap\n2024-01-03T09:30:00,100,101,99,100.5,123,10,100.2\n";
        var histCsv2 = "timestamp,open,high,low,close,volume,count,vwap\n2024-01-04T09:30:00,101,102,100,101.5,124,11,101.7\n";

        using var prov = new ThetaDataProvider("localhost", 25503, "csv", 0, 0, new FakeHandlerWithDates(datesCsv, histCsv1, histCsv2));
        var bars = await prov.GetIntradayAsync("SPX", 1);
        Assert.Equal(2, bars.Count);
        Assert.Equal(new DateTime(2024,1,3,9,30,0), bars[0].T);
        Assert.Equal(100m, bars[0].O);
        Assert.Equal(101.5m, bars[1].C);
    }
}