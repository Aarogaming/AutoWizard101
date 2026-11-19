# First wizwalker adapter

Maelstrom now has an optional **one-shot W101 loading-state observation** through
the existing operation scheduler and control owner. This is a concrete wizwalker
binding with offline-tested lifecycle and transport, not a full questing/farming
player. The initial implementation was offline-only. Its follow-up
[live/CLI qualification](WIZWALKER_LIVE_20261110.md) records the missing-dependency
repair, two source-build and one installed idle-state read with verified disconnect. A positive flag
during an actual loading transition remains unqualified.

## User workflow

In the installed build, open **Wizard101 → Observe & teach → Optional client
observation**. Select the intended Wizard101 window, locate the existing adapter
workspace if necessary, enable observation for this check, and choose **Check
loading state**. The checkbox starts off and resets off after each run. Opening
the form or refreshing the window list does not attach to a game.

The check queues once, reads once and disconnects. Stop, disabling the checkbox,
or closing the dialog requests cancellation; the dialog waits for the operation
to finish disconnecting. It never activates a game window. Results and evidence
are available through the existing **Routine runs** view. Visual Observe stays
independent; this snapshot is not automatically used to make gameplay decisions.

The optional runtime uses the already existing `.local/envs/deimos` Python
environment and the reviewed wizwalker source under `third_party/wizwalker` (moved there from
`Scripts/Library/wizwalker` on 2026-11-19; the pinned digest is unchanged).
Running a check does not install dependencies or launch Deimos. This entry and
the headless CLI are now installed and qualified for the narrow idle-state read;
the optional Python backend remains external to the base application package.

## Reused source and intentionally small capability

The reviewed archive identifies wizwalker **1.8.1**. The 53 Python source files
are pinned by a deterministic SHA-256 tree digest:
`99f0eec7e15da862893bcf6aa3d89c3d1538c0a022c04f198d490cf383cdb2d1`.
`tools/fixtures/wizwalker_review.json` records that lineage and the adapter hash.
Production admission checks the adapter script against the hash compiled into
Maelstrom; a substituted worker is rejected before target access. Queued inputs
also pin the interpreter, worker and selected client executable.

The binding calls `Client(selected_hwnd)`, `Client.is_loading()` and closes that
client's Pymem process handle. The selected method scans the selected client's
module for its loading flag and reads the flag. It does **not** call
`activate_hooks`, teleport, keyboard/mouse methods, packet APIs or a general
script executor. Constructing the upstream Client opens a process handle and
initializes its helpers; this is client-side observation, not screen capture.
Its process access permissions must not be represented as an OS read-only sandbox.

Source review found that the upstream all-hooks path activates movement and
other hooks unnecessary for this first observation, and its handler can fail
partway through initialization. This increment therefore selects the existing
hook-free loading read. The owner's broader client-side exception remains valid;
future hook-backed operations need their own restoration evidence.

The upstream loading flag explicitly excludes character load-in. `false` means
the flag was false in one read, **not** that a wizard is signed in, a screen is
recognized, a destination is reached or it is safe to act. Unknown, stale,
replaced-target and failed reads never become a false/ready value.

## Contracts and control ownership

`WizwalkerSession` defaults to disabled and binds an immutable selected HWND,
PID, process creation time, executable path and image digest. The native/Python
version-1 request fixes the only operation to `wizwalker-loading-v1` and requires
`networkAllowed=false`; unsupported operations/extra command fields are rejected.
There is no raw provider reference, global enable switch or arbitrary command
in this adapter protocol. Existing generic Deimos/wizwalker/wizSDK launch paths
remain blocked; this narrow reviewed operation is their first separate integration.

`w101.wizwalker-observe` submits an ordinary `OperationRequest`. It deliberately
uses `WritesGame=true` to enter the existing exclusive control lane even though
this operation only observes. The native scheduler acquires `GameInputLease` and
retains it through worker exit and report verification. It does not delegate
or create a second lock, queue, scheduler or input owner. A busy owner prevents
the worker from starting; recovery holds and queue cancellation continue to apply.

The worker starts hidden in the existing `OwnedProcessJob` and waits for its
bounded stdin request until the parent has attached supervision. It imports a
fresh verified copy of the pinned source, excluding old bytecode caches. The
worker verifies the selected HWND/PID/creation time/image path before opening,
during the pending read and after observing; the executable hash is checked
before attachment. No automatic client enumeration or retargeting is used.

The result carries run ID, target, source identity, method, nullable loading
state, observation time, duration and disconnect status. The parent requires the
same run/target/source/method and a fresh observation before reporting completion.
Completion means a loading observation was obtained and the worker disconnected;
it does not assert a game outcome. Child output is drained without accumulating
logs or exposing backend diagnostic text. This is reviewed trusted-code admission,
not an OS sandbox against arbitrary code run outside Maelstrom.

## Limits and failure behavior

- One process, one read and one executor thread; no models, video or polling service.
- Below-normal worker priority; a 384 MiB working-set threshold requests stop.
  This is supervised enforcement, not a guaranteed instantaneous memory ceiling.
- Observation budget: 8 seconds. Parent requests stop at 18 seconds at the latest,
  permits 3 seconds for cooperative shutdown, then terminates the owned process.
  Scheduler deadline: 25 seconds. There is no automatic retry.
- Result input is limited to 16 KiB; request input to 32 KiB; the verified source
  tree is limited to 200 Python files / 4 MiB. Client-image verification is bounded
  to 512 MiB. Each operation retains its small source snapshot for provenance.
- Cancellation retains ownership until disconnect/exit. Forced termination,
  malformed/mismatched results, partial connection or failed disconnect require
  recovery. That condition is latched **before** diagnostic/report persistence,
  including when the warning cannot be written.

The existing recovery dialog verifies released inputs and stopped workers. It
does not prove restoration of future injected patches; a hook-backed adapter must
supply that additional cleanup evidence before being integrated.

## Offline evidence and remaining gate

| Final check | Result |
| --- | --- |
| Native Release suite, warnings as errors, two processors | 1,535 passed; 0 failed/skipped, including 23 adapter cases. |
| Python adapter/policy/ownership/recovery/board/map suites | 79 passed; 0 final skips (15/8/8/17/22/9). |
| Actual supervised child with fake client | Observation, cooperative cancellation and forced termination paths pass; the latter leaves recovery blocked. |
| Formatting/lint | Five C# files pass scoped whitespace/analyzer checks; three Python files pass Black and syntax checks. |
| Plan integrity | 30 workstreams and the 25-archive / 29-source integration map validate. |

The final validation record is `.local/wizwalker-adapter-20261110/validation.json`;
native TRX reports are in the same directory. Tests use fake clients and isolated
temporary locks. One native test launches the real supervised Python transport
with `wizwalker_fake_worker.py`, which cannot attach to a game. Other tests cover
default-off admission, source changes, competing owners, selected-client changes,
stale/future/unknown data, exceptions, cancellation during cleanup, failed
disconnect, failed warning persistence and unsupported/network commands.

The existing environment contains Pymem 1.13.1 while the archived project declares
1.8.3; the worker imports the pinned wizwalker source, not the environment's
installed wizwalker 1.8.2. The follow-up proves this narrow read on the selected
current client after repairing missing imports; it does not qualify all upstream
APIs or future client versions. Offline fake success must not be reported as
proof of a current W101 memory layout or real-client success.

Next: qualify an actual loading transition and a changed/closed target under an
appropriate live-test scope; the idle-state snapshot and disconnect now pass. Then
connect qualified observations to shared module conditions and review the next
client capability. Broader gameplay teachers continue through the same scheduler.
