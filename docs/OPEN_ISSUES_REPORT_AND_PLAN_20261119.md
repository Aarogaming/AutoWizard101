<!-- response-format: 5w1h-bda -->
# Open issues: report and plan (2026-11-19)

**Status: draft for owner review.** Written by Claude after the evidence-gate work on PR #77. It is a plan,
not proof of any game outcome: nothing here was run against a live game, and the only game-side action taken
was a read-only SHA-256 of the installed client executable. Facts are labelled with how they were obtained.

Format: every response (`R1`..`R10`) states Who, What, When, Where, Why, How and the Before, During and After
of the work. The rule is in `AGENTS.md`; `python tools/check_response_format.py --strict` enforces it on files
that carry the marker on line 1.

## Short answer to "did we set up patch discovery, verification and repair?"

**Only the safe-stop half.** The program refuses to touch a client build it has not pinned, so it cannot
silently misread memory after a rebuild. It does **not** discover patches, verify a new build, or repair or
recover on its own; those steps are manual today.

| Capability | In place? | Evidence |
| --- | --- | --- |
| Refuse an unknown client executable at the moment of use | **Yes** (fail-closed) | `W101UiTreeOperation.SupportsClientImage` (three pinned SHA-256 values), `WizwalkerOperation.Prepare` rejection text; `W101ForegroundAdmissionTests.cs` covers the pinned and unknown cases |
| Notice a patch before the program is used | **No** | nothing reads `PatchInfo/` or hashes the executable outside `Prepare` |
| Tell a cosmetic data patch from an executable change | **Incidental only** | the gate hashes the executable only; a WAD-only patch passes unobserved |
| Verify a new build | **Manual, offline pieces** | `tools/wizdiff_adapter.py` diffs two patch lists you supply; wiztype dumps; no pipeline joins them |
| Repair offsets/layouts | **No, by design** | `wizdiff_adapter.py` never rewrites offsets; each layout needs review and a live qualification |
| Roll back to a last-known-good profile | **No** | the launcher patches game files, so the game cannot be downgraded, and the app has no fallback profile |
| Flag stored data that a patch made stale | **No** | zone/collision data and wiki-derived facts carry no source-tuple staleness check |

Observed today (read-only, local, 2026-11-19):

- Installed `Bin/WizardGraphicalClient.exe` is 57,099,416 bytes, modified 2026-11-18 10:41 (local), SHA-256
  `818dbe66037bcb6913a87ab4c2b5f77b0c4fad8d2d10bed921c9fe36ef7d1114`. That value **is** one of the three pins, so
  today's build is admitted by the gate. Admitted is not qualified: it has not been re-tested live in this work.
- `docs/W101_LOCAL_INSTALLATION_SOURCES.md` recorded a 56,433,288-byte executable dated 2026-10-22. The
  executable therefore changed at least once in about four weeks, and the pin list was updated by hand.
- Data archives changed on 2026-11-18 as well (`Root.wad` and `Mob-WorldData.wad` 10:42, `Equipment-HighDetail.wad`
  19:53, the last being the equipment/clothing archive). Nothing in the repo observes these. File times are
  patch-client write times, not release dates.

Your clothing example: an update that only changes equipment data and leaves the executable byte-identical will not
trip the gate, so the program should keep running. That is expected from the code, not something that was tested
against a real equipment-only patch. Any executable rebuild, even a tiny one, changes the hash and stops the
memory-reading features until someone pins the new hash. The message says visual-only operation remains separate;
this audit did not re-test the visual-only paths.

Priority order: R1 (patch resilience), R2 (live qualification), R3 (first CI run: done, corrected 2026-11-19), R5 (owner decisions), R6
(stale claims and work-in-progress), then R4, R7, R8, R9.

## R1. Patch discovery, verification and recovery

**Who.** Owner decides the degraded-mode policy and opens every live window. Implementing agents work one bounded
claim per phase. The integration agent holding the build slot lands C# changes. No agent runs a live
qualification alone.

**What.** Turn "hard stop on any unknown executable" into a tiered response, so small patches are noticed and logged
without stopping work and large ones degrade safely and say why.

| Tier | Trigger | Program behaviour | Recovery |
| --- | --- | --- | --- |
| 0 cosmetic or data | executable hash unchanged; WAD/patch-list change only | keep running; add a drift-ledger entry; mark affected data anchors stale | automatic re-check of the anchors |
| 1 rebuilt, expected | new hash whose layout signature still matches a qualified profile | read-only/observation features held until the owner confirms; visual-only continues | offline replay, then one owner-scoped idle read |
| 2 rebuilt, unexpected | new hash and signature mismatch | memory-reading features quarantined with a banner; visual-only continues | wizdiff/wiztype offline, new reviewed layout, live qualification |
| 3 large patch | many layout differences | no automatic repair | owner-led re-qualification; the old pins stay for history |

**When.** One bounded claim per phase, in order; no calendar dates are promised. Phases P1 to P3 wait for PR #77 to
merge or for the owner's direction; P5 needs an owner live window.

**Where.** New manifest `docs/coordination/client_builds.json`; new offline tool `tools/check_client_build.py`;
a drift-ledger section in the manifest; a build-status line in the native app (`W101UiTreeOperation`,
`WizwalkerOperation`); a runbook under `docs/`. Inputs are the install folder under
`C:/ProgramData/KingsIsle Entertainment/Wizard101` (read-only).

**Why.** The game patched at least twice recently. Without discovery, the first sign of a patch is a refused operation
in the middle of a routine. Without a ledger, data-derived facts can go stale with no warning even when the executable
did not change.

**How.**
1. P1: manifest holding the three pins (status, source document, observed size/time) plus
   `check_client_build.py`, which hashes the executable and `PatchInfo/LatestFileList.bin`, records size/time for
   the WADs (hashing multi-GB archives would break the bounded-resource rule), and prints the tier. Tests use a fake
   install folder.
2. P2: one source of truth: a Python test asserting the C# `SupportsClientImage` pins equal the manifest.
3. P3: native build-status line (qualified, admitted-unqualified, unknown) and the degrade-to-visual-only path.
4. P4: drift ledger tying each stored data anchor to its source tuple (file, size, time, hash where cheap).
5. P5: re-qualification runbook chaining wizdiff, wiztype, replay fixtures and the owner live checklist (R2).
6. P6: a daily read-only watch (scheduled task) running `check_client_build.py` and writing a report under
   `private/`; it launches no game process and sends nothing.

**Before.** Record today's baseline (done above). Confirm the manifest schema with the owner, since pins decide what
runs. Keep `AGENTS.md` boundaries: offline phases do not authorize live gameplay or installs.

**During.** Phases P1 to P4 and P6 only read files and run offline tests; stop and report if any step needs the game
process, network or a write to the install folder. P3 needs the build slot, so no concurrent cargo or dotnet job.

**After.** Done means: a fixture "new hash" gives tier 2 and a fixture "WAD-only change" gives tier 0, both in tests;
the C#/manifest drift test passes; the board gate and evidence gate stay at zero warnings. Still unproven afterwards:
that a real patch lands in the predicted tier. That is only learned on the next real patch.

## R2. Live qualification (zero live qualification so far)

**Who.** Owner opens a window and authorizes it in chat; an agent runs the bounded operation and writes the evidence.
Claude can prepare the scope and checklist but cannot start a live run.

**What.** Qualify the installed build `818dbe66...` for one idle-state UI-tree read first; combat reading for
`d410a246...` stays blocked (`W101-BUILD-PROFILE-01` found zero combat field candidates, so a reviewed layout is
missing). The claims that say "live paging/cooldown waits remain qualification" stay open.

**When.** At the owner's next session with the game running at a known idle spot. Not before R1 P1, so the result can be
recorded against a manifest entry.

**Where.** Local game client, one account at a known idle location; results in a new
`docs/coordination/results/` file naming the executable hash, plus a claim update.

**Why.** Every pass so far verified code against code and saved evidence, not against a live client. Until a live run
exists, "admitted" cannot be read as "works".

**How.** One `w101.ui-tree-observe` idle read through the single input owner, with a visible countdown, cancellation and
takeover, then a cleanup verification (native Idle restored, lease released).

**Before.** Owner confirms the target window and scope; hash of the running executable matches the pin; no other
controller, capture or build is running.

**During.** One read only. Any focus loss, mouse movement or loading screen pauses dispatch per `AGENTS.md`; the owner
can stop at any time.

**After.** Save the receipt, hash and cleanup evidence; update the claim; record failures as failures. No completion
claim for any other build or for combat.

## R3. First CI run on GitHub (done; corrected 2026-11-19)

This section was a plan written before any run. It said CI had never run, that the workflow had five jobs and that it was
unknown whether Actions was enabled. All three were wrong or are now out of date, so it is rewritten as a record. Run
numbers and results below were read from GitHub on 2026-11-19; the causes are labelled as proven or not.

**Who.** The owner enabled Actions (it had been disabled) and set the repository policy to `allowed_actions: all`. Agents
fixed the failures under the owner's Autofix authorization, with small test-only commits pushed to PR #77.

**What.** `.github/workflows/ci.yml` has four jobs, not five: `build-and-test`, `python-tests`, `rust-tests` and
`node-tests`, all on `windows-latest`. Result: all four passed on `f668eea` (run 44590486074, `pull_request`). Out of
scope: adding `cargo fmt` or `clippy` to CI (toolchain drift risk, already decided), and any production code change.

**When.** Actions was disabled until the owner enabled it on 2026-11-19. Three runs followed:

| Run | Trigger, commit | Result |
|---|---|---|
| 44586189238 | `workflow_dispatch`, `da76aeb` | `python-tests` failed (23 errors of 922 tests), `node-tests` failed (1 of 12); `build-and-test`, `rust-tests` passed |
| 44589053478 | `pull_request`, `a2169ea` | `build-and-test` failed (1 of 3135 tests); the other three passed |
| 44590486074 | `pull_request`, `f668eea` | all four passed |

**Where.** GitHub Actions for this repository, on PR #77. Fix commits: `a2169ea` (Python and Node tests) and `f668eea`
(C# test). Run pages: `https://github.com/Aarogaming/AutoWizard101/actions/runs/<run number above>`.

**Why.** The earlier local fresh-clone simulation passed all four jobs and could not have caught these, because they
depend on the runner:
1. *Python, proven.* The runner's temp folder is spelled `C:\Users\RUNNER~1\...` by `tempfile` but
   `C:\Users\runneradmin\...` once resolved. Eight Dodge test files built their root from the unresolved path, the
   benchmark summary stored report paths as given, and the loaders re-resolve them, so saved and recomputed summaries
   differed. Reproduced locally by pointing `TEMP` at a directory junction: the same 23 errors with the same per-file
   counts. The tests now resolve their root. A short user name such as `aarog` has no 8.3 short form, so local runs
   could not show it.
2. *Node, best-supported cause, not proven.* The heartbeat test needed three chained 1 s timers inside a fixed 2150 ms
   sleep (123 to 147 ms of slack measured locally). It now waits for the third event with a 10 s deadline. It was not
   reproduced under load.
3. *C#, best-supported cause, not proven.* `ActualChildCancellationConfirmsExitOrKeepsRecoveryBlocked(forced: True)`
   waited 4 s for a fake Python worker to start and failed once in 3135 tests, while its sibling variant passed. The same
   message also appears if the operation ends early, so a slow start is likely, not shown. Its marker wait and its 8 s
   receipt wait (the forced path chains four 2 s transport waits) are now 20 s.

**How.** Read each failed job's log through the Actions API, reproduce locally where possible (bare virtual environment,
clean export of the commit, junction `TEMP`), fix the test setup, run the full suite and the gates, push, read the new
run. Only the first run was a manual `workflow_dispatch`; the later runs are triggered by pushes to the PR.

**Before.** The job list was checked against the local gates; `cargo fmt` and `clippy` stay out of CI. The first run was
the baseline.

**During.** Watch each run once; no polling loop and no auto-merge. No production code, workflow file or repository
setting was changed by agents.

**After.** All four jobs are green at `f668eea`. Not proven: that the Node and C# timing causes are the real ones, and a
single green run does not show they cannot recur; a repeat failure of either test means looking at the code under test,
not widening the wait again. Still open: `actions/checkout@v4` runs on Node 24 instead of 20 (a notice only), and the
policy is `allowed_actions: all`. The workflows use only `actions/checkout@v4` and `actions/setup-dotnet@v4`, so
`selected` with GitHub-owned actions only remains an owner option; changing it is the owner's decision.

## R4. Result documents only mechanically swept

**Who.** The hourly cleanup loop and Claude.

**What.** About fifty `docs/coordination/results/*.md` files had only the mechanical evidence check, not a claim-by-claim read.

**When.** One document batch per pass while the loop runs. The loop is session-only and stops when this session
closes or after seven days.

**Where.** `docs/coordination/results/`; log in `private/improvement-log.md`.

**Why.** The evidence checker catches missing paths and test names, not wrong interpretations.

**How.** Read each document against the code or test it cites; fix, then disclose with a dated "Audit corrections"
section, as the evidence contract says.

**Before.** Skip files modified in the last ten minutes.

**During.** Fix before disclosing; no new claims are added.

**After.** Log the pass; the durable control is the evidence gate, not the loop. If the loop stops, remaining files stay marked unreviewed.

## R5. Decisions only the owner can make

**Who.** Owner. **What.** Four decisions, each with options and a recommendation (the first option):

| Decision | Options | Evidence |
| --- | --- | --- |
| Recast/DotRecast navmesh adoption | A no dependency (keep the native navmesh); B offline build-time tool only; C runtime dependency | `GAMEBRYO-NAV-RESOURCES-01` and `NAV-ECOSYSTEM-DISPOSITION-01`: no dependency adopted, replacement premature |
| spiraldb data (CC BY-NC-SA 4.0) | A study only; B ask the author; C integrate under share-alike non-commercial terms | `APPROVABLE-IMPORT-BATCH-01` |
| New module drafting | A pause until the ten `integration_pending` workstreams move; B continue with a cap | `workstreams.json`: 20 backlog, 10 integration_pending, 0 complete |
| `libm` for `no_std` | A drop the `no_std` goal (docs already corrected); B add `libm` | `maelstrom-core` does not build with `--no-default-features` (22 errors) |

**When.** Any time; none blocks R1 or R2. **Where.** Your reply in chat; recorded in the matching result document.
**Why.** Each is a dependency, licence or scope choice that `AGENTS.md` reserves for approval.
**How.** Reply with a letter per row.
**Before.** Nothing is adopted or copied until you answer.
**During.** Agents keep the current default (option A) and add nothing new.
**After.** The decision is written into the result document and the claim notes.

## R6. Shared checkout, stale claims and work in progress

**Who.** Owner and the integration agent.

**What.** Four claims are still `active`, all owned by `root`: W101-CHARACTER-27, TEACHING-LONG-SESSION-35,
CARD-PROTOTYPE-81, DANCE-CAPTURE-82. Whether work continues is not recorded. Workstreams sit at 20 backlog, 10
integration_pending, 0 complete.

**When.** Before the next batch of new tasks.

**Where.** `docs/coordination/claims.json`, `docs/coordination/workstreams.json`.

**Why.** Stale claims block other agents from owned paths and hide what is really in flight, which is the
shared-checkout contention risk raised in review.

**How.** The owner marks each claim continue or release; the integration agent releases those with notes. Optionally a
board-gate warning for `active` claims with no dated note.

**Before.** Read each claim's notes and owned paths; no discarding of uncommitted work.

**During.** One claim at a time; touch only `claims.json` plus the claim's note.

**After.** Board gate stays at zero warnings; the active list matches what is truly running.

## R7. Python formatting debt

**Who.** An agent, with the owner's go-ahead (mass formatting is not allowed without direction). **What.** 13 files under
`tools/` fail `black --check` (8 top-level; re-measured today), including `wizdiff_adapter.py`. **When.** `wizdiff_adapter.py`
with R1 P1; the rest when no active claim owns them. **Where.** `tools/*.py` and `tools/` subfolders. **Why.** The
format check cannot be a CI gate while it fails. **How.** `python -m black <file>` per file, one commit. **Before.** Confirm
no active claim owns the file. **During.** Formatting only; run the file's tests. **After.** `black --check` passes for the
file; CI formatting gate considered afterwards.

## R8. History cleanup leftovers

**Who.** Owner. **What.** The deleted backup branch's objects remain in the local reflog (245 reflog entries), and the
local branch `maelstrom/restore-integrations` has an unrecorded purpose. **When.** After PR #77 merges. **Where.** Local
repository only. **Why.** Expiring the reflog is the only irreversible step and removes local recovery. **How.** Review the
branch; if unneeded, `git reflog expire --expire=now --all` then `git gc --prune=now`. **Before.** Confirm the merge and
a good remote state. **During.** Nothing else touches the repository. **After.** Confirm `git status` and the branch
list; no push is involved.

## R9. Navigation data decoding

**Who.** An implementing agent after the owner approves the approach. **What.** `.BCD` role flags are not adopted
(`DESTINATION_NAVIGATION.md` says no `collision.bcd` semantics were adopted); an owned raw reader is the missing piece.
**When.** After R1 P1 and R5. **Where.** A new offline reader and fixtures. **Why.** Navigation clearance depends on it. **How.**
Read file bytes directly and write fixtures from first-party reading, not by copying code whose licence conflicts
(`APPROVABLE-IMPORT-BATCH-01`). **Before.** Licence note and a claim. **During.** Offline only. **After.** Fixture tests and an
evidence note; no claim that navigation is qualified.

## R10. The new response-format rule

**Who.** Every agent working in this repo, including Claude, Codex and Gemini. **What.** Every response states Who,
What, When, Where, Why and How, plus Before, During and After. **When.** From the commit that adds this report.
**Where.** `AGENTS.md`, `docs/coordination/EXTERNAL_AGENT_GUIDE.md`, `tools/check_response_format.py`, CI, and a saved
memory note. **Why.** Plans were missing preconditions, stop conditions and follow-through, which is how drafts
read as finished. **How.** Marker on line 1 plus `## R<n>` sections; the checker lists missing labels. **Before.** Rule
and checker were added together. **During.** Short items are fine; "n/a - reason" is allowed, silence is not. **After.**
The gate runs in CI and over this file; chat replies are not machine-checked, so that part relies on the rule and on review.
