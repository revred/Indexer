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
