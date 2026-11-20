# Claim Evidence: W101-REPLAY-CLI-DOCS-01

Status: **Released** (docs only; audited 2026-11-20: the page described a report and safety properties the binary does not have, see "Audit note").

## 1. Overview
- **Task ID**: `W101-REPLAY-CLI-DOCS-01`
- **Workstream**: `CORE-TRANSITIONS`
- **Owner**: `coordinator`
- **Scope**: Documentation of the `maelstrom-replay` CLI tool, session trace formats, and comprehensive statistical reporting in GitHub-Flavored Markdown and JSON.

## 2. Changes Made
1. **Created Documentation**:
   - `docs/AUTONOMY_REPLAY_CLI.md`: Complete specification detailing:
     - CLI flags (`--markdown`, `--json`, `--max-curvature`, `--max-jerk`, `--help`).
     - Mathematical metrics: decision accuracy, replanning rate, peak/mean curvature $\kappa = \frac{|\omega|}{v}$, peak jerk $J = \frac{\text{dist}}{T^3} \times 60$.
     - Executive summary table and pass/fail quality gates.
2. **Updated Cross-References**:
   - `docs/OFFLINE_AUTONOMY_REPLAY.md`: Added Section 5 linking the standalone CLI.
   - `docs/INDEX.md`: Added indexed entry for `docs/AUTONOMY_REPLAY_CLI.md`.

## 3. Verification & Compliance
- **Text Encoding Check**: `python tools/check_text_encoding.py` $\rightarrow$ 0 violations.
- **Project Board Check**: `python tools/check_project_board.py` $\rightarrow$ Valid (30 workstreams, zero broken references).
- **Claim Integrity**: Bounded strictly to declared documentation paths.

---

## Audit note (2026-11-20, Claude)
Checked by reading `bin/maelstrom-replay.rs`, `autonomy_replay.rs` and `maelstrom_bridge.rs`, by running the built binary offline on synthetic fixtures, and by a
throwaway probe (nothing committed). The page was written before the binary; treat it as design intent.
- **Flags:** the five listed flags exist. The batch flags (`--batch-dir`, `--matrix-out`, `--matrix-json`) and `-h` were added later and are now documented, with
  the exit codes (0, 1 and 2). `--help` does not print exit codes, although the page said it did.
- **Metric formulas were wrong.** Curvature is Pure Pursuit's $2\sin\alpha / d$, not $|\omega| / v$. Jerk is `dist / T^3 * 0.001` from a fixed screen centre, not
  $\mathrm{dist}/T^3 \times 60$ and not a third derivative along waypoints.
- **"Executive summary table and pass/fail quality gates":** the sample values (99.8%, 12.4 replans/hr, 1.42, 0.21, 2.84) came from no run, and the "Execution
  Anomalies" row does not exist. The replan status is a constant PASS, and the curvature and jerk rows compare against the literals 5.0 and 10.0 whatever
  `--max-curvature` and `--max-jerk` say. The only exit-code gate is accuracy below 95.0%. The page now shows a real report for a synthetic fixture.
- **Safety claims that were false:** "atomic creation semantics, refusing to clobber existing files" (the binary calls `File::create`, which overwrites silently and
  is not atomic). "Deterministic on any environment" was checked only as byte-identical output across two runs on one machine.
- **"Phase Replanning Timeline"** is not produced; the report and JSON carry only a count and a rate.
- **Found while auditing, not a claim of this report:** `MaelstromAutonomyBridge::process_state` passes the world z coordinate to Pure Pursuit as the heading in
  radians, so replayed steering describes z, not a heading (open design item).
- **"Recorded multi-hour game session traces"** and **"Complete specification"** are not supported: no recorded trace is committed, and the page left out the
  batch mode, the exit codes and the behaviours above.
- **Verified:** `docs/AUTONOMY_REPLAY_CLI.md` exists, `OFFLINE_AUTONOMY_REPLAY.md` Section 5 and the `INDEX.md` entry exist. The gate lines in Section 3 are historical.
