# Wizard101 pet snacks: research and subsystem plan

Research date: **2026-11-08, America/New_York**. This is a separate workstream
alongside Pet Dance, not an extension of the arrow reader. This change adds
research and a proposed contract only; it enables no feeding, purchasing or
inventory changes. The [structured ledger](../research/w101-pet-snacks-20261108.json)
is reference data, not an executable game pack or an automatic import.

## What the evidence supports

| Operation | Evidence | Consequence for Maelstrom |
| --- | --- | --- |
| Training snack after a pet game | The official guide offers a snack after a training game, increasing pet statistics and progression. Shops and crafting are documented sources. It reports neutral, liked and loved feedback, with Power gain for preferences. [Official feeding guide](https://www.wizard101.com/w101playersguide/pet-games6) | A verified end-of-game feeding opportunity is separate from a Dance round ending. Read the selected pet, item and result; do not feed once per arrow sequence. |
| Happiness top-up | A 2020 guide describes the Pet HUD's separate Happiness feeding control. Happiness fuels certain pet activities/talents; its historical cap uses trained stats rather than their maximum caps. Token unlocks are another resource. [Final Bastion](https://finalbastion.com/wizard101-guides/pet-happiness-talent-tokens/) | Give this its own optional routine and budget. Verify Happiness change; do not credit training XP merely because a snack was consumed. Current costs and conversions need client evidence. |
| Feeding ordinary inventory items | A publisher administrator's November 18, 2011 announcement describes consuming equipment/housing items through the backpack for possible rewards. [Publisher announcement](https://www.wizard101.com/forum/pets-mounts/pet-feeding-43209) | This is inventory disposal, not pet-snack training. Exclude it from this subsystem. A button labeled Feed is insufficient proof of the correct screen. |
| Playing or skipping a training game | Training games consume Energy. A community guide describes mastery-based skipping, also consuming Energy and allowing a snack without game XP. [Official Energy guide](https://www.wizard101.com/w101playersguide/pet-games7), [community training guide](https://ravenwoodacademy.com/wizard101-pet-training-guide/) | Energy, snack stock, training XP and Happiness are separate accounts. Skip is a distinct, opt-in strategy requiring observed eligibility; it must not replace Dance practice automatically. |

The pet's five primary stats are Strength, Intellect, Agility, Will and Power.
Keep their current values and caps distinct from age XP, manifested talents and
Happiness. The official stats page retains legacy progression details; use it
for terminology, not an exhaustive current talent formula.
[Official stats guide](https://www.wizard101.com/w101playersguide/pet-games3)

The 2020 preference guide describes one preferred food category and one preferred
school: one match gives a liked bonus of one XP and Power; both give a loved bonus
of two XP and Power. Treat those numbers as documented historical expectations,
not calibrated current-client rewards. A pet's displayed school does not by
itself establish its snack preference. A large base reward can matter more than
the preference bonus. [Preference guide](https://finalbastion.com/wizard101-guides/w101-pet-guides/beginners-guide-wizard101-pets/)

A publisher announcement establishes that a member Double Pet XP event affected
games and snacks in February 2015. It does not establish any active event today,
current eligibility or how modifiers stack. Snapshot active bonuses and measure
reward deltas. [Historical event](https://www.wizard101.com/forum/ravenwood-commons/double-pet-experience-for-members-8ad6a4184bc02a57014bc1744b2d05f8)

## Evidence quality and chronology

Official player-guide pages above have no established publication dates. Their
mechanics are useful baselines, but old screenshots and progression descriptions
cannot qualify today's Tiny UI controls. No snack screen or feeding outcome was
observed during this research.

The preference article was published April 22, 2020 and modified August 16,
2020. The Happiness article was published April 20 and modified May 7, 2020;
its introduction explicitly references Test Realm. Ravenwood Academy's community
training page has 2025 publication/update metadata but retains 2020 screenshots
and calls Happiness a new feature. Its Energy cost examples are internally
inconsistent. It is a fan site, distinct from the publisher's
`ravenwoodacademygame.com` domain.

The old official April 2020 update URL redirected to the current registration
landing page when retrieved. That response is not evidence of update contents.
Record source publication, feature effective date, region, platform, Live/Test
Realm and actual client build separately. An older omission can reflect feature
expansion. None of these sources independently establishes the active account's
region, build, pet inventory, mastery status or bonuses.

## Proposed user configuration

Within **Wizard101 → Pets → Snacks**, show independent choices for:

- **Training snacks:** after eligible games; goal of pet age/XP or chosen stat
  deficits. Optional mastery skipping has its own setting.
- **Happiness:** top up below a chosen threshold, with its own snack allowance.
- **Inventory planning:** stock, reserves and replenishment suggestions.

All begin disabled or preview-only. A user can play Dance without feeding,
feed after another supported pet game, or manage Happiness without Dance.
The UI should explain the selected pet, expected benefit, protected stock,
budget and stop condition before enabling a routine. A newly manifested talent
is a review checkpoint, never a reason to discard a pet automatically. Snacks
must not be advertised as selecting a desired unmanifested talent.

Selection first applies hard constraints: permitted items, protected items,
minimum remaining quantity, maximum feeds, Energy reserve and a supported
feeding context. Only then rank candidates by the selected objective:
estimated XP per training Energy, stat deficit reduction, least goal overshoot,
or sufficient Happiness with least protected stock spent. No universal
“best snack” ranking should override the user's reserves.

Protect rare or valued snacks by explicit item rules and minimum quantities,
not only rank. Keep unknown items unavailable until classified. Preserve snacks
reserved for other pets or crafting. Do not infer permission to consume from
a No Auction flag, a high stack count or a low apparent price.

## Data and routine boundaries

Proposed data, not existing public APIs:

| Record | Required information |
| --- | --- |
| Pet snapshot | Session-local pet identity, visible name/species, age and XP, trained stats/caps, current/max Happiness, observed preferences, evidence references. Names alone need not be unique. |
| Snack entry | Stable identity when verified; localized aliases/icon evidence otherwise; school, category, rank, base XP, stat vector, observed flags, quantity and user reserves. Unknown values remain unknown. |
| Reward observation | Context, game result identity, pet and snack identity, before/after quantities and resources, preference feedback, active bonuses, client/pack versions and capture times. |
| Feed proposal | Goal, eligible candidates, exclusion reasons, chosen quantity of one, expected benefit interval, required screen and validity limits. |
| Feed receipt | Confirmed, rejected or uncertain outcome; evidence of consumption and resource change; no fabricated reward from a successful click alone. |

Pet Dance emits a **verified whole-game result** with a unique result identity.
It releases held keys and its input lease before proposing a feeding task.
The scheduler then rechecks the pet, screen, item, inventory and budget using
fresh observations. Navigation, Auto-Idle and feeding cannot own input together.
User activity pauses work; resume invalidates old screen coordinates and
proposals. Desktop control remains brief, and tests return W101 to native Idle
between attempts.

Suggested future operation names are `w101.snacks.observe`,
`w101.snacks.plan`, `w101.snacks.feed-training`,
`w101.snacks.feed-happiness` and `w101.snacks.verify`. These names are not
registered by this change. A standalone Happiness request has its own identity;
it does not invent a Dance result.

Use an observed→planned→dispatched→verified/uncertain transaction. Persist the
request/result identity before dispatch so repeated completion events or a
restart cannot feed twice. Reconcile an uncertain dispatch from fresh inventory
and pet evidence; **never retry consumption automatically after an ambiguous
click**. A verified post-feed state is required before Play Again.

Tiny UI calibration belongs to the feeding panel itself, not the Dance arrow
scale. Anchor the panel, identify the selected item and quantity, and account
for scrolling, sorting, hover overlays, animation and changing DPI. OCR can
read small text; cached icons and stable regions should handle recurring
recognition. No model belongs in the live feeding decision loop by default.

## Stock and replenishment

The official feeding guide supports shop and recipe routes. Community guides
add gardening and boss drops, with Couch Potatoes and Evil Magma Peas as garden
research candidates. These are discovery leads, not verified holdings or
executable farming routes. [Official guide](https://www.wizard101.com/w101playersguide/pet-games6),
[gardening and acquisition overview](https://ravenwoodacademy.com/wizard101-pet-training-guide/)

A separate planner can report projected stock usage, gardens/recipes the user
actually has, ingredient shortages and suggested acquisition routines. Buying,
crafting, harvesting and combat farming each need their own qualified action
and resource budget. Crown purchases, Energy elixirs, talent-token spending and
automatic travel are outside the default snack routine. Do not encode historical
drop claims as guaranteed supplies or assume every snack is Bazaar-tradable.

## Delivery gates and project tracking

| Work item | State | Completion evidence |
| --- | --- | --- |
| SNACK-01: mechanics, sources and boundaries | Complete as research | This report and its structured ledger. No live qualification claimed. |
| SNACK-02: read-only inventory/context capture | Planned | Independent examples of training feed, Happiness feed and ordinary-item feed; correct pet, quantity, sort and Tiny UI identification. |
| SNACK-03: configurable planner | Planned | Dry-run decisions honor per-pet objectives, allowlists, reserves, budgets and unknown data. No input dispatch. |
| SNACK-04: scheduler and transaction integration | Planned | Shared input ownership; duplicate/result replay and restart tests; ambiguous outcome stops safely. |
| SNACK-05: opt-in single-item live trial | Not enabled | One explicitly allowed expendable snack, verified stock/resource deltas, then native Idle. |
| SNACK-06: independent routine and supply planning | Planned | Training and Happiness policies remain separate; stock forecasts use measured consumption. |

Offline qualification should cover wrong pet, wrong Feed screen, empty stock,
last-item reserve, duplicate names, OCR quantity error, stale frame, changed
sorting, stat caps, goal overshoot, unknown bonus, duplicate result event,
interrupted dispatch and user activity. Replay must distinguish game rewards
from subsequent snack rewards. Evaluate on recordings separate from calibration.

Open client checks include exact current preference bonuses, snack effects near
caps/age transitions, reward multipliers, mastery availability and cost, Energy
regeneration, Happiness conversion and costs, actual inventory identifiers and
layout. Nothing here authorizes spending snacks merely to resolve an unknown.

Related boundaries: [Pet Dance teacher](PET_DANCE_TEACHER.md),
[shared operations](SHARED_OPERATIONS.md) and
[Wizard101 operating research](W101_GAME_RESEARCH_20261108.md).

