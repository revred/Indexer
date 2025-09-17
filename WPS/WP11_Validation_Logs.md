# WP11 — Validation & Logs

**Objective**
Expose integrity counters and reasons so data issues are visible and auditable.

**Required Logs**
Emit `OUTPUT/Integrity_<SYMBOL>.txt` with:
- DaysLoaded
- DaysKept
- EarlyCloses (close - open <= 5h considered early; heuristic, not a calendar)
- Skipped: LowCoverage / BadOHLC / NonConformingCadence / Other
- MedianBarsAfterResample

Console should print a one-line summary per symbol and the output path.

**Sheet Integrity Row (optional)**
- On each symbol sheet, add a small block with totals under the summary.

**Acceptance Criteria**
- Synthetic input with deliberate errors yields expected counters.

**Definition of Done**
- Operators can pinpoint gaps without opening the CSVs.
