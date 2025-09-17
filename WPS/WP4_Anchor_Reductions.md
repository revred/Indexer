# WP4 — Anchor & Reductions

**Objective**
Reduce intraday bars to a single per‑day record anchored at **10:00** (last bar ≤ 10:00).

**Derivations per day**
- `PrevClose` (from prior day's last close).
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
