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
