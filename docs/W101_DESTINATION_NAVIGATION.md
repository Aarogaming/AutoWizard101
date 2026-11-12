# Destination navigation and between-test Idle

User requirement, 2026-11-08: select a destination, walk or use an allowed in-game
port to get there, and avoid enemies while the user is away. This is planned
capability, not functionality supplied by the existing short motion editor.
During the earlier legacy review W101 received no input. A later bounded desktop
check inspected the Commons map and settings, then restored native Idle and
released control. No route or Pet Dance round was completed. See the dated
[game research and version history](W101_GAME_RESEARCH_20261108.md) and its
structured source ledger for the current travel baseline.

The ordinary map is informational; clicking a destination label is not travel.
Walking, Home, the current world's common-area return, mark/recall and eligible
transport devices are distinct actions. The first route to qualify is from the
current world's Commons port anchor through the Pavilion tunnel to the Dance sigil. Map nodes
need recognized landmarks, progress and arrival checks, not fixed click targets.

The user offered a [recorded route demonstration](W101_ROUTE_DEMONSTRATION.md).
Use its input durations as movement candidates between verified landmarks, with
loading, camera drift and manual takeover checks. A bounded check successfully
used the Commons port, then restored native Idle. The walking route and Dance
round remain unqualified; the conspicuous tunnel on the anchor's right is the
Shopping District, not an established Pavilion waypoint.

## Pack context

- Destination ID, expected world/zone and visual arrival evidence.
- Allowed travel methods: walking, the game's normal home/mark/friend transport,
  each enabled separately. A memory-coordinate teleport from a legacy bot is a
  distinct capability, never silently substituted for normal in-game travel.
- Route alternatives and landmarks; calibration for client pixels, UI scale,
  Windows DPI, camera/mount state and movement bindings.
- Enemy/encounter exclusion zones, uncertain-observation policy, time/distance
  budget, stuck threshold and a known safe stop condition.
- Restrictions on combat, purchases, energy, dialogue and other-player actions.

## Event-driven execution

Recognize the current zone and position confidence; choose the next reachable
landmark; propose a short movement; verify actual progress. Recompute after every
observation, including enemy motion, loading, obstacles and camera changes. Local
avoidance can interrupt the route. A fixed list of identical timed key presses
does not satisfy this requirement. Unknown location or a blocked path pauses
movement with all keys released; it does not guess a distant teleport target.

Keep one action owner for navigation, teachers and Auto-Idle. Manual desktop
activity pauses foreground automation. Background/isolated operation requires a
separately qualified backend; Windows foreground focus is not bypassed by a
routine definition. No large model is required in the reaction loop.

## Reusable candidates and known gaps

Deimos supplies navigation-graph parsing and geometry; both supplied revisions
fail the opposite-sign coordinate proximity fixture. Automatus supplies waypoint
sequencing and pause points, not moving-enemy avoidance. wizAPI supplies visual
quest-arrow/friend-menu ideas but uses old fixed geometry and unbounded waits.
wizwalker supplies optional client primitives that still need version-specific
qualification. Keep each under its own ID in the legacy catalogue.

## Qualification sequence

1. Offline route/obstacle fixtures: multiple layouts and enemy trajectories,
   blocked exits, stale frames, wrong zones and failure to move.
2. Private home: short bounded routes, varied starts and camera headings;
   verify arrival visually without combat or other-player interaction.
3. Repeat routes with randomized obstacles in an isolated test environment.
   Record success rate, collisions, interventions and latency before expanding.
4. Only after the previous gates, qualify real W101 travel in an explicitly
   chosen area and separately test normal game transport.

After every live test: stop the action owner, release keys, use W101's screen
menu **Escape → Idle**, verify the **YOU ARE IDLE** banner, record that evidence,
and release desktop control. If the client is logged out or the menu cannot be
verified, report that state; do not claim Idle was restored. The composed CLI navigation visit now restores native Idle under its own owner;
left/right pulse visits were independently verified by the user. The new bounded
retry and quiet-recovery extension is offline-tested and still needs a live check.


## Observed correction choices

Navigation segments may now supply up to four optional `corrections`, each with
an exclusive observed scene and registered action ID. A route can turn toward
alignment, move forward, and correct drift from subsequent observations without
hardcoding one sequence. All correction outcomes require a newer observation
bound to the released proposal. Turns cannot directly prove arrival; arrival
must follow the segment movement and its independent destination cue. Corrections
consume the same action/time budget and pause on unknown/loading states.

The optional field is omitted when absent, preserving older serialized packs.
These are synthetic contract tests, not a W101 route recognizer or qualified
Commons-to-Pavilion route. A trusted provider of route-relative cues remains
required; floating labels and animation changes alone are not sufficient.
