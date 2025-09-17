# Stooq Data Provider Analysis

## Executive Summary

Stooq's free API provides **daily data only** for US ETFs. It does **not provide intraday minute-level data** required for the IndexContainment Half-Gap Containment hypothesis analysis.

## Testing Results (2025-09-17)

### API Endpoint Tested
- Base URL: `https://stooq.com/q/d/l/?s={symbol}&i={interval}`
- User-Agent: `Indexer/1.0`
- Symbols tested: SPY, QQQ, IWM, DIA (using `.us` suffix)

### Interval Testing Results

| Interval | Parameter | Status | Data Available |
|----------|-----------|---------|----------------|
| 1-minute | `i=1` | ❌ No data | "No data" response |
| 5-minute | `i=5` | ❌ No data | "No data" response |
| 60-minute | `i=60` | ❌ No data | "No data" response |
| Daily | `i=d` | ✅ Success | ~20 years (2005-present) |

### Sample Daily Data Response
```csv
Date,Open,High,Low,Close,Volume
2005-02-25,92.7949,93.8716,92.7097,93.6948,79289004
2005-02-28,93.4735,93.5952,92.6083,93.0544,89985785
2005-03-01,93.1845,93.7683,93.1845,93.5386,61658442
```

### Error Pattern for Intraday Data
```
Warning: mysql_num_rows() expects parameter 1 to be resource, boolean given in /home/stooq/www/q/d/l/index.htm on line 286
No data
```

## Impact on IndexContainment Application

### Requirements Not Met
- ❌ **Intraday minute bars**: Required for composite cadence (5m/15m/5m)
- ❌ **Gap analysis**: Needs opening gaps and intraday movements
- ❌ **Anchor point analysis**: Requires bars around 10:00 AM anchor time
- ❌ **Session detection**: Needs multiple bars per day to detect early closes

### Requirements Partially Met
- ✅ **Historical depth**: ~20 years available (vs 25 year target)
- ✅ **Symbol coverage**: All target ETFs (SPY, QQQ, IWM, DIA) supported
- ✅ **Data quality**: Clean CSV format, proper OHLCV structure

## Implementation Status

### Backfill Command Results
```bash
cd ZEN
dotnet run --project Cli -- backfill stooq --symbols SPY,QQQ,IWM,DIA --interval 1 --out ../DATA --throttle-ms 1200 --retries 3

# Output:
[stooq] fetching SPY interval=1m ...
[stooq] fetching QQQ interval=1m ...
[stooq] fetching IWM interval=1m ...
[stooq] fetching DIA interval=1m ...
[stooq] wrote 0 yearly files.
```

### Code Status
- ✅ **StooqProvider implementation**: Complete and functional
- ✅ **Rate limiting**: Working (1200ms throttle)
- ✅ **Retry logic**: Implemented (3 retries)
- ✅ **Error handling**: Graceful "No data" handling
- ✅ **CSV parsing**: Supports multiple formats (comma/semicolon, various date formats)

## Alternative Data Sources

For intraday minute-level data, consider:

1. **Interactive Brokers (IBKR)**
   - ✅ Intraday minute data
   - ✅ Professional-grade API
   - ❌ Requires account and API setup

2. **Alpha Vantage**
   - ✅ Intraday data (1,5,15,30,60 min)
   - ✅ Free tier available
   - ❌ Limited API calls per day

3. **Yahoo Finance (yfinance)**
   - ✅ Intraday data for recent periods
   - ✅ Free access
   - ❌ Limited historical depth for minute data

4. **Polygon.io**
   - ✅ Comprehensive intraday data
   - ✅ Good historical coverage
   - ❌ Paid service

5. **Quandl/Nasdaq Data Link**
   - ✅ Professional data quality
   - ✅ Historical depth
   - ❌ Paid service

## Recommendations

1. **Short-term**: Use IBKR or Alpha Vantage for proof-of-concept with limited historical data
2. **Production**: Consider Polygon.io or professional data vendor for full 25-year intraday dataset
3. **Keep Stooq**: Useful for daily data validation and basic backtesting
4. **Hybrid approach**: Daily data from Stooq + intraday from another source

## Technical Notes

- Stooq appears to be experiencing some database issues (mysql_num_rows warning)
- The `.us` suffix mapping is correct for US ETFs
- Rate limiting at 1200ms works well with no 429 errors observed
- CSV parsing is tolerant and handles the daily format correctly