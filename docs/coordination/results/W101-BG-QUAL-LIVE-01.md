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

## Next step

Repeat with a foreground sampler running concurrently with the CLI so admission-time foreground is recorded. Only a pass with zero W101-foreground samples during the whole run can qualify the no-foreground behavior. No pin promotion, reader wiring or input authority is granted by this record.
