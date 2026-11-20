# MINIGAME-ECOSYSTEM-DISPOSITION-01 — fishing/gardening/crafting/pet ecosystem

Status: **research disposition — no import, code, build, live or network work.**
Date: 2026-11-17 · owner: root · workstream: `CORE-REFERENCES`

## 1. Source discipline

The owner's list is unverified external context. No upstream repository was fetched, no anti-cheat/ban claim was tested, and no project capability is asserted as current. Only our own tree is cited as fact.

## 2. Explicit exclusion first

**Wizard101LuaCommandExecution (Gavpherk)** is **excluded**, not a lead. The owner's own quoted description says it decompiles `Root.wad`, modifies UI and executes custom Lua code **to the game server**, with stat/combat modification described as highly detectable and ban-inducing. That crosses the owner-approved boundary: live traffic interception, auth/session manipulation and server-side state changes are excluded; installed game files remain read-only. The external authors' ToS/ban warnings reinforce the boundary but are not the reason for it — we do not build server-side manipulation regardless of detection probability.

`wiz-packet-map` (AES-GCM decryptor via Crypto++ hook) and `wizmsg` (DML messaging) are likewise excluded/only archive references, as recorded in `POST-PATCH-DOCTRINE-01.md`.

## 3. Disposition of the stationery/minigame categories

| Category from owner | Our status | Disposition |
|---|---|---|
| CV fishing bots (bobber bite, reel hotkey) | no fishing bot; only a reagent icon asset | **Missing lead** — CV bite detection fits our allowed envelope (pixels + bounded key pulse); no existing implementation to import |
| WizWalker fishing UI/entity filters | pinned wizwalker adapter reads UI/entity lists generically | **Capability present, adapter absent** — fish-type filtering would be a pure policy over existing observations; no new memory reader needed |
| AHK pixel-scan fishing macros | no AHK scripts | **Missing lead** — our input owner would have to replace AHK's global hotkeys; not imported as-is |
| Gardening plow/plant/water/music/pest macros | `gardening-bot-main.zip` tracked as reference; native pet-snack/reagent planning exists | **Reference only** — legacy timed fixed-grid loops are candidates for wrapped pure decisions, not direct import; no verified plant-state qualification today |
| Deimos Auto-Craft / Auto-Potion | vendored Deimos source present | **Reference only** — timed button spam and teleport-to-wisp are not qualified operations; a bounded, observed crafting/potion action would need its own contract |
| Stationary Halfang/gold farms | `HalfangFarmingBot.cs` legacy native shell | **Legacy reference** — policy-gated, not a qualified integrated player; stationary loop fits the envelope only with ordinary outcome verification |
| Pet kiosk / auto-hatch clickers | no kiosk-specific code; pet training preferences/planner exist | **Missing lead** — exact inventory/energy policies and verified outcomes would be required; no Crown/energy purchase path |
| Wizard101_DanceBot | `petdance.zip` + pinned source + native teacher pipeline | **Reused** — arrow detection and sequence logic ported; feeding remains default-off and owner-authorized live only |
| MaroonCoffee wizbot / OCR Bazaar snipers | `tools/bazaar_tc_sniper.py` + BazaarTCBot archive | **Covered natively** — see `POST-PATCH-DOCTRINE-01.md`; purchases must be policy-gated transactions, not uncontrolled clicks |
| katsuba (vbe0201) format parser | no katsuba clone; independent BCD field-order parser in `tools/w101_bcd_evidence.py` (`W101-SHAPE-ORDER-139`) | **Covered independently** — our parser resolved the cylinder field-order discrepancy; no new import needed |
| wizwalker / wizSDK / Deimos / wizlaunch / hivemind | see `POST-PATCH-DOCTRINE-01.md` | **Already dispositioned** — wizlaunch/hivemind remain missing leads |

## 4. Doctrine

Stationary/minigame automation fits the owner-approved envelope when it uses our native owner, verified observations, bounded inputs and explicit policies for consequential effects (feeding, purchases). None of the listed external scripts is imported by this report. Fishing, gardening and crafting are **candidate future teacher domains**, to be handled wrap-first after named facts and fixtures exist — not new controllers or dependencies.

## 5. Next gates

No new live or import action is authorized. If the owner wants a fishing or gardening teacher, register a bounded brief that defines observed states, exact action contracts, negative bounds and saved fixtures; then wrap a proven legacy decision function before writing a native model.
