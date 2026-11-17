# W101-BACKGROUND-API-01 — design brief: continuous background W101 facts service

Status: **DESIGN BRIEF — owner approval required before implementation.**
Date: 2026-11-16 · owner: root · workstream: `W101-COMBAT`

## 1. Product statement

A long-lived, headless part of Maelstrom attaches to a selected W101 client and continuously publishes **facts** — game stats, conditions, zones, position, combat structure — through Maelstrom's existing local API surfaces. **It has no foreground dependency.** Window focus is required only when Maelstrom *interacts* (input dispatch), never when it observes.

## 2. Why today's path can't do it (verified 2026-11-16)

- `W101UiTreeOperation.cs:138` and `:175` require `runtime.IsCurrent(target)` — the game must be foreground.
- `WindowObserver.cs:53,134,168–172` requires the window to be current and not minimized for capture.
- Consequently a background window yields no UI trees; the owner's 45 s all-context pass produced only 2 trees and `combat-readiness.json` = `Ready:false`, `Missing:["fresh-combat-observation"]`.

The Python wizwalker adapter (`tools/wizwalker_adapter.py`) does not itself require foreground — it verifies HWND/PID/creation-time and never activates the window. The **native gates and capture**, not the memory reads, are the blocker. That is the seam this service exploits.

## 3. Design

### 3.1 Lifecycle and ownership
- Hosted **inside the existing Maelstrom owner** (named-pipe `JobDeckHost` process), never as a second controller or a competing hook owner.
- Explicit `--background-facts` start with exact target identity (HWND/PID/start-ticks/image hash) and reviewed adapter opt-ins; explicit stop. "Runs all the time" means for the lifetime of that session, idle-paused when no valid target — **not** an unmanaged always-on process at boot.
- One shared input lease: the facts service **never acquires or dispatches game input**. Interaction remains a separate foreground-only path.

### 3.2 Read model (memory-only, no window capture)
Reuse pinned readers behind one bounded sampler:
- `tools/w101_ui_tree.py` — UI tree states, dialogue/prompt/menu/native-Idle candidates, combat control candidates (note: this installs a reviewed temporary root hook with restoration; cap rate and verify restoration).
- `tools/w101_pose_reader.py` — position/yaw, owner-bound samples.
- `tools/w101_zone_reader.py` — zone name candidate with layout id.
- `tools/w101_combat_reader.py` — structural card/participant/duel candidates.
- `tools/wizwalker_adapter.py` — client stats where the reviewed build allows.

No `WindowObserver`, no screenshots, no OCR. Visual evidence is explicitly **unavailable** in this mode.

### 3.3 Publication contract
Publish through the **existing** local API/MCP/CLI surfaces (no new network listener):
- A new bounded command `facts` on the native pipe: `{schemaVersion, target, generatedUtcTicks, facts[], qualification, note}`; plus an MCP read tool routed through the same owner.
- Each fact: `{id, value, type, unit, producer, observedUtcTicks, freshnessMs, qualification, layoutId/build}`.
- Fact IDs **must** come from `JobFactCatalog` where one exists (`health`, `mana`, `energy`, `gold.current`, `wizard.level`, `quest.objective`…). New IDs (e.g. `zone.candidate`, `combat.card.candidate`, `pips.candidate`) are proposed and default `ProducerAvailable=false` until qualified.
- Qualification tiers: `observed` (reader succeeded with owner binding), `candidate` (archived layout/structural only), `unknown` (expired, failed, or no valid target). Never silently promote candidate → qualified.
- TTL: identity/layout facts until target change; volatile stats/UI facts expire at a bounded age (2 s default, same as current live gates). Expiry yields `unknown`, never a stale value.

### 3.4 Target and failure semantics
- Target identity re-verified each cycle from process state (PID/ticks/image hash). On restart, replacement, hook restoration failure or sampler fault: publish `unknown` for all volatile facts, emit a reason, and require a fresh explicit attach — never auto-retarget.
- Recovery hold from any interaction path blocks publication of `observed` facts until verified restore.
- Sampling bounded: default 1–2 Hz per reader, hard cap (e.g. 4 Hz), per-read timeout, memory cap, cancellation within 2 s, no thread pile-up.

### 3.5 What it explicitly does NOT provide
- No visual states, screenshots or OCR-based qualification.
- No input capability, no auto-clicking, no dispatch.
- No combat decisions, no pip/castability claims (no pip fact exists today; this service would expose `pips.candidate` only, unqualified).
- No network service: only the current-user local pipe and existing MCP/CLI adapters.

## 4. Relation to the manual-capture controller

The background facts service is the **back-end half** of the front/back capture the owner wants. It can publish a timestamped fact stream into the same `SessionTimeline` session clock, so gameplay video/inputs and backend facts align for cross-comparison. It remains composition-only: both run under one owner, one session, no concurrent capture jobs.

## 5. Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Root-hook churn during background sampling | Cap rate, verify restoration each cycle, latch recovery on uncertainty |
| Stale data presented as current | TTL + `unknown` on expiry; qualification tier in every fact |
| Background memory reads destabilized by game updates | Build/layout pins with allowlist; unknown builds rejected |
| Resource creep ("runs all the time") | Explicit session lifecycle, idle when target invalid, hard sampling/memory caps |
| Ambiguous API promises | Default `ProducerAvailable=false` for new facts; no research claim becomes a permitted action |

## 6. Acceptance gates (for the future implementation claim)

1. Offline tests with a fake reader: identity change → all volatile facts `unknown`; TTL expiry; hook-restoration failure → recovery latch; no dispatch from the facts path ever.
2. Focus independence verified by construction: no `IsCurrent`/foreground check in the facts read path; a dedicated test asserts memory-only sampling proceeds with the window unfocused (simulated).
3. Local pipe/MCP `facts` read returns the same contract with bounded framing and current-user-only security.
4. Full native suite with `-warnaserror`, scoped format/analyzers, encoding/board checks.
5. Live qualification is a **separate** owner-authorized task; no live pass is part of implementation.

## 7. Boundaries

Client-side exception stays opt-in and reviewed; network manipulation excluded; installed game files read-only; no Crown/snack/feed/volume/restart actions; no new dependencies without approval; no second input owner. This brief changes no code, claims beyond its own output, or runtime behavior.
