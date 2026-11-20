# W101-BG-QUAL-LIVE-01 — bounded live qualification attempt (interim)

Status: **INTERIM — focused pass clean; unfocused pass produced data but foreground-at-admission unproven. No pin promoted.**
Date: 2026-11-17 · owner: root · claim: `W101-BG-QUAL-LIVE-01`

## Focused pass (step 1)

- Owner confirmed W101 idle at home; preflight: exact target (HWND `642779728`, PID `44468`, ticks `639304802391820609`, image `d410a246…`), guard clear (`0`, no marker), W101 foreground.
- `--cli ui-tree-watch` with exact identity, pose + quest context, 8 s, no resource context (full tree sampling), no input.
- Receipt `...`: exit 0, `Completed`, 8 trees / 32 loading samples / 0 failures, `CleanupConfirmed:true`, `HookRestored:true`, `States.nativeIdle:true`, zone candidate `Housing/CardPromo/GS_Fantasy_Castle`. No input, no recovery hold.

## Unfocused-cycle attempt (step 2) — evidence limitation

- A local waiter observed foreground change to OpenCode (`199884`) and fired the same read-only observation immediately.
- Exit 0, `Completed`, 5 trees / 20 loading samples / 0 failures, cleanup confirmed, hook restored, guard clear, no input. Zone candidate and Idle state matched the focused pass.
- **But** `ui-request.json`/`ui-result.json` contain no foreground field. The waiter only sampled foreground before launching the CLI; process startup and the native admission check happen later. The owner's 10 s unfocused window may have ended before admission. Therefore this pass **does not prove** that observation works while W101 is unfocused. No no-foreground claim is made from it.

## Definitive unfocused pass — no-foreground behavior QUALIFIED 2026-11-17

A corrected waiter (fire when W101 is NOT foreground) polled the foreground handle every 100 ms for the entire run and recorded **147 samples, `W101_samples=0`, distinct `[199884]`** (OpenCode). During that run an `ui-tree-watch` against the exact pinned target completed with exit 0: **12 trees / 48 loading samples / 0 failures**, `CleanupConfirmed:true`, `HookRestored:true`, `States.nativeIdle:true`, zone candidate `Housing/CardPromo/GS_Fantasy_Castle`, no input, guard clear afterward. Receipt `d8c040db511245d994614752063f5805`; artifacts `.local/agent-runs/bg-qual-unfocused-sampled-20261117-012259/`.

**Correction to my earlier diagnosis:** I claimed the native read path requires foreground (`W101UiTreeOperation.cs:138,175`). That was wrong — `GameWindows.IsCurrent` is `SameIdentity(window, Read(window.Handle))` (`GameWindows.cs:45`), i.e. **process identity**, not focus. The pinned `w101_ui_tree.py` therefore already supports unfocused observation; no new worker pin is needed to build the background facts service.

Limits: this qualifies read-only observation without foreground on this client build; it does NOT qualify movement, input dispatch, gameplay outcomes, or a resident always-on loop. Hook restoration was verified per bounded pass, not for an unbounded continuous attachment. No pin was promoted by this pass.
