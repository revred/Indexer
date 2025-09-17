# WP1 — Data Contract

**Objective**
Define an unambiguous, vendor‑agnostic CSV schema for aggregated RTH minute bars (~30 bars/day) and rules that guarantee stable downstream analytics.

**Scope**
- Symbols: index ETFs / indices (e.g., SPY/QQQ/IWM/DIA; extendable to FTSE/DAX/NIFTY/HSI).
- Sessions: Regular Trading Hours (RTH). Half-days allowed.
- Timezone: Exchange local. Must be uniform per symbol.

**Schema (per row, CSV)**
`Date,Time,Open,High,Low,Close,Volume`
- `Date`: `yyyy-MM-dd` (exchange local date).
- `Time`: `HH:mm` (exchange local time).
- Prices: decimal with dot separator; 4–6 dp recommended.
- Volume: integer (0 allowed).
- Header row mandatory.

**Cadence**: 5m (first hour), 15m (mid), 5m (last hour). Session boundaries are **derived from the data** per day (first and last timestamps).

**Early Closes**: If `close - open < 6.5h`, last-hour window still uses `[close−60m, close]` and the mid window shrinks. If there is < 2h total session, mid window collapses (only first/last windows are used).

**Validation**: If input is minute bars → resample. If already aggregated → validate that bar end-times match the composite windows; else **Skip: NonConformingCadence**.

**Constraints & Validation**
- Monotone non‑decreasing times within a day.
- `Low ≤ min(Open, High, Close)` and `High ≥ max(Open, Close)`; otherwise **Skipped: BadOHLC**.
- Remove duplicate timestamps (keep first, log **Dedup**).
- No NaN/empty cells; trim whitespace.

**Outputs**
- Per symbol, yearly file: `DATA/<SYMBOL>/<SYMBOL>_YYYY.csv`.

**Acceptance Criteria**
- Sample year validates with 0 schema errors.
- Loader can read 25+ years without parser exceptions.

**Risks / Mitigations**
- *Vendor daylight saving anomalies*: normalize with a canonical TZ map per symbol.
- *Missing bars*: allow and log; downstream guardrails handle skips.

**Definition of Done**
- Schema doc checked into `WPS` and validated against sample CSV.
