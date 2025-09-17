# WP3 — Loader & Session Guardrails

**Objective**
Implement robust, streaming CSV ingestion with day grouping and RTH sanity checks.

**Key Behaviors**
- Discover files: `DATA/<SYMBOL>/<SYMBOL>_*.csv` (sorted).
- Group rows by `Date`.
- **Session detection**: per day, `open = first bar time`, `close = last bar time` (exchange local time).
- **Resampling decision**: If median spacing ≤ 65s → treat as minute bars → resample to composite.
- **Coverage** (post-resample):
  - Full US day: target ≈ 42 bars (12 + 18 + 12).
  - US half-day (13:00 close): ≈ 30 bars (12 + 6 + 12).
  - Keep days with ≥ 24 bars; else **Skip: LowCoverage**.
- Sort intra‑day by time; drop out‑of‑order rows (log **Reorder**).

**Edge Cases**
- Duplicate timestamps: keep first (log).
- Price anomalies (see WP1 constraints): skip day (log reason).
- Variable half‑days: allowed.

**Outputs**
- Sequence of `DayBars` with `PrevClose` propagated for each day.

**Acceptance Criteria**
- Loader yields deterministic `DayBars` for a synthetic dataset.
- Logs: counts per skip reason.
- **Integrity**: log reasons: `LowCoverage`, `BadOHLC`, `NonConformingCadence`, `Dedup`, `Reorder`.

**Definition of Done**
- `CsvLoader.LoadAll()` passes Tests/ReducerTests and synthetic regressions.
