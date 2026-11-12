# Pet Dance packaged player — 2026-11-09

## Current result

The repaired update is **installed**. Native tests: **842 passed**; scoped format
and analyzer checks passed. Installation/update/uninstall preservation checks
passed, and all **512 pre-existing user-data files** retained their SHA-256 hashes
when this final update was installed.

After the user restarted W101, the frozen repaired build achieved **three
consecutive perfect games: Huge → Tiny → Huge**, each with five successful rounds,
25 keys and supervised native Idle confirmed. The planned five-game changed-size
batch remains incomplete: the user stopped desktop use before the fourth game.
Earlier failures below remain part of the evidence; this is not universal 100%
qualification. Across development builds, seven of eight games were perfect.

Last positively observed state was native Idle at Huge UI; current state after
the interruption was not rechecked. Original Large UI remains to be restored at
the next authorized live session. Volume was unchanged; no snacks were fed.

Subsequent offline development adds native Idle as a player operation, pet
preferences, context-aware recommendation/energy planners and a dedicated Dodge
workspace. Those changes have **not been live-qualified** by the above games.
See [postgame operation](PET_DANCE_POSTGAME_OPERATION.md).

## Live evidence, including failures

All actual runs were windowed 1280 × 720, first Wizard City arena, selected-game
audio enabled, Windows display scale 125%. The supervisor used menus only and
sent no answer arrows. Each game used newly observed random sequences.

| Run suffix | Build | UI | Visible game result | Rounds / keys | Visit cleanup |
| --- | --- | --- | --- | --- | --- |
| `004552-17cdf5c9` | Initial packaged player | Large | PERFECT | 5 successes / 25 | Completed, Idle confirmed |
| `004853-3855073c` | Initial packaged player | Large | PERFECT | 5 successes / 25 | Completed, Idle confirmed |
| `005205-243b83f8` | Initial packaged player | Tiny | PERFECT | 5 successes / 25 | Failed: supervisor missed old 60-second confirmation; Idle visually reached |
| `010300-02205a44` | Idle confirmation repair | Tiny | PERFECT | 5 successes / 25 | Completed, Idle confirmed |
| `010648-989f7695` | Idle confirmation repair | Huge | TRY HARDER | 1 observed success / 3 | Completed cleanup, failed gameplay; Idle confirmed |
| `014009-5b484325` | Frozen audio repair | Huge | PERFECT | 5 successes / 25 | Completed, Idle confirmed |
| `014322-b9cd07aa` | Frozen audio repair | Tiny | PERFECT | 5 successes / 25 | Completed, Idle confirmed |
| `014643-6e2fdbd4` | Frozen audio repair | Huge | PERFECT | 5 successes / 25 | Completed, Idle confirmed |

Full run identifiers and independent UI observations are in
`.local/dance-release-20261109-v5/live-ui-review.json`. Native reports are under
`%LOCALAPPDATA%/Maelstrom/reports/pet-dance/`. Cleanup completion is not a game win.

The successful live responses took at most approximately **2.104 seconds** for
the longest sequence, against the player's five-second response target. Mean
visual analysis was about 4.2–4.9 ms per admitted frame in these runs; this does
not measure whole-PC load or guarantee capture throughput. One point-in-time
sample of parent plus trial memory was approximately 240 MB, not a peak measure.

## Repairs made from this evidence

- Native Idle confirmation now has a visible two-minute countdown. Capture and
  input stop before this period. The scheduler budget includes it, and a failed
  cleanup no longer exits with a success status. One live Tiny game verified it.
- The failed Huge run had a **572 ms observation gap** at preview onset. Audio
  recorded Left, Left, Down, Right, but the old Watch boundary excluded the first
  Left. Audio recovery now allows at most **750 ms**, bounded by the immediately
  preceding observed result/resolving frame. Actual Watch and fresh Go remain
  required, as do cue count, confidence, continuity and causal availability.
  Missing visual support is explicitly recorded; no Watch frames are fabricated.
- One dropped audio packet subsequently stopped the old player. Optional audio
  now permanently disables itself for that game on stream failure while complete
  qualified visual sequences can continue. Unfinished sound-only responses are
  discarded. The legacy sound-led path still requires healthy audio.
- Spectral analysis no longer holds the lock shared with visual observation.
  Visual observation never waits on that lock. Queue and evidence limits remain
  bounded; no model service, enlarged queue or new production dependency was added.

The capture stall's cause is unproven. The later desktop capture error was
`Direct3D11CaptureFramePool.CreateFreeThreaded`, `0x887A0005`, with a suspended
GPU-device message. A fresh control session restored screenshots, but the game
itself remained white. No driver, security, volume or game installation settings
were modified to work around it.

## Validation of the installed repair

- 842 native tests passed with warnings treated as errors; scoped formatting and
  analyzers passed. Installer's 14 tests passed during this release work.
- New tests cover the exact Huge recording, bounded onset recovery and prohibited
  boundaries, missing/weak/late/discontinuous audio, and permanent audio degradation.
- Installed executable replay retained **20/20** visual sequences from the four
  perfect recordings, with no input sent.
- Installed sound replay recovered the actual failed second-round sequence
  **Left, Left, Down, Right**. It is counterfactual evidence, not a corrected win.
- Published installer lifecycle passed install/update/uninstall in isolated
  locations, preserving packs, reports, audio, recordings, experiments, routines
  and operation-history sentinels.
- Real update preserved all 512 inventoried user-data files. Installed assembly
  and preset hashes match the built artifacts.

## Frozen installed artifacts

Portable folder: `.local/releases/Maelstrom-20261109-201913-aa49ab/`.

Portable ZIP SHA-256:
`6EEBC70C1C46217093B6FB4935C15FED36DA7A471DDDFA0DEAF953B8F790599F`.

Installer: `.local/releases/Setup-20261109-201947-7f13ab/MaelstromSetup.exe`.
Its adjacent `SHA256.txt` records the installer hash.

Installed version directory:
`%LOCALAPPDATA%/Programs/Maelstrom/versions/1a2a87c27da74321b5531f5d3d9e9548`.

Assembly SHA-256:
`880534B2786CB9685A733F25D50BE5E42AD34B086D09C15423AAB6BE06A162B6`.

Unchanged preset SHA-256:
`08FE79569B4B8445DC6509356260CB118D87114A84BD113DF922CE2D5154538C`.

Unchanged audio comparison SHA-256:
`AEA53362D10F6131725AD8C582F33B4285B9E5EC76BB8B2256F1364D7DB6D37E`.

Release evidence: `.local/dance-release-20261109-v5/`. Files containing
`audio-repair` and `installed-` describe this binary. Earlier `-final` names
refer to superseded pre-live packages; they are retained as historical evidence.

## Resume the frozen live batch

After W101 renders normally again, reselect its new process/window identity and
confirm windowed 1280 × 720. Do not reuse a prior game handle after a restart.

1. Use the installed player under Wizard101 → Observe & teach → Pet Dance player.
   Game controls and selected-game audio were enabled explicitly through the UI.
2. Start a fresh batch: **Huge → Tiny → Huge → Tiny → Large**. Require five
   consecutive complete PERFECT games on the same binary and preset, with fresh
   previews and a UI-size change between every game.
3. Verify each visible result separately from key receipts. Require all 25 rounds,
   125 released answer keys and each answer within the five-second target.
4. After each game use Next → Finish → game menu → Idle, observe YOU ARE IDLE,
   then confirm in Maelstrom before its two-minute countdown ends. No snacks.
5. Keep every failure, reset the streak after failure or a new build, bound each
   batch to ten attempts or twenty minutes, and retain an energy reserve. Restore
   Large UI and native Idle at the end. Do not launch parallel models or benchmarks.

This is still a supervised menu workflow, not an unattended endless farming
loop. Five successful varied games would demonstrate the tested conditions,
not certainty across all sequences, resolutions, machines or future updates.
The usage guide is [PET_DANCE_PLAYER.md](PET_DANCE_PLAYER.md).
