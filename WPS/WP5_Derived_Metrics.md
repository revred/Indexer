# WP5 — Derived Metrics

**Objective**
Compute hypothesis metrics for diligence and rule formation.

**Formulas**
- `GapPct = (Open / PrevClose) - 1`
- `ExtraDropPct = (P10 - LowAfter10) / P10`
- `ExtraRisePct = (HighAfter10 - P10) / P10`
- `TimeToLowMins` = minutes from 10:00 to bar of `LowAfter10`

**Acceptance Criteria**
- Unit tests validate edge cases (flat day, immediate low).
- Rounding: 6 dp; MidpointRounding.AwayFromZero.

**Definition of Done**
- Fields present in `DailyRow` with verified math.
