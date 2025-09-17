# 1238_Patch_01_WPS_Full_Detail.md

This patch supplies **full, production‑ready content** for all 12 Work Packages (WPs) in the `WPS/` folder of the **Indexer** repo.
Each WP is designed to be **actionable, testable, and version‑control friendly**, following your RichMove style.

## Apply-To (file map)

- `WPS/WP1_Data_Contract.md`
- `WPS/WP2_Directory_Layout.md`
- `WPS/WP3_Loader_Session_Guardrails.md`
- `WPS/WP4_Anchor_Reductions.md`
- `WPS/WP5_Derived_Metrics.md`
- `WPS/WP6_Threshold_Logic.md`
- `WPS/WP7_Symbol_Level_Analysis.md`
- `WPS/WP8_Excel_Writer.md`
- `WPS/WP9_Config_Metadata.md`
- `WPS/WP10_Performance.md`
- `WPS/WP11_Validation_Logs.md`
- `WPS/WP12_CLI_ExitCodes.md`

> **Note:** Copy each section below into the corresponding file path. Or commit this whole patch as-is into `PATCHES/1238_Patch_01_WPS_Full_Detail.md` and apply in your next commit.


---

## File: WPS/WP1_Data_Contract.md

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


---

## File: WPS/WP2_Directory_Layout.md

# WP2 — Directory Layout

**Objective**  
Standardize folders to simplify ingestion, builds, and artifact location (RichMove‑style).

**Tree**  
```
/
├─ ZEN/                 # Solution and code
│  ├─ IndexContainment.sln
│  ├─ Core/
│  ├─ Data/
│  ├─ Analysis/
│  ├─ Excel/
│  ├─ Cli/
│  ├─ Tests/
│  └─ scripts/
├─ DATA/                # Source CSV (gitignored)
├─ OUTPUT/              # Excel outputs (gitignored)
├─ WPS/                 # Work packages (this folder)
├─ Analysis/            # PERF, notes
│  └─ PERF/             # performance tests & traces
└─ PATCHES/             # time-stamped patch markdowns
```

**Acceptance Criteria**  
- Solution builds from `ZEN/` with relative paths to siblings.  
- `DATA` and `OUTPUT` excluded via `.gitignore`.

**Definition of Done**  
- Readme reflects the layout and build steps.


---

## File: WPS/WP3_Loader_Session_Guardrails.md

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


---

## File: WPS/WP4_Anchor_Reductions.md

# WP4 — Anchor & Reductions

**Objective**  
Reduce intraday bars to a single per‑day record anchored at **10:00** (last bar ≤ 10:00).

**Derivations per day**  
- `PrevClose` (from prior day’s last close).  
- `Open` = first bar open of day.  
- `P10` = close of last bar with `Time ≤ 10:00`.  
- `LowAfter10` = min low for bars `Time > 10:00`.  
- `HighAfter10` = max high for bars `Time > 10:00`.  
- `Close` = last close of the day.

**Acceptance Criteria**  
- Synthetic day produces expected P10 and extremes after anchor.  
- No use of bars beyond 10:00 when computing P10 (anti‑peek).

**Definition of Done**  
- `DailyReducer.BuildRows` yields one `DailyRow` per valid day.


---

## File: WPS/WP5_Derived_Metrics.md

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


---

## File: WPS/WP6_Threshold_Logic.md

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


---

## File: WPS/WP7_Symbol_Level_Analysis.md

# WP7 — Symbol‑Level Analysis

**Objective**  
Aggregate per‑day rows to per‑symbol statistics that are decision‑useful and conservative.

**For each X**  
- `n` = days where `Qual_X == 1`  
- `hits` = days where `Hold_X == 1`  
- `hitRate = hits / n`  
- `WilsonLower95(hits,n)` — conservative bound on true hit rate  
- `p99ViolationRatio` = 99th percentile of `VR_X`  
- `MedianTimeToLow` (minutes)

**Scoring (for ranking)**  
`score = WilsonLower95 * sqrt(n)`

**Acceptance Criteria**  
- Summary matches unit tests on fixed synthetic sets.  
- Stable across runs (deterministic ordering).

**Definition of Done**  
- `SummaryBuilder.Build()` returns a full table across X.


---

## File: WPS/WP8_Excel_Writer.md

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


---

## File: WPS/WP9_Config_Metadata.md

# WP9 — Config & Metadata

**Objective**  
Centralize build parameters and reproducibility info.

**Contents**  
- Anchor time (HH:mm).  
- Threshold grid.  
- Symbols list.  
- Data root path.  
- Build time (UTC).  
- Version (git hash if available).

**Acceptance Criteria**  
- Values on `Config` match CLI inputs and code defaults.

**Definition of Done**  
- Consumers can audit runs from workbook metadata alone.


---

## File: WPS/WP10_Performance.md

# WP10 — Performance (PERF)

**Objective**  
Guarantee predictable performance and prevent regression creep.

**Approach**  
- Streamed CSV reading (no full file loads).  
- Avoid LINQ in extreme hot paths if needed; but keep readability first.  
- PERF harness under `Analysis/PERF/`: measure wall time and allocation for representative years.

**Targets** *(indicative, machine-dependent)*  
- 4 US ETFs (25y each) end-to-end ≤ ~10–20s cold run.  
- Zero GC-induced pauses > 200ms in steady build.

**Acceptance Criteria**  
- PERF markdown report with timings per module and end‑to‑end.

**Definition of Done**  
- PERF report checked into `Analysis/PERF/README.md`.


---

## File: WPS/WP11_Validation_Logs.md

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


---

## File: WPS/WP12_CLI_ExitCodes.md

# WP12 — CLI & Exit Codes

**Objective**  
Provide a minimal, scriptable CLI with deterministic exits for automation.

**CLI**  
```
dotnet run --project ZEN/Cli --   --data ./DATA   --out ./OUTPUT/IndexContainment.xlsx   --symbols SPY,QQQ,IWM,DIA   --anchor 10:00
```

**Exit Codes**  
- `0` Success.  
- `2` No symbols discovered/specified.  
- `3` Per-symbol fatal error (I/O, schema) — message to stderr.

**Acceptance Criteria**  
- Running with missing DATA returns `3` and a clear message.  
- Valid run prints per‑symbol day counts and final output path.

**Definition of Done**  
- Used in CI or local scripts (`scripts/run.sh`, `run.ps1`).
