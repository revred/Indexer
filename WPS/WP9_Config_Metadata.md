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
