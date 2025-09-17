# WP11 — Validation & Logs

**Objective**
Expose integrity counters and reasons so data issues are visible and auditable.

**Required Logs**
- Per symbol totals: Days loaded, Days kept, Skipped: LowCoverage, Skipped: BadOHLC, Dedup count, Reorder count.
- Emit to console and append to a small text artifact in `OUTPUT/Integrity_<SYMBOL>.txt`.

**Sheet Integrity Row (optional)**
- On each symbol sheet, add a small block with totals under the summary.

**Acceptance Criteria**
- Synthetic input with deliberate errors yields expected counters.

**Definition of Done**
- Operators can pinpoint gaps without opening the CSVs.
