# W101-OVERLAY-CONTROLS-35 — optional scope overlay controls (resolved record)

Status: **COMPLETE — offline implementation and verification. No live display or click qualification.**
Claim: `W101-OVERLAY-CONTROLS-35` (root) · workstream: `CORE-DELIVERY` · recorded 2026-11-16 from the Codex session transcript `codex_convo.md` plus a fresh focused test run.

## 1. Why this record exists

The worker stopped at a usage limit and the claim note said "coordinator completing validation and repairs, preserving files." The planned result document was never written. This record closes that gap so the claim can be released and its paths transferred to the manual-capture controller work.

## 2. What was built (from `codex_convo.md` lines 61333, 61436, 61699, 61701)

- `Utilities/W101RunningNotice.cs` — optional scope overlay with opaque owner-drawn **play / pause / stop** glyphs, a gold observed-state ring, hit-tested circular buttons and **nonactivating clicks**.
- Active feedback-waypoint scopes expose **Pause/Resume**; all scopes expose **Stop**; unavailable buttons dim.
- **Pure scope signals latch Stop**; Play cannot clear keyboard takeover, target/ownership guards or a stopped state.
- **Manual pause returns `false` immediately to dispatch guards** and waits only at supported released checkpoints.
- The overlay lives only for the lifetime of an active scope — **not** a persistent between-routine host.
- Offline bitmap preview was inspected at `.local/overlay-controls-35/running.png`.

Architecture clarifications recorded in the same transcript (lines 56231, 61242):
- Maelstrom remains background; the overlay is nonactivating and target-relative; foreground belongs to W101 while playing. An interactive panel must pause gameplay before taking focus.
- W101 scopes ignore mouse activity and count keyboard takeover only when W101 is foreground; `Ready` remains an immediate dispatch guard.
- Focus waits at released checkpoints are bounded (up to 60 s) within parent deadlines, with fresh evidence before resuming; countdown focus interruption cancels admission.

## 3. Coordinator verification (2026-11-16)

| Check | Result |
| --- | --- |
| Focused tests `--filter FullyQualifiedName~W101RunningNotice` | **28 passed / 0 failed** (`-warnaserror`, bounded flags) |
| Full native suite earlier in session | 3,068 passed / 0 failed (includes these tests) |
| Result document | this record |
| Live physical display / click | **not qualified** — offline preview only, as the transcript states |

The transcript also records 59 focused checks for the original increment; the current filter yields 28. No claim is made that the larger historical count was re-run today — only the 28 above are freshly verified.

## 4. Scope limits

No live overlay click/path was qualified; no persistent host was implemented; no generic travel/Dance/menu replay was enabled by this claim. These are inputs to the next controller design, not delivered features.

## 5. Disposition

Release the claim. Transfer `Utilities/W101RunningNotice.cs` and its test to the manual-capture controller implementation claim so the persistent host can reuse the tested glyph/click/status logic rather than rewriting it.
