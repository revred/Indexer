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
