# Wizard101 operating knowledge and version history

Research date: **2026-11-08, America/New_York** (completed after midnight UTC).
Scope: the Windows game selected for Maelstrom, especially navigation, native
Idle, Pet Dance and the wider routine workspace. This is a sourced operating
baseline, not an exhaustive world walkthrough or a live-play qualification.

The companion [structured ledger](../research/w101-context-20261108.json) records
sources, evidence types, known dates, local source hashes and unresolved checks.
It is reference data, not an executable pack or an automatic app import.

## Corrections that govern the next implementation

1. **The ordinary map is a location reference, not a click-to-teleport interface.**
   The user confirmed this, and clicking the Pet Pavilion label in the observed
   Commons map did not navigate. The official guide describes landmarks, maps,
   compass bubbles and separate transport controls. Do not repeat that attempt.
   [Map and compass guide](https://www.wizard101.com/w101playersguide/getting-around3)
2. **Travel is a prerequisite to the teacher.** A functioning Pet Dance sequence
   reader does not get the wizard to its sigil. Maelstrom needs location-aware
   walking and explicit, eligible game transport before unattended routines.
3. **Documentation evolves with the game.** An older page's missing feature is
   not automatically a contradiction. Track feature introduction, source date,
   region, platform and Live/Test Realm separately. Search-engine crawl dates
   are not release dates.
4. **Tiny UI is a separate calibration.** The supplied DanceBot README requests
   Huge UI; its code uses two fixed resolution layouts. Reuse its concepts and
   reviewed arrow images, not its pixel positions or fixed sleeps.

## Evidence and chronology

Use these evidence labels independently: `documented`, `observed-local`,
`user-confirmed`, `legacy-implementation`, `proposed`, and `unverified`.
Documentation can establish an intended mechanic without proving this client's
binding, layout, timing or eligibility. An observation proves only the tested
state. Record exceptions instead of silently replacing either source.

| Evidence | Date and scope | Interpretation |
| --- | --- | --- |
| Official beginner navigation and pet guides | Publication dates absent; checked Nov 8 | Stable mechanic baseline, with legacy details retained on some pages. Their crawl dates do not date the features. |
| Pet introduction lists four training games; training overview lists six | Both pages undated | Consistent with chronological expansion. Use the broader inventory for planning, but do not invent an exact expansion date from the pages alone. [Introduction](https://www.wizard101.com/w101playersguide/pet-games2), [training overview](https://www.wizard101.com/w101playersguide/pet-games7). |
| Summer update report describes native Idle | June 6, 2026; third-party reporting | Historical corroboration, not the primary proof of this machine's UI. [Report](https://www.mmorpg.com/news/wizard101s-summer-update-the-blighted-veil-raid-update-is-live-2000138231). |
| Publisher's Ahmarra announcement includes Idle Wizard in the upcoming EU update | Nov 5, 2026; forward-looking NA/EU rollout announcement | Regional availability matters. An announcement saying an update will launch is not evidence that it already launched everywhere. [Publisher announcement](https://www.play.wizard101.com/news/Ahmarra-Arrives-on-North-American-and-European-Servers). |
| Ahmarra notes describe custom keybinding | Page headed Test Realm, Oct 21, 2026; retrieved on the publisher's Ravenwood Academy domain | A discovery lead for upcoming bindings, not proof that this live W101 client has the feature. The unusual host and Test Realm scope are preserved. [Test Realm notes](https://www.ravenwoodacademygame.com/news/ahmarra-update-notes). |
| Current selected W101 settings and Idle banner | Observed Nov 8 | Settings show 1280 × 720, windowed, Tiny UI. Native Idle is present and was restored after the brief check. Client build, region and DPI were not independently recorded. |
| Supplied DanceBot's Huge UI requirement | Local source revision hashed; original publication date not established | Historical compatibility constraint, not a requirement that Maelstrom change the user's UI. |

The old Test Realm URL redirected to a registration landing page during this
research. A guessed Blighted Veil update URL was inaccessible. Neither response
was used as evidence of mechanics; retrieval failures remain in the ledger.

## Navigation: what the player can actually do

| Mechanic | Documented meaning | Maelstrom requirement |
| --- | --- | --- |
| Walking and turning | W/Up advance, S/Down reverse, A/Left and D/Right turn. Holding both mouse buttons provides mouse-directed movement. [Movement](https://www.wizard101.com/w101playersguide/getting-around1) | Calibrate heading change and distance for the active camera, mount and speed. A/D are not assumed to strafe. |
| Compass bubbles | Landmarks appear around the screen; N toggles them. [Compass](https://www.wizard101.com/w101playersguide/getting-around3) | Candidate landmark detector; bubbles indicate directions, not verified free paths. |
| Local/world map | M opens the local map; the world view is a separate overview. [Map](https://www.wizard101.com/w101playersguide/getting-around3) | Recognize area and topology. Do not dispatch travel from a label's coordinates. |
| Home / common area | Home and End invoke distinct transports; End targets the current world's common area. [Shortcuts](https://www.wizard101.com/w101playersguide/keyboardshortcuts) | Bind destination to world context; End is not universally Wizard City's Commons. Verify destination after loading. |
| Mark / recall | Page Down marks, Page Up recalls; marking uses mana. [Shortcuts](https://www.wizard101.com/w101playersguide/keyboardshortcuts), [marking](https://www.wizard101.com/w101playersguide/getting-around3) | Preserve the user's existing mark. Read eligibility and cost rather than assume an exact cost or unlimited waypoint slots. |
| Teleport stones | Certain areas have discovered point-to-point devices. [Teleport devices](https://www.wizard101.com/w101playersguide/getting-around4) | Store unlock/availability as route conditions; a discovered device is distinct from a map click. |
| Friend transport | Friend location is a separate menu action. [Friend transport](https://www.wizard101.com/w101playersguide/getting-around2) | Optional explicit travel method; it is not needed for the planned solo Commons route. |
| Realms | Parallel copies of areas can be selected outside duels. [Realms](https://www.wizard101.com/w101playersguide/getting-around2) | Track realm changes as context changes, not movement to a different destination. |
| World travel | Spiral doors connect progression-dependent worlds. [World travel](https://www.wizard101.com/w101playersguide/how-to-play6) | Graph edges need eligibility and loading/arrival evidence. The old guide's short world list is not a current world catalogue. |
| Quest direction | The selected quest supplies direction and approximate distance; not every quest supports it. [Quest tracking](https://www.wizard101.com/w101playersguide/questing3) | Treat as a directional observation. It is not obstacle avoidance or a route to any arbitrarily requested destination. |

Important default bindings from the official
[shortcut reference](https://www.wizard101.com/w101playersguide/keyboardshortcuts):
X interact; Q quest journal; I pets; Escape options; Ctrl+T quest tracker;
Ctrl+R or Num Lock autorun. **R alone replies in chat.** Defaults must be checked
against the active client's bindings. The brief check confirmed M, I and Escape;
it did not test autorun, mark/recall or a portal.

### The first route: Fairgrounds to Pet Dance

Observed start: the mana-minigame sigil among the Commons' Fairgrounds tents.
The pet name above the wizard's companion is not a location/NPC identifier.
The official pet guide places the Pavilion entrance in a tunnel near Prospector
Zeke in the Commons. [Entrance reference](https://www.wizard101.com/w101playersguide/pet-games2)

Proposed semantic route, **not yet a validated executable route**:

`Fairgrounds sigil → open Commons path → Pavilion tunnel landmark → loading
transition → Pet Pavilion identity → Dance sigil prompt → game instructions`.

Each edge needs a before image, recognized landmark, short movement, progress
check and timeout. The tunnel's present screen position, the player's heading,
and the final Dance sigil position are still unknown. No turn durations or
coordinates have been fabricated. Use compass cues if they are identifiable;
otherwise gather route examples before committing an edge.

Start with this small route instead of waiting for a universal world navigator.
Save it as reusable landmarks and conditional edges, not a fixed whole-journey
keypress list. Test different starting headings. A change of mount, camera,
window geometry or visible landmark invalidates the relevant calibration.

### Enemy avoidance and arrival

The guide describes encounters being triggered by walking in front of creatures.
This makes reactive avoidance part of navigation, not an optional timing tweak.
[Encounter baseline](https://www.wizard101.com/w101playersguide/how-to-play4)

Design inference: maintain a local free-space estimate, enemy motion/uncertainty,
and a short movement horizon. Stop on a duel transition, lost location, absent
progress, unexpected dialogue, loading, or desktop takeover. A decreasing quest
distance is useful but insufficient: independently recognize the destination
label, entrance transition or interaction prompt. Do not claim an exact aggro
radius until it is measured for the encounter and client.

## Native Idle and desktop ownership

Observed procedure: dismiss the current book if necessary, open Options, click
Idle, and verify **YOU ARE IDLE**. Escape is state-dependent: in the pet book it
closed the book; the next Escape opened Options. Therefore `Escape → fixed click`
is not a universally valid macro. M exited Idle during this check.

The banner was visibly restored and the desktop helper reset before research.
This establishes the observed return state, not an independently measured
infinite login duration. Automatic menu recognition/Idle cleanup is still not
implemented. See [native Idle evidence](W101_KEEP_ACTIVE_REUSE.md).

User takeover outranks cleanup: if the user interrupts control, stop immediately
and leave cleanup marked pending. Do not send more keys under the assumption
that restoring Idle justifies competing with the user. Resume only when a new
desktop-use window is established. Background research needs no input owner.

## Pet Dance and the other pet specialists

The guide describes Dance as repeating shown arrow sequences, increasing in
length, with termination after five rounds or three failures. It does not specify
our matcher thresholds, cue cadence, initial sequence length or exact success
transition artwork. Those remain measurement questions.
[Pet-game rules](https://www.wizard101.com/w101playersguide/pet-games8)

| Pet game | Documented mechanic | Candidate lightweight specialist |
| --- | --- | --- |
| Dance | Recall arrows | Phase recognizer, cue memory and verified arrow delivery |
| Gobbler Drop | Catch food while avoiding falling hazards | Object tracking and lateral avoidance |
| Cannon | Aim with direction, elevation, power and wind | Small trajectory search with outcome fitting |
| Maze | Collect food and avoid ghosts | Maze graph plus moving-hazard tracking |
| Grumpy Gobblers | Aim shots to knock down structures | Trajectory search and scene-change evaluation |
| Way of the Ninja Pig | Finish an obstacle course in time | Motion tracking and timed obstacle responses |

The table's mechanics come from the same
[official pet-game guide](https://www.wizard101.com/w101playersguide/pet-games8);
the proposed algorithms are engineering choices, not tested performance.
Pet Derby is a separate multiplayer racing activity, not one of these six
training specialists. [Derby](https://www.wizard101.com/w101playersguide/pet-games9)

Training consumes Energy, which replenishes over time. Record actual pre/post
Energy and pet experience; do not hard-code a refill rate or per-game cost from
old advice. [Training resources](https://www.wizard101.com/w101playersguide/pet-games7)
For the first teacher test, finish before snack, purchase or unexpected level-up
flows. A successful arrow dispatch is not a successful round.

### Tiny UI qualification checklist

- Record the actual WGC frame size and client size separately from the in-game
  1280 × 720 setting and any resized tool screenshot. Record UI scale, DPI,
  language and camera. Retain full-resolution originals.
- Collect current-client Ready/Watch/Go/result examples plus arrow/empty-gap
  pairs. The names used by Maelstrom are internal labels, not claims about the
  game's exact displayed wording.
- Measure arrow size, neutral appearance, fade duration, cue spacing, repeated
  directions and round-length transitions. Inspect false positives and unknowns.
- Verify all four directions and repeated arrows. The old bot's `turn + 3`,
  fixed waits and shared Play/Next/Feed coordinate are implementation assumptions.
- The native reviewer currently permits scale 0.50–1.50, requires 70 ms stability
  and rejects observation gaps over 150 ms. These are **our current guards**, not
  published W101 timing requirements. Validate them against footage before tuning.
- The passive still sampler supports at most 5 fps; its 200 ms nominal spacing
  cannot meet that gap guard. A faster bounded capture path or a timestamped
  video-derived section is needed. No faster sampler was implemented in this study.
- Keep calibration and independent check sessions separate. Log proposed actions,
  actual key receipts, round outcomes and cleanup independently. Use one input
  owner and one small visual worker; no model is needed in the reaction loop.

## Wider W101 workspace: context before routines

| Domain | Research baseline | Consequence for Maelstrom |
| --- | --- | --- |
| Fairground games | Sigil interaction opens the selector; the guide describes a 20-games-per-24-hours gold/item limit while mana refill continues. [Minigames](https://www.wizard101.com/w101playersguide/mini-games1) | Separate from pet training. Verify actual rewards; don't infer unlimited profitable farming. |
| Potions | Minigames can refill potions; purchased refills are a separate route. [Resources](https://www.wizard101.com/w101playersguide/how-to-play2) | Verify potion/mana change, not merely a score threshold. |
| Quests | NPC markers distinguish available, in-progress and ready-to-complete work; the journal holds objectives. [Quests](https://www.wizard101.com/w101playersguide/questing) | Parse objective state and completion evidence; don't blindly skip every dialogue. |
| Instances | The legacy guide describes several actions that reset dungeons and distinguishes gauntlets. [Instances](https://www.wizard101.com/w101playersguide/questing2) | Treat instance progress as context before Home/Commons/minigame detours; verify current-instance rules rather than universalize old reset timing. |
| Combat | Deck contents can include equipment-granted cards; playable state and turn timing matter. [Decks](https://www.wizard101.com/w101playersguide/spell-cards2), [casting](https://www.wizard101.com/w101playersguide/spell-cards3) | Recognize the current hand, resources and legal targets. Fixed card-slot macros are insufficient. |
| Gardening | Housing plants use Energy and expose needs/pest cues; elder harvest and plowing have different effects. [Setup](https://www.wizard101.com/w101playersguide/gardening), [care](https://www.wizard101.com/w101playersguide/gardening2) | Share a resource budget with pets. Recognize needs and harvest outcomes; keep removal distinct from care. |
| Fishing | Location and lure/bite observations drive the activity. [Fishing](https://www.wizard101.com/w101playersguide/fishing) | Later specialist using small visual state transitions, with its own resource/outcome checks. |

The eight fairground games already have a separate
[mechanics and difficulty study](MINIGAME_RESEARCH.md). Preserve that dated work
and append current-client results rather than duplicate or silently rewrite it.

## Reuse, with clear compatibility boundaries

Read-only inspection in this study; no legacy program was launched:

| Local source | Useful contribution | Boundary / repair |
| --- | --- | --- |
| DanceBot `README.md`, `dance_game.py` | Four-arrow vocabulary and sequence concept | Huge UI, fixed 800×600/1280×800 layouts, fixed delays and merged button roles. Its selectable world backgrounds are game-menu choices, not world-travel routing. |
| Automatus `lib/map.py`, `func/locomotion.py` | Route records, forward/reverse traversal and pause points | Reads client coordinates; raw (0,0,z) records encode both positions and commands. Redevelop as typed edges; add zone validation, progress, bounds and arrival checks. Source uses `os` without importing it in the inspected locomotion file. |
| Deimos `src/teleport_math.py` | Navigation data parsing and geometry concepts | `navmap_tp` calls client teleport; that is not ordinary map travel. The nested absolute-value proximity check loses coordinate signs, as the prior probe found. Reuse reviewed mathematics only after repair. |
| Maelstrom shared scheduler and input lease | One owner, cancellation and persisted outcomes | Already implemented. This does not supply a qualified route or live Pet Dance result. |

Exact local paths and hashes are in the ledger; prior component evidence remains
in [legacy validation](W101_LEGACY_VALIDATION_20261108.md). The broader
[integration map](W101_CAPABILITY_INTEGRATION_MAP.md) still covers every archive.

## Implementation order and acceptance evidence

1. **Game context:** selected client identity, zone, active screen, language,
   UI/capture geometry, bindings, travel eligibility, resources and allowed goal.
   Unknown values stay unknown; public guide text never grants runtime readiness.
2. **Small navigation slice:** qualify the Commons-to-Pavilion route using
   landmarks and short steering, then recognize the Dance sigil. Record failed
   paths and user interventions. No arbitrary coordinate teleport fallback.
3. **Capture and Tiny UI calibration:** save phase/cue examples and actual timing
   coverage. Establish current-client recognition before dispatching proposals.
4. **One bounded live game:** shared owner, fresh observations, finite Energy and
   action budgets, measured key delivery, independent results, verified Idle.
5. **Repeat and learn:** fresh sequences, held-out checks, teacher/student
   comparison and versioned promotion. Only then expand to other routes and games.

Current endpoint: still at the Fairgrounds, **native Idle observed**, helper
released. No Pet Dance round, live cue calibration or autonomous route was
completed. This study advances the specification and evidence base; it does not
change installed app behavior or close the live-teacher gate.
