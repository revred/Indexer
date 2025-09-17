# WP8 — Excel Writer

**Objective**
Render one workbook with a `Config` sheet and one sheet per symbol, compact enough for Excel and humans.

**Config sheet**
- Build UTC, Data root, Anchor, Thresholds, Symbols.

**Symbol sheet**
- **Summary block** (top 7 columns): Threshold, n, Hits, HitRate, WilsonLower95, p99ViolationRatio, MedianTimeToLow.
- **Daily table** with all `DailyRow` fields and flags.
- Freeze panes at the first daily row; AutoFilter on; AdjustToContents.

**Acceptance Criteria**
- 25 years of a symbol fits in one sheet (well < Excel row cap).
- File opens without warnings; formats are readable.

**Definition of Done**
- `WorkbookWriter.Write()` produces an `.xlsx` at `OUTPUT/`.
