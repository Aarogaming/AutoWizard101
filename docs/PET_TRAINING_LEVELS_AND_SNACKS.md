# Pet Dance levels and snack outcomes

Research and qualification snapshot: **2026-11-09 (America/New_York)**.
Run identifiers below use UTC and therefore begin `20261110`. This extends
[the snack research](W101_PET_SNACK_RESEARCH_20261108.md),
[configuration contract](W101_PET_SNACK_CONFIGURATION.md), and
[read-only planner](PET_SNACK_PLANNER.md); it does not enable feeding or certify
unplayed arenas. All game observations and actions remain UI-only.

## What different Dance levels mean

Use **arena**, not an assumed easy-to-hard difficulty number. Wizard City,
Krokotopia, Marleybone, MooShu and Dragonspyre are historically documented
choices. A May 25, 2011 player post describes different mixes of awarded stats,
but it is a community suggestion on the official forum, not a current publisher
specification. Its proposed changes must not become our reward table.
[Historical arena report](https://www.wizard101.com/forum/the-dorms/rearranging-points-awarded-during-pet-games-35779/198278).

Music does change: KingsIsle explicitly identifies Krokotopia and MooShu Dance
themes with their corresponding Dance levels in its January 18, 2016 article.
**Engineering inference:** the existing tone detector must be checked against
each arena's background music; success in Wizard City does not establish the
same false-positive rate elsewhere.
[Publisher music article](https://kingsisleblog.com/2016/01/18/the-wizard101-soundtrack-to-everyday-life/).

No accessible current publisher source retrieved in this review establishes
per-arena sequence lengths, exact Watch/Go durations, or the complete reward
matrix. Wizard101Central pages could not be retrieved. Do not encode a guessed
five-second/five-second cycle or assume that changing the background makes
the game harder. Detect actual phases, store observed boundaries and latency,
and compare fresh randomly presented sequences. Measure game rewards separately
from snack rewards and active event modifiers.

## Qualification matrix

The local [run ledger](../.local/dance-release-20261109-v5/live-ui-review.json)
records three consecutive perfect games on the frozen `audio-repair` build:
Huge, Tiny, Huge. These were Wizard City, 1280 × 720 windowed, with audio backup,
no supervisor arrow inputs, no snacks, and native Idle confirmed afterward.
Earlier-build failures remain in the ledger. The planned five-game changed-UI
batch is **not complete**.

| Arena | Tiny | Large | Huge | Reward/timing evidence |
| --- | --- | --- | --- | --- |
| Wizard City | One perfect game on current frozen build; earlier-build successes | Earlier-build successes; current-build repeat pending | Two perfect games on current frozen build | Recorded Wizard City evidence only |
| Krokotopia | Unqualified | Unqualified | Unqualified | Measure; different music documented |
| Marleybone | Unqualified | Unqualified | Unqualified | Measure |
| MooShu | Unqualified | Unqualified | Unqualified | Measure; different music documented |
| Dragonspyre | Unqualified | Unqualified | Unqualified | Measure |

In those three current-build results, displayed Agility advanced 54 → 58 →
62 → 66 and within-age XP advanced 43 → 47 → 51 → 55. That is an observed +4
change in each displayed value per game for this pet and session, not a universal
reward formula or evidence about other arenas.

Complete the existing Wizard City batch first. Then establish each other arena
at Large without feeding, followed by Tiny and Huge. Record selected arena
evidence, resolution, scale, profile/build identity, every round's presented and
sent sequence, feedback, lost observations, response latency, result, resource
changes, and native-Idle confirmation. Preserve failed attempts. Proposed
release criterion: five consecutive perfect complete games per arena while
changing UI size, with the final build frozen; a new build resets that batch.
This is an application acceptance criterion, not a claim of statistical certainty.

## Snack mechanics that matter to the workflow

- **Training and Happiness are separate outcomes.** Pet games grant experience
  and require Energy. The official feeding guide places training snacks after
  a completed game and describes stat/rank gains and ate/liked/loved feedback.
  Liked/loved snacks grant Power according to that guide. These undated pages
  provide a baseline, not a calibrated current-client conversion formula.
  [Training](https://www.wizard101.com/w101playersguide/pet-games7),
  [feeding](https://www.wizard101.com/w101playersguide/pet-games6).
- **Happiness is a distinct resource.** KingsIsle's April 22, 2020 announcement
  says it fuels pet actions such as activating talents and introduces mastery
  as a separate training convenience. Do not interpret a Happiness increase as
  training XP or silently replace Dance practice with mastery skipping.
  [Publisher announcement](https://www.kingsisle.com/wp/press/playwithpets/).
- **Rank alone is insufficient.** Keep each snack's observed identity, rank,
  school/category, base XP, five-stat vector, preference feedback, quantity and
  reserves. Community guidance gives different XP values for individual mega
  snacks; it also describes mastery skipping as spending Energy without game
  XP. Neither rank nor the word “mega” is permission to consume an item.
  [Ravenwood Academy guide](https://ravenwoodacademy.com/wizard101-pet-training-guide/).
- **Targets require the selected pet's actual limits.** Strength, Intellect,
  Agility, Will and Power are separate trained stats. The official statistics
  page includes legacy progression language, so do not derive today's maximum
  age, universal caps, or exact talent formulas from it. Record visible current
  values/caps. Snack selection must not promise a particular unmanifested talent.
  [Official statistics baseline](https://www.wizard101.com/w101playersguide/pet-games3).

The prior research ledger records historical school/category preference rules
and +1/+2 bonuses. Keep those as hypotheses until a selected pet's feedback and
before/after values establish the applicable reward. Event bonuses, stat caps
and preference bonuses must not be silently folded into “base XP.”

At an age transition, XP within the new age is a different coordinate from XP
within the old age. Preserve both snapshots and pause the snack proposal until
the new age, target, caps and manifested-talent checkpoint are reconciled. Do
not treat a lower displayed XP number as lost progress, infer overflow rules,
or spend another snack merely because the numeric counter reset. These are
proposed accounting requirements; age-rollover handling is not in the current
pure planner.

Source chronology matters: Ravenwood Academy displays February 1, 2025 but
still describes Spring 2020 Happiness as new and contains inconsistent Energy
examples. It is useful for topics to verify, not fixed current Energy costs.
The two Final Bastion articles from the existing research returned 403 during
this review; their previously recorded claims were not newly verified here.

## User choices and protection

| Setting | Proposed behavior |
| --- | --- |
| Don't use snacks | Default. Finish games without feeding; zero consumption budget. |
| Suggest snacks | Explain eligible choices and expected benefit; never submit a feed. |
| Use selected snacks | Explicit opt-in, restricted to verified allowed items and budgets. Does not authorize all inventory. |
| Goal | Dance practice; an age/XP checkpoint; selected trained-stat targets; or a separate Happiness target. Show unavailable goals until supported. |
| Allowed snacks | Explicit per-mode item list, supported by visual identity evidence. New or unknown items remain unavailable. |
| Protected stock | Per-item “never use” plus minimum remaining quantity. Protection wins over allowlists and optimization. |
| Budget | Maximum feeds per run, per-item limits and Energy reserve. Persist totals before offering daily limits; zero means none, not unlimited. |
| Stop conditions | Goal reached, cap reached, reserve/budget reached, changed pet, new talent needing review, unknown stock or unresolved consumption. |
| Completion | Finish result/snack screen, return to the world and request the native-Idle operation; report whether Idle was actually observed. |

For example, “use selected inexpensive snacks but keep ten” needs a specific
verified item, known quantity, minimum ten, a nonzero run budget and an unmet
goal. “Use what they are there for” can allow more items and a larger budget
without removing protected-stock rules. No snack, Energy refill, purchase,
hatching, item disposal or other resource spend is implied by selecting a goal.

## Implementation boundary and next evidence

`PetSnackPlanner` currently returns a one-snack suggestion or no action for
caller-supplied verified XP/Happiness data. It does not inspect inventory,
consume items, persist budgets, handle age rollover or optimize stat vectors.
Its “verified” fields are inputs, not proof produced by the planner. The
configuration design and new preferences must not be described as live feeding.

Before enabling a feed executor, qualify visual identification of the selected
pet, training-versus-Happiness context, snack identity, quantity and result.
Use a transaction keyed to the game/opportunity: observed → proposed → sent →
verified or uncertain. One observed inventory decrement and reconciled reward
must close a transaction before another feed is possible. A loading screen,
timeout or ambiguous count cannot justify resending the click. Unknowns should
stop consumption, while the lifecycle can still finish safely without a snack.

The first future feeding qualification should use one explicitly selected,
unprotected item, a one-feed budget and before/after evidence; this document
does not authorize or perform that test. Keep feeding disabled while arena and
UI-size qualification proceeds.
