# W101-CHARACTER-26: selection evidence, profiles and loading recovery

Status: partial source integration and bounded live qualification; not a finished unattended deck card. No installation performed.

## What ran

- `character-26-observe.json`: current Death wizard native Idle; clean read-only disconnect.
- `character-26-selection.json`, operation `ef7b2979d738457c8c7a8912aac87d7b`: countdown, owned wake/menu/Quit, selection screen captured. No Play in this stage.
- Selection tree exposes exact `Root/WorldView/mainWindow/sprBanner/txtName` and `sprSubBanner/txtLevel`, `txtLocation` paths. Narrow typed text reads were added and pinned. Three live readings agreed on Luke SkyWalker, level 10 Initiate Necromancer, Dorm Room.
- Roster trials retained a geometry admission failure (1282x759 actual capture), a desktop takeover, then five bounded seat probes. They found Luke SkyWalker level 106 Exalted Pyromancer in Kondha Desert. The owner confirmed the level change. Multiple wizards share Luke's name; name alone is insufficient.
- `character-26-enter.json`: selected Fire wizard and dispatched Play. A later game-window frame (`f0c14b9268d645d1ae5fdacf9f47d32d/frame.png`) visibly verifies Luke SkyWalker, level 106, Fire emblem in the loaded world. The character page and Crown Shop were open. UI-tree unavailability interrupted the program before automatic identity qualification/Idle; input receipt alone was not counted as success.
- `character-26-verify.json`: bounded attempt could not obtain destination trees; no success claim. Root cause of those panel-tree failures remains unqualified; allowlisted failure diagnostics added.
- `character-26-home.json`: cancelled during countdown on desktop activity. The owner manually ported home and entered Idle. This is **user-assisted home arrival**, not Maelstrom navigation success.
- `character-26-home-observe.json`, operation `264d516e4b444365bb4958c3d09f759d`: three read-only trees confirm native Idle and clean disconnect. Home location is owner-confirmed; the proposed OpenObjectPlacement marker is absent here and is not a universal house detector.

All receipts remain under `.local/w101-cycle-06`; no game controller remains running from these checks.

## Source changes

- Wizard profiles use stable local IDs, mutable last-known level, independent house lists and explicit house provenance/layout qualification. Refreshing a level preserves existing house settings and rejects a conflicting identity for the same ID.
- Luke's profile is saved at `.local/w101-cycle-06/live-data/wizard-profiles/9d82d54801774708b7294a837216cfbc.json`. Its equipped-home entry is owner-confirmed; no routes or layout have been qualified. This is persisted profile data, not a claim that all existing routines consume an active-profile pointer.
- Bounded CLI qualification stages reuse the scheduler, shared input owner, takeover guards and countdown. Frame-only diagnostics do not activate or send inputs.
- `LoadingTransitionGate` waits through actual loading and missing evidence, clears earlier readiness, requires two fresh destination observations, and retains its original deadline. It never resends Play/Home. It is connected to character world/home waits. Unreadable panels are unknown, not falsely classified as loading. Shared routine graphs already have explicit Loading scenes; this does not claim every legacy exception path has been migrated.
- A clean unavailable character-page tree can fall back to pixels and proceed to panel closing instead of abandoning the open page. Cleanup uncertainty still blocks further inputs.

## Limits and next work

The deck's Switch Wizard card remains preparation-only. CLI stages are qualification tools, not a completed single-call switch loop. Roster seat candidates are reviewed for one exact 1282x759 window capture, and the current title-to-school matcher qualifies Fire only. General seat detection, other schools, reliable post-load panel handling, house identity detection, complete single-owner orchestration and deck binding remain necessary. The Home stage requires a verified open character page and must not be offered as a general recovery action. Do not repeatedly retry it from Idle.

No profile is allowed to borrow the dorm route. No purchases, snacks, character deletion, character creation, volume changes, game-file writes or network manipulation were performed.

## Verification

Native full suite: 2,425 passed before the final profile-refresh refinement; final result recorded below. Focused native checks passed. Python reader: 59 tests passed before the allowlisted diagnostics addition; final result recorded below. Scoped formatting/analyzers and board validation are required before releasing the claim.


Final checks: all 2,425 native tests passed with warnings as errors; all 59 synthetic UI reader tests passed. Scoped C# whitespace verification and analyzers passed. Single-worker Black check, Python compileall and the 30-workstream board check passed. Restricted formatter subprocess attempts were stopped and rerun successfully with local subprocess access. No production dependency, installer or live gameplay loop was left running.

Claim released with the unattended orchestration/deck/house-detector gates explicitly open. The latest state is owner-assisted home plus independently observed native Idle, not a completed autonomous home cycle.
