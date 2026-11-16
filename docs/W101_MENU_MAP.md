# Wizard101 menu map

Status: initial inventory and shared contract, 2026-11-14. Not a complete or live-qualified menu navigator. Owner task W101-MENU-MAP-89; offline inventory GEM-MENU-INVENTORY-90.

## Purpose
One workspace menu atlas shared by cards, CLI, API and routines. Reuse W101ModuleObserver, W101MenuRecognition, W101ModuleBindings and the existing scheduler/input owner. Do not create another controller or equate the broad `menu` fact with a particular panel or clickable control.

## Each menu entry records
- Stable game-pack menu ID, title and parent/tab relationships; variants for scale, resolution and client build.
- Recognition evidence: UI-tree paths, visible text/anchors, screenshot hash, geometry coordinate space, timestamp and selected wizard when relevant. Missing evidence stays unknown.
- Controls: stable semantic ID, label, type (button/tab/toggle/slider/list), enabled/selected/value readings, bounds source and freshness.
- Entry and exit transitions: prerequisites, permitted action, expected new observation, timeout and recovery route. Escape is never assumed universally safe.
- Effects: read-only view, settings change, quest progression, character/logout, teleport, inventory/snack use or currency spend. Purchases and feeding remain outside current testing scope.
- Qualification: proposed, observed, replay-tested, live-verified; evidence binds exact revision and context. Qualification does not grant execution permission.

## Seed inventory
| Family | Existing evidence seam | Remaining work |
| --- | --- | --- |
| Options and native Idle | UI tree settings ancestors, nativeIdle; menu OCR and Idle cycle | Tab-specific identity/control evidence; live recovery and scale checks |
| NPC dialogue and services | Dialogue classifier and quest conversation reader | Separate spoken progression from vendor/services; explicit effects for each button |
| Shops and purchase prompts | shopGUI / PayToPlay coarse detection | Per-shop inventory, currencies, selection and cancellation; no purchase authorization |
| World Gate | Existing gate inspection/travel helpers | World selection, enabled travel and cooldown-linked outcomes |
| Character selection | Existing roster/selection helpers | Seat identity, selected wizard verification, unavailable Idle state |
| Pet Dance | Entry candidate tree, entry/postgame recognition | Arena selection, Play, watch/respond, reward and no-feed exit as one verified visit |
| Spellbook and character panels | Inventory pending | Tabs, stats, spells/decks, quests, maps, pets, housing and other available pages |

This is a work inventory, not a claim that every named family is navigable. The final row lists collection targets; exact screens and shortcuts must be established from evidence.

## Qualification sequence
Inventory existing code and saved fixtures first. Collect one panel at a time under a bounded authorized session, starting with Options/Idle. Verify identity before interacting, reobserve after each transition, and preserve before/after settings. Replay failure cases before live qualification. Never close quest dialogue as generic cleanup. Loading pauses observation-dependent transitions; focus loss releases inputs and waits at a checkpoint; a physical key while W101 is focused stops. End controlled tests in verified Idle when safe.

## User-facing result
A menu explorer within the W101 workspace: hierarchy on the left, selected panel and readable controls on the right, with evidence/qualification and configurable routine actions. This UI is planned, not implemented by this document. Reusable menu actions become configurable card building blocks through the existing catalog.

Source checkpoint94: World Gate finalization uses shared focus-wait/Idle recovery after travel input release.17 focused tests pass. This does not qualify every travel-body pause point or authorize replaying teleport inputs after focus loss; live verification remains pending.
