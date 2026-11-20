# Reusable World Gate Travel card — 2026-11-12

Status: source/offline verified; one configured Zafaria/Home round trip live verified. Parent W101-NAVIGATION.
Owned under the coordinator's W101-CHARACTER-27 continuation.

## Implementation

- `w101.travel` is a configurable card in the existing routine registry. Destination,
  optional Home return, 0–120 second Home availability wait and 1–8 page limit.
- Same prepared operation is callable from CLI `travel-card --selection <card.json>`.
  Explicit client/UI/action opt-ins remain mandatory. Deck connection has a separate,
  default-off Travel permission, including a checkbox in the existing connection UI.
- Exact World Gate prompt, registered X, actual Spiral Map controls and fresh OCR.
  Select the exact world label; require two UI-tree observations of the selected
  check mark aligned with its world row before Go To World. Missing worlds/page
  cycles and ambiguous geometry stop selection. Page arrows calibrate against
  current-frame world labels and parent-relative UI rectangles; no fixed pixels.
- Loading edge and cleared gameplay must be observed. Selected-world verification
  is not yet an independent zone-identity reader.
- Home observes the exact HUD GoHomeButton and txtGoHomeTimer paths twice. A visible
  timer or disabled button waits; missing/conflicting evidence grants no action.
  One Home command, observed loading, house controls and return within 300 game
  units of the recorded house-gate origin are required. Return-Home mode currently
  requires starting at a house gate; public-gate travel can disable Home return.
- One scheduler/input owner, countdown, target/takeover guards, default shared Idle
  finalizer (including early failures), no focus-recovery override, no purchases.
  Hard cap 300 seconds; no models or additional production dependencies.

## Evidence and limits

Reader-only probe ce3c57f6ea4a4cfd82d39d57f5b0d361 exposed an initial HUD pruning
exclusion; repaired with regression coverage. Follow-up probe discovered the real
GoHomeButton and txtGoHomeTimer controls, disconnected cleanly. Gate inspection
11572aeb4ddc42098077c1f19cec8edf stopped on foreground/user activity before input
while the owner warned of a concurrent 3D workload. No takeover bypass.

Synthetic tests cover bounds, exact selection geometry, ambiguous/missing marker,
page calibration, timer overriding enabled status, unknown availability, house
origin matching, catalog availability and CLI opt-ins. Reader suite:64 passed.
At that checkpoint, no active-cooldown or destination-selection live claim existed; see the final live result below. Previous
separate-call Khrysalis/Home round trip belongs to W101-CHARACTER-27, not this card.


## Final live qualification and checks

- First configured-card attempt 3697d5a5d20f40c684b7ea9eafd2a4da stopped at
  foreground admission before inputs. Owner then explicitly brought W101 forward.
- Authorized retry c87172cb2a0f47319f004456c3bd7be0 completed under one CLI card,
  scheduler lease and countdown. Exact Zafaria label selected; two UI observations
  verified its check mark. Loading and destination gameplay observed; saved pixels
  show Baobab Market, Baobab Crown, Zamunda and Savannah landmarks. This is visual
  corroboration, not a new machine-readable zone-identity capability.
- Home readiness was true on the first poll. Exactly one Home command followed,
  loading cleared, house controls and origin proximity verified, then shared Idle
  finalization independently observed native Idle. Final pixels show the house.
- The captured ready trace has GoHomeButton enabled and txtGoHomeTimer hidden;
  the final post-Home trace has the button STILL enabled but timer visible. Thus
  cooldown detection must inspect the timer, not merely the button's enabled flag.
  Synthetic tests cover that case. Waiting through an active live cooldown and
  traversing other menu pages remain separately unqualified.
- All owned live processes were checked absent; no desktop-control session,
  models, volume changes, installation, purchases or combat were used.
- Full native suite: 2478 passed, zero failed/skipped. Python reader suite:64 passed.
  Warnings-as-errors compilation, scoped whitespace format/verify and analyzers,
  Python Black/compileall and board validation passed.

Local configured card: `.local/w101-cycle-06/travel-30-card-zafaria.json`.
Local final CLI result: `.local/w101-cycle-06/travel-30-zafaria-2.json`.
No installation/package was requested or performed. The configured card requires
fresh prompt/house evidence and separate Travel permission on every connection;
this does not qualify arbitrary world routes or general auto-questing.
