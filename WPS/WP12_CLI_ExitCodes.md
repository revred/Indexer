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
