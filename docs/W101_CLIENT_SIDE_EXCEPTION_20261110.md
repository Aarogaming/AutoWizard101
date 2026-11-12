# W101 reviewed client-side functionality — owner decision

On **2026-11-10**, the owner explicitly confirmed:

> Yes—reviewed client-side functionality; keep network manipulation excluded

This supersedes the earlier read-only interpretation of the W101 exception.
The objective is to reuse the reviewed projects' actual operations as Maelstrom
backends, teachers and reusable components, including necessary client-side
hooks, injection, process reads and memory writes. It is not limited to extracting
ideas or receiving telemetry. Do not ask the owner to approve this architectural
scope again merely because a reviewed W101 function uses these methods.

## Effective scope

| Area | Decision |
| --- | --- |
| General game behavior | Keep UI-only as the default. The exception is specific to W101. |
| W101 client-side functions | Authorized for reviewed integration, repair and testing, including required hooks, injection and memory reads/writes. |
| UI/visual operation | Retain it as an independently usable option and a source of outcome evidence. Client-side observations and controls must identify their actual method. |
| Network manipulation | Excluded. Do not add live interception, crafted/replayed packets, packet injection or traffic rewriting. Normal communication by the game while it runs is not an extra network-manipulation integration. Saved-data decoding remains offline research. |
| Installed game files | Existing read-only installation rule remains. Process-memory authorization does not authorize patching on-disk game files. |
| Activation | Keep client-side functionality optional and disabled by default. Authorization does not silently start a tool, enable unreviewed code or resume a routine. |
| Existing user preferences | Preserve resource limits, cancellation, user takeover, desktop-use boundaries, snack preferences and Crown Shop lockout. |

An approved method and a functioning integration are separate facts. The owner
has settled the method decision; implementation still has to establish the
specific adapter's current-client compatibility and lifecycle. Existing task
scopes govern whether work is offline or includes a bounded live test.

## How this changes the reviewed collection

| Source group | Now eligible for reuse | Remaining qualification |
| --- | --- | --- |
| Deimos, wizwalker, wizSDK | Actual client-backed observation, navigation, questing, combat/farming and pet routines where implemented; isolated teacher backends alongside native implementations. | Trace each selected function and its complete startup/dependency path; fix known defects; bind it to the selected client and shared control owner; verify cancellation, cleanup and outcomes. A repository name does not qualify every function. |
| wiztype and diagnostics | Reviewed client type/state inspection and compatibility diagnostics. | Classify the actual access path, including any initialization hooks; validate version/provenance and bound work. |
| Pet Dance, visual farming, gardening, Bazaar and related routines | Continue functional reuse of visual algorithms and bounded operations; compare client-backed alternatives when available. | Recognition, resource/transaction limits, input ownership and current-game results remain relevant. |
| WAD readers, companion, knowledge exports, damage calculator, wizdiff | Continue data, evaluator and asset/version reuse. Their useful offline work does not depend on the exception. | Data validity, chronology, importer bounds and integration tests. |
| moonlight, packet-map/protocol projects | Saved-file decoding, schema vocabulary and separable asset utilities. A separable non-network client utility may be reviewed under the client-side exception. | Live network interception/manipulation remains excluded; do not enable an entire mixed-purpose program to obtain one acceptable function. |
| Cheat Engine, historical apps and arbitrary plugins | Reviewed reusable components and UX ideas remain candidates. | This decision does not register arbitrary executables, plugin DLLs or every archived binary as trusted runtime code. |

The existing 25-archive map and additional connected sources remain the inventory.
No source review is discarded, and no old qualification is silently promoted.
Alternative implementations can share an operation name and produce comparable
observations/actions/results; they must not control the same client concurrently.

## Next implementation sequence

1. Select one client-backed observation path from the existing wizwalker/Deimos
   integration. Review startup, dependencies, target selection, actual read/write
   behavior and shutdown. Identify network-manipulating functions separately.
2. Add a W101-specific, explicit session configuration shared by C# and Python.
   Identify the pinned adapter and permitted methods. Default settings, imported
   packs and generic automation toggles must not activate it implicitly.
3. Route the adapter through the existing scheduler and exclusive game-control
   owner. That owner must cover client mutations as well as keyboard/mouse input.
   Preserve cancellation, cleanup evidence, restart behavior and manual takeover.
4. Test with fake clients first: wrong/replaced target, unsupported client build,
   stale observations, partial initialization, exceptions, cancellation and denied
   network operations. Avoid a second scheduler or unbounded background polling.
5. Qualify one bounded real operation within the current live-test scope, record
   independent outcomes and resource use, then expand into navigation and teachers.
   Keep the visual fallback and compare outputs rather than assuming equivalence.

These are engineering gates, not requests to reconfirm the owner's exception.
The telemetry contract issues already documented remain useful work, but a
standalone external memory reader is no longer the only eligible implementation.

## Current implementation and validation

This decision update changes documentation and the project board, not executable
admission or installed software. The current C# and Python policies still block
Deimos/wizwalker/wizSDK client-internal launches. They need the reviewed adapter
and session integration above before those guards can be replaced responsibly.
The existing read-only toggle is a skeleton, not the expanded client-side mode.

No legacy process, game memory access, hook, input, capture, installer, model or
desktop control was started for this update. Offline checks validate existing
default admission, plan integrity and documentation consistency; they do not
claim full functional readiness. Counts and command results are recorded in
`.local/w101-client-exception-20261110/validation.json`.

The 8 Python policy tests and 22 board tests pass. The map validates all 25
archives / 29 sources / 17 operations. Documentation whitespace and local links
pass. A no-build native rerun produced no completion result and was interrupted;
it is not counted as a new pass. All six policy/provider source and test files
still match the hashes from the earlier 14-passing-native-test review. No runner
was left active.

The earlier [telemetry review](W101_TELEMETRY_REVIEW_20261110.md) and
[UI-only audit](W101_UI_ONLY_BOUNDARY.md) preserve historical implementation
evidence. Their superseded scope decisions do not override this decision.
