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

**Aggregation**
- Target ≈ 30 bars per RTH day (e.g., 13‑minute bars).
- For half-days, ≥ 20 bars; else mark as **Skipped: LowCoverage**.

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
