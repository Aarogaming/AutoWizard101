# Maelstrom standalone compatibility profile

Profile version: **1.0**, 2026-10-30. Scope: the existing Windows application,
guided Python adapters and offline Diego observer. This is Maelstrom's current
development baseline; earlier Aaroneous handoffs are historical context.

Desktop packaging update: `scripts/build_desktop.ps1` produces a self-contained
Windows x64 folder with `Maelstrom.exe`, private Python/Tk and the offline Workshop.
It requires no system Python/.NET runtime at launch. `portable.mode` selects local
`Data/`; without it the packaged Workshop uses `%LOCALAPPDATA%/Maelstrom`.
This payload does not include optional Deimos/DanceBot/companion environments.
`scripts/build_installer.ps1` now packages that payload as MaelstromSetup.exe.
Per-user install/update/uninstall use recorded file ownership and preserve the
separate user-data folder. The old InstallerForm is excluded from compilation.
See `PACKAGING.md` at the root for migration, retention and validation details.

For the next build sequence, follow the
[Desktop-Use Game-Player plan](DESKTOP_GAME_PLAYER_PLAN.md), supported by the
[source recovery appendix](MAELSTROM_2_RECONSTRUCTION_PLAN.md).
It preserves this baseline while building a shared player and overlay across games.

## Runtime and installation

The implemented desktop target is `net9.0-windows10.0.22621.0` with WinForms.
Windows x64 is the evaluated environment; other operating systems, architectures
and older Windows versions are not qualified by these checks. Source builds need
the .NET 9 SDK; framework-dependent desktop builds need its Windows Desktop runtime.
The project includes Emgu CV, Tesseract, InputSimulator, NAudio, Newtonsoft.Json
and RestSharp; exact versions remain in the application csproj.

`Launch Tools.cmd` uses `.local/ToolsApp/ProjectMaelstrom.exe`, publishing it from
source on first use if absent. After source changes, explicitly republish:

```powershell
dotnet publish ProjectMaelstrom/ProjectMaelstrom/ProjectMaelstrom.csproj -c Release -o .local/ToolsApp
```

The observer uses the existing Python 3.12 dance environment with Pillow 10.4.0,
NumPy 1.26.4 and OpenCV 4.8.0.76; its desktop UI also requires Tk.
`Launch Diego Observer.cmd` opens it. The headless CLI does not import Tk.
Neither observer mode needs a game client, account, OCR service, GPU or model.
Seven optional tool environments are described in `WIZARD101_INTEGRATION_GUIDE.md`.
They are local evaluation installations, not a portable installer. A clean-machine
setup remains planned. Restoring packages may require internet access; no Aaroneous
or Devtools repository, service, build step or credential is required.

Game adapters require a signed-in Wizard101 window and their matching environment.
Pet dance supports 800x600 or 1280x800; snack feeding is supported only at 1280x800.
Guided Deimos currently requires exactly one client. Live controls require explicit
local policy permission. Offline reports do not. OCR.space credentials belong only
to features using that external service, not to all Maelstrom operation.
CPU, memory, disk and real-time latency minimums are **unmeasured**; do not advertise
a performance guarantee. Reports are limited to 256 KiB and source dimensions to
16384 per axis by the current report checker.

## Component map and verification requirements

“Satisfied” below means the stated narrow scope, not production-wide conformance.

| Boundary / requirement | Current implementation and status | Verification |
| --- | --- | --- |
| Presentation owns user choices | WinForms ToolCenterForm / GuidedActivityForm; Tk Observer. Implemented; full visual review partial. | .NET form tests; Python UI construction test; manual visual review still needed. |
| Perception reports observations without controls | `diego_vision.py`, Examples, SceneTracker; offline implemented. Native OCR SnapshotBridge is separate. | Synthetic detections, ambiguous matches, reordered frames and tracker tests. Current-client imagery not qualified. |
| Coordination bounds one activity | `ActivityRun`, hub, ToolIntegrationService. Implemented for guided paths. | Profile, policy revocation, stop and private-config tests. |
| Planning reacts to observations | Diego suggestions use current geometry/tracks. Human review only. No SLM or autonomous minigame player implemented. | Random-order/unknown-observation tests; similarity is not probability. |
| Execution belongs behind adapters | `activity_runner.py` invokes Deimos or DanceBot; native legacy bots still exist. Partial isolation. | Callback and focus tests use doubles; live gameplay remains unverified. |
| Persistence publishes complete valid reports | `offline_report_store.py` shared by Tk and both CLIs. Satisfied for Diego report saves. | Valid replacement, invalid/nonfinite/oversized payload rejection and locked-destination recovery tests. |
| Knowledge remains local where possible | Companion SQLite read-only export and WizWikiDataService merge. Implemented. | Integration tests and earlier 1,851-record export evidence. |
| One input owner per target | Hub OS lease prevents competing hub-launched backends; Windows job objects supervise descendants. Partial: not a universal lock across native legacy bots or externally launched tools. | Hub contention/release and .NET process tests. Global arbitration is a priority gap. |
| Stop releases ownership | Cooperative stop, process-tree fallback, job ownership and backend cleanup. Implemented for supervised tools; no claim of bounded cooperation from arbitrary upstream code. | Cancellation/cleanup tests; live stuck-backend recovery still needs an explicit controlled trial. |

## Local contracts

Activity input is a version-1 JSON profile validated by `validate_profile`; it names
questing, farming or pet_dance, runtime bounds and activity settings. Unknown profile
versions fail. CLI argument arrays carry source, profile, policy, event and stop-file
paths. Status is the latest JSON snapshot plus `ACTIVITY` stdout records; errors
surface through a failed status and/or nonzero process exit. Connection readiness
has a 120-second bound; activity duration is 1–180 minutes. Stop-file detection,
policy revocation and focus checks are cooperative, with the launcher responsible
for process termination when cooperation fails.

Diego input is a saved frame plus an example pack; output is the existing offline
report described in `AARONEOUS_COMPATIBILITY.md`. That payload has no runtime schema
version, session ID or timestamp. Fixture-set v1 is not a payload version. Retain its
shape for existing consumers; introduce a separately versioned envelope when real
cross-process sessions require identifiers, deadlines and evidence references.
Do not derive timing from frame index or claim that replay is reactive execution.

All Diego report writers now serialize finite JSON, enforce size and shape checks,
write a sibling temporary file, then replace the destination. Save failures raise
errors (GUI displays them); there is no automatic retry. An ordinary validation or
replacement failure preserves the previous file and removes the temporary file.
A process crash can leave a temporary file; readers use only the selected report.
Atomic replacement is not a guarantee against power-loss data loss. Concurrent
successful writes to the same path remain last-writer-wins; unique run destinations
are required before introducing concurrent sessions.

## Task lifecycle and evidence

Adopted lifecycle: intent → bounded task → attempt → observation/action → verification
→ disposition. The desired final outcomes are completed, failed, cancelled, expired
and review-pending. **This full outcome model is planned, not already implemented.**
Current guided statuses include connecting/running/stopping/stopped/failed/completed;
stopped does not distinguish cancellation from expiration. Backend exit is not quest
completion. Offline suggestions always remain review-pending for gameplay purposes.
Current code does not automatically retry a failed activity; the user must restart.
Future retries must have an explicit limit and a fresh attempt ID, and must not repeat
an unverified action after an ambiguous failure.

Evidence must name source revision (including dirty state), configuration, input
provenance, mode and verification. Synthetic fixtures, saved-media observation,
connection-only checks and live runs are separate evidence classes. Existing fixture
hashes and environment snapshots cover some provenance; durable per-attempt receipts
are a prioritized gap. The earlier successful connection probe proves readable client
state and release only. No complete quest, farm or pet run has been validated here.

## Permissions, gates and next work

The offline observer needs selected image/pack read access and an output directory
it can write. Guided activities additionally need game-window or process access,
explicit policy permission and user-selected resource/spending settings. There is no
new service, model download or desktop capture permission introduced by this profile.

Use the existing `maelstrom/*` branch workflow and conventional commits. Review
changed boundaries and recovery behavior before merging; do not sweep unrelated
working changes into a commit. Python changes require relevant unittest coverage,
Black formatting, compilation and `git diff --check`; C# changes require dotnet
format/build/test. Existing repository-wide formatting debt is not silently rewritten.
Comments explain atomic publication and its recovery limits. No new dependency was
added for this change.

Priorities, in order:

1. Add a local versioned attempt receipt and explicit cancelled/expired/review-pending
   outcomes to guided runs, preserving current UI compatibility. Verify deadlines,
   cleanup failure and crash recovery with doubles before live execution.
2. Unify input ownership across guided and native legacy paths, keyed to target
   identity. Verify contention, process death and reacquisition across both paths.
3. Produce a reproducible optional-component installer and run it in a clean Windows
   environment without either reference repository present.
4. Calibrate Diego on labelled current-client recordings, hold out evaluation frames,
   measure latency, then qualify a bounded live trial separately. Random spawn order
   requires observation-driven decisions, never a tutorial timeline script.

Future integration may adapt saved observations, task receipts and bounded execution
requests. Language choice follows the per-operation efficiency rule in
`ADR_MAELSTROM_INDEPENDENT.md` (Language selection, 2026-11-19). No plugin
registry, shared ABI, Guild service or model host is required to continue this roadmap.

## Verification record: 2026-10-30

Rechecked branch `maelstrom/restore-integrations`, base HEAD
`5b5c2400809dbcb9a146254036ffb86fce148094`, with existing uncommitted work preserved.
Results apply to this working tree, not a published release:

- `.local/envs/dance/Scripts/python.exe -m unittest discover -s tools/tests`:
  **42 passed**, including three new report-publication recovery tests.
- `dotnet test ProjectMaelstrom/ProjectMaelstrom.Tests/ProjectMaelstrom.Tests.csproj --no-restore --verbosity quiet`:
  rebuilt application/test projects, **63 passed**.
- `black --workers 1 --check tools/offline_report_store.py tools/diego_report.py tools/diego_observer.py tools/tests/test_offline_report_store.py`:
  changed Python scope formatted and checked. Python compilation and `git diff --check`
  also passed. No C# source changed in this step.
- Headless `diego_report.py --demo` saved `.local/standalone-check/report.json`;
  `check_diego_report.py` accepted it with `inputSent: false`.

No game input, live capture, model training or gameplay validation was performed in
this step. Clean-machine installation, full visual QA and performance qualification
remain open. No new commit, push, release or cross-project dependency was created.
