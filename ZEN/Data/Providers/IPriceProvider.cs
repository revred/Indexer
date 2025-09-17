namespace IndexContainment.Data.Providers;

using IndexContainment.Core.Models;

public interface IPriceProvider
{
    /// <summary>
    /// Fetch intraday bars for a symbol and interval (minutes).
    /// Implementations may ignore date ranges if endpoint returns a fixed span.
    /// Bars must be returned in chronological order and use exchange-local time.
    /// </summary>
    Task<IReadOnlyList<Bar>> GetIntradayAsync(string symbol, int intervalMinutes, CancellationToken ct = default);
}