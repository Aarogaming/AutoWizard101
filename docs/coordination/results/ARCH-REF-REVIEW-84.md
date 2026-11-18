# ARCH-REF-REVIEW-84 — review of four third-party "adjacent architecture" texts

Date: 2026-11-18 · owner: root · claim: `ARCH-REF-REVIEW-84`
Status: **released evidence.** Coordinator review of four Gemini-generated
recommendation/review texts (two from browser, two from antigravity) that propose
open-source game/robotics/RPA architectures for Maelstrom.

## Method

Every repository citation, file path, line quote, binary offset and test number in
the four texts was independently re-checked against this checkout by root: grep/read
of each cited path, re-execution of the binary signature scan, re-run of the claimed
test suite, live GitHub API fetch of each named upstream repository (licence field
read from `license.spdx_id`, 2026-11-18). No claim entered this document on the
source text's word alone.

## Verdicts

| Text | Verdict |
|---|---|
| Browser #1 — 4-project pitch (baritone, l2r, GW2-Nexus-Pathing, ExileApi) | **Reject as written.** Headline premise (sidewalk immunity) contradicts our own audit; applications target capabilities that do not exist or that we rejected. |
| Antigravity #1 — zero-stub proof report | **Mostly sound.** Every artifact number independently reproduced; one fabricated exception type, one "proven" claim that is not, one stale leak description. |
| Antigravity #2 — comparative architecture matrix | **Best of the four.** Keep/reject dispositions align with the codebase; six citation defects, two of them fabricated code claims. |
| Browser #2 — MMO-AI / RPA / robotics pitch | **Reject as written; inventory the repos.** Two wrong licence claims, one stale URL, and three applications that misattribute our architecture or exceed approved boundaries. |

## Corrections register

Corrections are stated as claim → reality, each verified on 2026-11-18:

1. **"Walking on the sidewalk is safe"** (Browser #1 premise) →
   `docs/coordination/results/W101-KNOWLEDGE-AUDIT-01.md:37` states sidewalk immunity
   is an unjustified absolute; sidewalks are a route *preference*, not a safe zone.
2. **".BCD identifies streets vs sidewalks"** (Browser #1) → `REF-STUDY-BCD-ZONE-01.md:12`
   shows only physics flags (`WALKABLE`, `CLIENT_OBJECT`, `TRIGGER`, `WATER`…); line 14
   records that our own BCD tooling does not read raw flags at all.
3. **"`UiCompatibilityError("client-build-unreviewed")` swallowed by the bare catch"**
   (Antigravity #1, called a "Proven Fact") → `UiCompatibilityError` has **zero C# hits**
   repo-wide (Python-only, `tools/w101_ui_tree.py`). C# throws
   `InvalidOperationException("The restarted W101 build needs UI-adapter compatibility
   review…")` (`WizwalkerOperation.cs:62`) or fails with the reviewed-reader message
   (`W101UiTreeOperation.cs:168–170`). What is proven: the bare catch
   (`W101FactsMapper.cs:114–118`) collapses every non-cancel exception into
   `observer-fault`; the specific swallowed exception was never captured.
4. **"TTL expiration living inside `W101Fact` (`expiresUtcTicks`)"** (Antigravity #1)
   → no such field exists; expiry is computed at read time from `FreshnessSeconds`
   (`W101FactsStore.cs:67`). The conceptual leak stands; the named field does not.
5. **`W101Fact("mob-actor", …)`** (Antigravity #2) → fabricated; zero `mob-actor` hits.
6. **"`PathFollower` dispatches bounded `W` pulses under `GameInputLease`"**
   (Antigravity #2) → `PathFollower` has no key dispatch or lease reference; it
   validates `PathMotionReceipt` records. Held-key movement lives elsewhere
   (e.g. `HomeMovement.cs`).
7. **"`POLICY_BOUNDARY`"** (Antigravity #2) → not a code symbol; only in
   `docs/TOOLKIT_RECON_NOTES.md`. `ExecutionPolicyManager` does exist
   (`GameWorkspaceForm.cs`).
8. **"`.NAV` waypoint node tags distinguish street vs sidewalk"** (Antigravity #2)
   → `NavigationMap.cs` has no node tags; unsupported.
9. **"`OverlayHudForm` click-through capable"** (Antigravity #2) → click-through
   exists only in `ActionStartNotice.cs:7`; `OverlayHudForm` has `TopMost`/`Opacity`
   only.
10. **GW2-Nexus---Pathing "never injects DLLs, out-of-process overlay"**
    (Antigravity #2) → the cited repo is an **in-process Nexus addon DLL**
    (`DllMain`, ImGui draw-list) reading camera via MumbleLink; original TacO was the
    out-of-process pattern. The two were conflated.
11. **"`W101BackgroundReader` can use UIA principles to overlay its HUD"**
    (Browser #2) → no UIA/UIAutomation usage anywhere in the tree; the background
    reader is memory-only ("No input, capture, network or game-file access lives
    here"); the overlay is `OverlayHudForm`. Window move/resize recovery already
    exists natively (GetWindowRect usage in `Main.cs` etc.).
12. **Licence/URL claims** (Browser #2) → see upstream table below.

## Upstream verification of the eleven named repositories (GitHub API, 2026-11-18)

| Repository | Claimed | Verified | Note |
|---|---|---|---|
| `cabaletta/baritone` | — | exists, **LGPL-3.0** | Java Minecraft pathfinder; cost-heuristic idea only |
| `l2r-dev/l2r` | Rust A* geodata | exists, **Apache-2.0/MIT**, 30 stars, "not intended for production"; README credits the generic `pathfinding` crate | server emulator; grid conversion rejected |
| `YoruDev-Ryland/GW2-Nexus---Pathing` | out-of-process overlay | exists, **MIT**, but an **in-process addon DLL** using MumbleLink | world-to-screen math is the transferable part |
| `exApiTools/ExileApi-Compiled` | C# entity reader | exists, fork of `TehCheat/PoEHelper`, **compiled-binaries dump** (DLLs, 1 commit), no licence field | source study needs the upstream repo |
| `ruslanmv/BOT-MMORPG-AI` | **MIT** | exists, **Apache-2.0** ✗ | licence claim wrong |
| `facebookresearch/Polygames` | MIT | **404 → `facebookarchive/Polygames`, archived, MIT, last push 2021** | stale URL, project archived |
| `a-real-ai/pywinassistant` | **Apache-2.0** | exists, **MIT** ✗ | licence claim wrong |
| `kelaberetiv/TagUI` | Apache-2.0 | **Apache-2.0** ✔, canonical owner is **`aisingapore/TagUI`** (redirect) | PHP RPA tool |
| `open-rpa/openrpa` | MPL-2.0 | **MPL-2.0** ✔ (C#) | orchestration reference only |
| `ros-planning/navigation2` | Apache-2.0 | canonical owner **`ros-navigation/navigation2`**; GitHub licence field = *Other/NOASSERTION* | Apache-2.0 stated in project docs; verify before any use |
| `introlab/rtabmap` | BSD | exists; GitHub licence field = *Other/NOASSERTION* | BSD claim not asserted by the API; verify LICENSE file before use |

## Governance notes (Browser #2)

- **Nav2-via-C++-FFI into the Rust workspace** = new production dependency; requires
  owner approval per AGENTS.md and a source/build claim. Not adopted.
- **"Hook into DirectX/OpenGL to read the Z-buffer"** = a graphics-API hook surface
  not part of any reviewed integration; the current capture path is out-of-process
  Windows.Graphics.Capture (`WindowObserver.cs`). Would need an explicit reviewed
  scope under `docs/W101_CLIENT_SIDE_EXCEPTION_20261110.md` before any experiment.
- **"Run multiple Wizard101 accounts in a coordinated farming group"** (OpenRPA
  pitch) = conflicts with the single-owner input lease and is out of scope for this
  project. Noted, not adopted.

## Evidence

- Binary scan re-executed (`.local/scan-verify.py`, `.local/scan-verify2.py`): size
  57,099,416; SHA `818dbe66…`; root-hook pattern 1 hit `0x189340b` with
  `49 8b 8d d8 00 00 00` = `CURRENT_ROOT_ENTRY`; scratch pattern 1 hit `0x17ea860`,
  `0xC3` at +4161; `profile_for()` refuses `client-build-unreviewed`.
- `python -m unittest tools.tests.test_w101_ui_tree` → **83 tests, OK, 29.987 s**
  (independently reproduced from Antigravity #1's 83/0 claim).
- `tools/fixtures/combat_ui/combat-ui-observations-v1.json` → `len(nodes) == 143`
  (confirms the "143-node fixture" claim).
- `docs/coordination/claims.json` → `W101-CLIENT-REVIEW-83` first occurs at line 4291,
  owner root (confirms the claims-registry citation).
- Grep results quoted above were run 2026-11-18 from the repository root.

