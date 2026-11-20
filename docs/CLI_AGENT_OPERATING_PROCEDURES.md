# Maelstrom CLI operating procedures for agents

This is the quick-start and handoff note for the Windows command-line entry
point. The detailed command reference is [HEADLESS_CLI.md](HEADLESS_CLI.md);
read that before using any command beyond `help`, `windows`, or `status`.

## 1. Reorient before acting

Work from `F:\Modlists\AutoWizard101` in PowerShell. Before edits or runtime
work, read:

1. `AGENTS.md`
2. `docs/PROJECT_ORGANIZATION.md`, `docs/PRODUCT.md`, `docs/ROADMAP.md`
3. `docs/coordination/CODEX_WORK.md`
4. The relevant entry in `docs/coordination/workstreams.json`
5. `docs/coordination/claims.json` and the contributor rules in
   `docs/coordination/EXTERNAL_AGENT_GUIDE.md`

For Gemini coordination, read `docs/coordination/gemini/FROM_GEMINI.md`,
`QUEUE.md`, `TO_GEMINI.md` and `STATUS.md`. `FROM_GEMINI.md` is Gemini-owned;
only the coordinator appends the other liaison files. An ACK or handback is not
acceptance, permission, or proof that a component works.

The checkout is shared and may contain other work. Run `git status --short` and
inspect the exact files you plan to touch. Never reset, clean, mass-format, or
commit unrelated changes. At this documentation snapshot the branch was
`maelstrom/rust-architecture-v1`, HEAD `7888cf2`, and the working tree contained
substantial concurrent changes; those are orientation details, not a clean-tree
claim. Re-read claims and git state because both change.

## 2. Find the exact app and selected window

The application is a Windows GUI executable (`WinExe`) with a headless `--cli`
entry point. It returns before opening the normal Maelstrom window and enables
per-monitor DPI awareness for window-coordinate operations. The CLI does not
open, focus, move, or resize the game window. It does not provide a general
desktop controller or an always-running backend service.

Use the exact executable you intend to qualify: source build, portable release,
and installed copy can expose different command sets. The project targets
`net9.0-windows10.0.22621.0`; a source build may be under
`ProjectMaelstrom/ProjectMaelstrom/bin/Release/`, but check the path instead of
assuming a build or install exists. Compare `--cli help` from that executable
with the source docs before relying on a newly added command.

Every CLI invocation requires a **new** `--output` JSON path whose parent folder
already exists. Reports use create-new semantics and are not overwritten. The
GUI is a `WinExe`, so the JSON report and process exit code are the reliable
result channels; do not assume a visible console or useful stdout.

```powershell
$app = 'F:\path\to\the\exact\ProjectMaelstrom.exe'
$run = 'F:\Modlists\AutoWizard101\.local\agent-runs\<unique-run-id>'
New-Item -ItemType Directory -Path $run

$helpArg = '--cli help --output "' + (Join-Path $run 'help.json') + '"'
$helpProcess = Start-Process -FilePath $app -ArgumentList $helpArg -WindowStyle Hidden -Wait -PassThru
$helpExit = $helpProcess.ExitCode

$windowsArg = '--cli windows --output "' + (Join-Path $run 'windows.json') + '"'
$windowsProcess = Start-Process -FilePath $app -ArgumentList $windowsArg -WindowStyle Hidden -Wait -PassThru
$windowsExit = $windowsProcess.ExitCode

$statusArg = '--cli status --output "' + (Join-Path $run 'status.json') + '"'
$statusProcess = Start-Process -FilePath $app -ArgumentList $statusArg -WindowStyle Hidden -Wait -PassThru
$statusExit = $statusProcess.ExitCode
```

`windows` returns window handle, PID, process start ticks, process name and
title. For W101 operations, copy **handle, PID and start ticks from the same
single row**; a title or PID alone is not target identity. The CLI rejects a
stale/replaced process. It never silently chooses the first window or
reattaches to a different process. `status` reads the latest 100 durable
operation receipts; it does not acquire control, clear a recovery hold, or prove
that the game objective succeeded.

## 3. Understand ownership, opt-ins, and cancellation

- Use one Maelstrom scheduler/input owner at a time. A GUI scheduler owning the
  same data root can make a standalone CLI invocation fail; do not kill or close
  it while the user is playing. The shared per-user input lock is separate from
  `--data-root`; changing the data root is not a lock bypass.
- Read-only observations and actions are separate. Source command groups include
  discovery (`help`, `windows`, `status`), selected-client reads
  (`wizwalker-check`, `wizwalker-watch`, `ui-tree-watch`, `module-observe`), and
  bounded action commands (`idle-cycle`, `navigation-probe`,
  `navigation-visit`, World Gate/travel/character commands, and
  `quest-conversation`). `--cli help` on the chosen binary is authoritative for
  its current command list. `HEADLESS_CLI.md` gives each command's required
  flags and limits.
- Client reads require explicit target identity and opt-ins. `--enable-client-observation`
  permits the reviewed optional observer; `--enable-ui-tree` enables the
  temporary UI-tree hook; `--enable-module-actions` admits the bounded module
  inputs. Quest progression has its separate `--enable-quest-actions` flag and
  explicit selection file. Do not add flags speculatively or infer live
  qualification from a command being available.
- All W101 commands require a fresh, exact target. **Read-only observation** (`wizwalker-check`, `wizwalker-watch`, `ui-tree-watch`, `module-observe`) verifies process identity (HWND/PID/start-ticks), **not focus**: a bounded 12-tree pass with zero W101-foreground samples completed with verified hook restoration on 2026-11-17 (receipt `d8c040db…`, `docs/coordination/results/W101-BG-QUAL-LIVE-01.md`). **Input-dispatching actions** (idle-cycle, navigation, quest, travel, character) still require W101 foreground and immediate takeover; `GameWindows.IsCurrent` is identity, while the action path adds separate focus guards. Maelstrom intentionally does not steal focus. A refused
  focus/target guard means no input was sent; do not retry in a loop.
- A stop-file path may be supplied with `--stop-file`. It must not exist at
  launch; create that exact file while the operation is running to request
  cancellation. Ctrl+C also requests cancellation when a console is attached.
  Wait for the worker to exit and inspect its receipt before any retry. A queued
  operation is not a completed one.
- If key release or observer cleanup is uncertain, honor the recovery hold.
  Use Maelstrom's verified **Restore game controls** flow; do not delete the
  warning, rewrite the guard, or relaunch gameplay to clear it. `recover-input`
  is a dedicated recovery command, not an ordinary routine finalizer.
- The usual W101 routine ending is verified native Idle, but cleanup must not
  override user takeover, cancellation, or a stopped owner. Observation-only
  work leaves the game state as found. Character selection has no native Idle.

All live actions still require current task scope and authorization. A prior
authorization, a CLI flag, a test result, or this runbook does not authorize a
new live run. Do not touch volume, spend Crowns, feed snacks, restart W101, use
network manipulation, or write installed game files as background work.

## 4. Read receipts as evidence, not as promises

Exit codes for `--cli` are:

| Code | Meaning |
| --- | --- |
| `0` | Command completed. Inspect its JSON; completion does not prove the requested game outcome. |
| `1` | Argument, setup, owner, or report error. |
| `2` | The operation did not complete successfully. Inspect receipt and artifacts. |
| `3` | Cancelled. It is not automatically restarted. |

Use the output JSON to find the operation receipt and its evidence directory.
Keep proposed actions, dispatch receipts, observed state changes, and derived
labels distinct. A process exit, accepted key dispatch, coordinate movement,
loading observation, or empty task list does not alone prove arrival, victory,
quest progress, or a complete routine. Save new reports beside the run, not over
an earlier attempt.

For offline replay, the executable also has separate entry points such as:

```text
ProjectMaelstrom.exe --replay-routine <routine.json> <session.json> <new-report.json>
ProjectMaelstrom.exe --replay-pet-dance-visit <observations.json> <new-report.json>
```

These do not attach to W101 or dispatch input. Follow `AGENTS.md` for the
focused test, formatting, lint and build commands when the assigned task calls
for them. The filtered Automatus importer test previously compiled the app and
test assemblies but stalled after test discovery without reporting a case; it
was cancelled at about 40 seconds, so no test result was established. If that
recurs, report the stall and do not claim a pass or failed assertion.

## 5. Current project handoff

The product is a game-neutral desktop game-player and experiment workbench; W101
is the first large game pack. The priority is a complete Pet Dance visit through
entry, loading, game rounds, result evidence, no-feed exit, and safe Idle. The
current work queue, active exact file reservations, and most recent evidence are
in `CODEX_WORK.md`, `claims.json`, and `workstreams.json`; read them afresh rather
than treating this snapshot as current state.

Important boundaries for follow-on work:

- The Pet Dance card remains unqualified until the live visit runtime, first
  round capture continuity, outcome and cleanup are connected and evidenced.
- Navigation experiments and graph candidates are not a qualified Commons to
  Pet Pavilion route. Do not replay old coordinates as a path.
- Combat observation candidates and the bounded turn/battle engine do not prove
  that live card identity, targeting, input response, or victory has qualified.
- Candidate game-pack data is not executable authority. Keep one shared
  scheduler/input owner and version source provenance.
- The Automatus waypoint importer is candidate-only and its focused tests have
  not produced a test result yet. It preserves ambiguous legacy markers without
  dispatching them.
- Gemini's report78 handback `GEM-0105` was accepted under `LIA-0119`; its claim
  is released. The result-screen producer remains unbound, and victory/score
  verification remains unimplemented.

For exact UI operation flags, target schemas and per-command budgets, continue
with [HEADLESS_CLI.md](HEADLESS_CLI.md). This quick-start intentionally does not
restate every historical/source-only CLI feature or claim it is installed.
