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

## Backfill (Stooq) Notes

- Provider: `stooq` with tolerant CSV parsing and rate limiting.
- One request per symbol/interval; data is then split by calendar year into `DATA/<SYMBOL>/<SYMBOL>_YYYY.csv`.
- Expect limited historical depth; treat as seed data and validate with `OUTPUT/Integrity_<SYMBOL>.txt`.
