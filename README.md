# IndexContainment — Data‑Lite Hypothesis Diligence (Excel)

Multi‑project .NET solution that ingests 25 years of index minute data (≈30 bars/day per symbol),
reduces to a compact per‑day dataset, computes threshold stats for the **Half‑Gap Containment** hypothesis,
and exports a single `.xlsx` workbook with one sheet per symbol (plus a Config sheet).

## Project Layout
```
IndexContainment/
├─ ZEN/
│  ├─ IndexContainment.sln
│  ├─ Core/        # Models, constants, utils
│  ├─ Data/        # CSV loader
│  ├─ Analysis/    # Reductions + summary stats
│  ├─ Excel/       # ClosedXML writer
│  ├─ Cli/         # Console entrypoint
│  ├─ Tests/       # Unit tests (synthetic data)
│  └─ scripts/     # build/run helpers
├─ DATA/           # Put your yearly CSVs here (ignored by git)
├─ OUTPUT/         # Excel output here
├─ PATCHES/        # Date/time-wise patches
├─ WPS/            # 12 Work Packages (docs)
└─ Analysis/       # PERF, notes (kept separate from DOCS)
```

## CSV Schema (per bar)
`Date,Time,Open,High,Low,Close,Volume`
- `Date` = `yyyy-MM-dd` (exchange local date)
- `Time` = `HH:mm` (exchange local time; ensure consistent timezone)
- Provide ~30 RTH bars per day (e.g., 13‑min aggregation). Half‑days OK.

## Thresholds & Anchor
- Anchor: **10:00** (last bar ≤ 10:00 used).
- Threshold grid: **{1.0%, 1.5%, 2.0%, 3.0%, 4.0%}**.
- Containment holds if **extra drop ≤ ½·|X|** after 10:00.

## Intraday Cadence (Composite)
We use a **composite sampling** per trading day:
- **First Hour:** 5-minute bars from session open to open+60m
- **Mid Session:** 15-minute bars from open+60m to close−60m
- **Last Hour:** 5-minute bars from close−60m to session close

The session open/close are **detected from the data** (first and last bar timestamps), so **half-days and early closes** are handled automatically. Holidays yield **no data** and are naturally skipped.

### Resampling
Provide either minute bars or already-aggregated bars. The CLI supports:
- `--resample auto`  (default): detects minute bars and converts to composite cadence.
- `--resample composite`: force composite resampling.
- `--resample none`: treat input as already aggregated.

## Integrity Logs
The CLI writes a small integrity report per symbol to `OUTPUT/Integrity_<SYMBOL>.txt` with counters for: Days loaded, Kept, Skipped (reasons), Early-close days detected, and Median bars/day post-resample.

## Build & Run
```bash
# .NET 8+
dotnet --version

cd ZEN
dotnet build
dotnet test
dotnet run --project Cli --   --data ../DATA   --out ../OUTPUT/IndexContainment.xlsx   --symbols SPY,QQQ,IWM,DIA   --anchor 10:00   --resample auto
```

## Output
- **Config** sheet with metadata.
- **One sheet per symbol** with:
  - Summary block per threshold: `n, hits, hitRate, WilsonLower95, p99ViolationRatio, median time-to-low`.
  - Daily compact table (filterable).

## Notes
- Keep each symbol’s 25‑year dataset within a single sheet (Excel limit ~1,048,576 rows — you’ll be far below).
- If your minute feed has slightly different session coverage on half‑days, loader will skip days with <20 bars (log).