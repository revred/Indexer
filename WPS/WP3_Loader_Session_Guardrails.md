# WP3 — Loader & Session Guardrails

**Objective**
Implement robust, streaming CSV ingestion with day grouping and RTH sanity checks.

**Key Behaviors**
- Discover files: `DATA/<SYMBOL>/<SYMBOL>_*.csv` (sorted).
- Group rows by `Date`.
- Enforce coverage: keep days with ≥ 20 bars, else **Skip: LowCoverage**.
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

**Definition of Done**
- `CsvLoader.LoadAll()` passes Tests/ReducerTests and synthetic regressions.
