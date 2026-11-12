# Wizwalker and headless CLI qualification — 2026-11-10

Scope: a bounded optional loading-state observation against the selected W101
client, followed by verified worker exit and shared-control release. The user
requested a headless command interface during qualification. No game input,
movement, gameplay hooks, packet operations, volume changes or snack use occurred.

## What actually passed

The corrected source build completed two independent CLI checks in **643 ms**
and **641 ms** end to end. Both returned `Loading=false`, `CleanupConfirmed=true`
and a Completed scheduler receipt. The first backend observation took 281 ms.
The visible game baseline showed **YOU ARE IDLE** at the Pet Dance sigil.
The selected process instance remained PID 27396 with creation ticks
639299525550685854; target image SHA-256 was
`544ab726ef7b920a5ccb67aa3d06b245db7e0012317d2fe7856d58c17c31cf7e`.

After each check, the authoritative control guard could be locked and read as
ASCII `0` (clean). Process inspection found no surviving adapter worker after
the first successful check. A second check reacquired and released control
normally. The code path sends no inputs, so the check does not take W101 out of
native Idle. No desktop automation was used for these two reads.

This qualifies **current-client attachment, the idle/nonloading read, and
disconnect** on this client. It does not yet qualify a `true` flag during an
actual loading transition, character load-in (excluded by upstream), target
closure during a live read, hooks, general gameplay readiness, or future patches.
Do not promote the observation into a gameplay-ready condition.

## Failure found and repaired

The first GUI attempt failed in 156 ms because the isolated environment lacked
`aioconsole`; the reviewed package also required `aiomonitor` and `terminaltables`
to import. It never produced a valid observation. The original worker treated
failure during client construction conservatively and required recovery.

Installed the source-declared versions into the existing isolated environment:
aioconsole 0.2.1, aiomonitor 0.4.5 and terminaltables 3.1.0. No system-wide install
or base-app dependency was added. Pymem remains the existing 1.13.1. A new backend
import preflight now occurs before client creation. Missing dependencies produce
a setup error and no recovery hold; partial client construction still fails closed.
The source tree pin remains unchanged; the reviewed worker pin is now
`70f38c0f875bce43d78f2b94e7933cf27a9051eaadad2a73f7743a3dd23f2d19`.

The actual Stop → Check cleanup → Restore controls flow passed after the initial
failure. Original failure and recovery evidence were preserved. W101 was visually
confirmed still in native Idle afterward. An intervening user Escape stopped
desktop work immediately; the later explicit resume preceded the CLI tests.

## Headless interface and offline checks

[Headless commands](HEADLESS_CLI.md) now expose help, window discovery, saved
status and the bounded opt-in wizwalker check. They execute before UI initialization
and reuse the ordinary scheduler and shared control lock. They do not remotely
control a running GUI or expose arbitrary scripts, input, recovery resets or
network commands. Window discovery completed in 98 ms; status completed in 117 ms.

- Full native Release suite: **1,562 passed, 0 failed/skipped**.
- Focused CLI/adapter suite: **50 passed** (26 CLI and 24 native adapter cases).
- Python adapter suite: **16 passed**, including import failure before attachment.
- Combined Python adapter/policy/ownership/recovery/board/map suites: **80 passed**, no skips.
- Scoped C# whitespace/analyzers and Python Black checks passed.

Local evidence is under `.local/wizwalker-live-20261110/`: `cli-full.trx`,
`cli-focused.trx`, `dependency-repair-full.trx`, `cli-live-01.json`,
`cli-live-02.json`, `cli-windows-01.json`, `cli-status-01.json`, `environment.txt`,
guard snapshots and the original failure/recovery snapshots. The ordinary
operation receipts and reviewed source snapshots remain in the user's data
library. The first successful run ID is `d47dcf216e194a989c956dd011b7b64d`.

## Distribution

Packaging and installation are separate gates. The base app contains the CLI
and optional observation UI; the optional backend continues to use the explicitly
selected repository and isolated environment. Live success does not imply those
dependencies are bundled or qualified on another machine. Add actual package and
installation evidence here only after verification.

Built portable folder/ZIP `Maelstrom-20261110-193052-ce1085` and installer
`Setup-20261110-193143-7250d6/MaelstromSetup.exe`. The packaged headless help
command completed in 92 ms, and checked ZIP contents match the published files.
The actual embedded payload passed isolated install/update/uninstall verification,
including preservation of recordings, audio timing, packs, reports, routines,
settings and operation-history sentinels. The 14 installer unit tests also pass.

Installer SHA-256:
`2116642c4c439f0308b225a3ef6abda57f5177f6e38ddb70ce18ba5bceb6118a`.
Published application DLL SHA-256:
`befb09ee8dd0941cf7cbddc2ba59e12d7a4054573288997cbe3ca924df069a85`.
`package-validation.json`, build logs and `installer-lifecycle.json` preserve the
local release evidence. A pre-install inventory records 1,163 user-data files,
including hashes of 341 small files. Actual installation and installed checks
are now complete, as recorded below.

## Installed verification — 2026-11-10

The user explicitly confirmed the tested installer. Setup displayed
"Maelstrom is ready" on its first readable observation; this report does not
infer who initiated the update button. The actual installation receipt and
matching binaries independently verify the completed update. Setup and the
previous development app were closed, then all checks used the hidden CLI.

Installed directory:
`C:\Users\aarog\AppData\Local\Programs\Maelstrom\versions\11d29e86169b472ab46b3a4f2f64de02`.
It replaces `versions/d1406a367c444f778395a30a855c9113`. The executable,
application DLL, CLI guide, Dance profile and Workshop entry point match both
the tested portable payload and the installation receipt. The installer remains
version 0.2.0-preview; the version-directory ID and file hashes identify this build.

The pre/post-install inventory preserved **all 1,163 files**, with no missing
files or metadata changes; all **341 small-file hashes** matched. This is bounded
verification, not a claim that every large recording was rehashed. The live check
afterward added its ordinary operation evidence separately.

Installed commands passed: help (88 ms), status (122 ms), windows (96 ms), and
the actual opt-in W101 check (**709 ms end to end; 297 ms backend observation**).
Run `1ed3dfe798934294b2c70c92d8d7320a` returned `Loading=false`,
`CleanupConfirmed=true`, and Completed. A subsequent status command found that
same run. Running status without a data-directory override correctly selected
`C:\Users\aarog\AppData\Local\Maelstrom`.

The final shared guard was independently locked and read as ASCII `0`; no recovery
warning remained. Process inspection found no remaining adapter, installed CLI,
development app or installer process. W101 visibly showed **YOU ARE IDLE** before
the CLI check; the check sent no game inputs and left native Idle unchanged.
Desktop interaction ended after closing Setup and observing that baseline.
This remains a nonloading snapshot qualification, not positive-loading or
gameplay readiness qualification.

Evidence: `.local/wizwalker-installed-20261110/` contains the installation receipt,
matching hashes, data inventories/comparison, installed CLI results, shared-guard
snapshots, process-exit check and final verification summary. The existing tested
package was installed unchanged; no production source was modified in this step.
