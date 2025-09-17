# WP6 — Threshold Logic

**Objective**
Evaluate the Half‑Gap Containment hypothesis across a fixed threshold grid.

**Thresholds**
`X ∈ {1.0%, 1.5%, 2.0%, 3.0%, 4.0%}`

**Definitions**
- `Qual_X = 1 if GapPct ≤ -X` else 0
- `Hold_X = 1 if Qual_X == 1 AND ExtraDropPct ≤ X/2` else 0
- `VR_X = ExtraDropPct / X` (0 if `X=0`)

**Acceptance Criteria**
- Synthetic examples validate Qual/Hold/VR toggles.
- Consistent flags for all X in `DailyRow`.

**Definition of Done**
- Downstream summaries reproduce counts from hand‑worked samples.
