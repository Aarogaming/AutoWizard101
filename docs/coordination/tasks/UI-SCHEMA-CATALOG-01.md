# UI-SCHEMA-CATALOG-01 — Design Brief: Versioned UI Schema Catalog & Activity-Flag Architecture (REPORT-ONLY)

Status: **PROPOSED / DESIGN BRIEF — static architectural design brief. NO CODE EDITS, NO BUILDS, NO TEST RUNS, NO LIVE SESSIONS.**  
Date: 2026-11-16  
Auditor / Author: `gemini`  
Workstream: `W101-COMBAT`  
Directive: `LIA-0129`  
Active Claim: `UI-SCHEMA-CATALOG-01`  
Workspace: `F:\Modlists\AutoWizard101`  
Branch: `maelstrom/rust-architecture-v1`  
Committed Tree HEAD: `67d41da`  

---

## Executive Summary

This design brief establishes the formal architecture for a **Versioned UI Schema Catalog** and an **Activity-Flag Detection System** (specifically for Dueling Diego and related minigames), responding directly to directive `LIA-0129`. 

Ground-truth observations in [`docs/coordination/results/W101-UI-NAMING-01.md`](file:///f:/Modlists/AutoWizard101/docs/coordination/results/W101-UI-NAMING-01.md) (94 distinct nodes in Khrysalis overworld) and [`docs/coordination/results/W101-COMBAT-UI-NAMING-01.md`](file:///f:/Modlists/AutoWizard101/docs/coordination/results/W101-COMBAT-UI-NAMING-01.md) (136 distinct nodes in boss combat) confirmed that Wizard101's global UI tree retains dormant structural nodes (`PlanningPhase`, `DiegoMeter`, `DuelTimerWindow`, fishing, gardening, and housing controls) even when those activities are completely inactive. 

This brief defines:
1. The multi-context **Sampling Matrix** required to calibrate candidate node meanings across activities.
2. The versioned **Catalog Format** strictly categorizing visibility, hierarchy, and evidence provenance.
3. The fail-closed **Activity Flag Architecture** (`activity.diego.visible`) acting strictly as a non-input **trigger**.
4. Full integration with [`docs/coordination/tasks/W101-BACKGROUND-API-01.md`](file:///f:/Modlists/AutoWizard101/docs/coordination/tasks/W101-BACKGROUND-API-01.md) candidate facts.
5. A comprehensive **Balance Section** contrasting verified artifacts against proposed features.

---

## 1. Multi-Context Sampling Matrix

To construct a reliable, versioned UI schema catalog without jumping to false conclusions, the client UI tree must be systematically sampled across distinct game contexts. 

### Context Matrix

| # | Game Context | Target Environment | Key Expected Visible Nodes | Key Expected Dormant (Hidden) Nodes | Sampling Objective |
|---|---|---|---|---|---|
| 1 | **Overworld Exploration** | Commons, Kondha Desert | `Root`, `WorldView`, `windowHUD`, `CompassWindow`, `btnSpellbook`, `spriteHealthBkg` | `PlanningPhase`, `DiegoMeter`, `DuelTimerWindow`, `CloseFishingButton` | Baseline HUD and navigation controls; proves minigame nodes are dormant. |
| 2 | **Standard Card Combat** | Boss battle, street mob | `CombatantControl`, `Health`, `Pips`, `Hand`, `Card1`..`Card7`, `SpellSelection`, `Target` | `btnSpellbook`, `GoHomeButton`, `CompassWindow`, `DiegoMeter` | Differentiates card planning phase from exploration; validates combat reader vocabulary. |
| 3 | **Dueling Diego Active** | Pet Pavilion Diego Arena | `DiegoMeter`, `DuelTimerWindow`, `PlanningPhase` (if active), minigame controls | `Hand`, `CombatantControl`, `CompassWindow`, `QuestHelperHud` | **Critical calibration gate**: Proves Diego nodes become `visible: true` during actual gameplay. |
| 4 | **Other Minigames** | Pet Dance, Potion Motion, Cannons | Minigame-specific overlays, arrow grids, board cells | `DiegoMeter`, `DuelTimerWindow`, `Hand`, `spriteManaBkg` | Distinguishes Pet Dance / Potion Motion signatures from Diego signatures. |
| 5 | **Modal Menus & Shops** | Bazaar window, Spellbook, Dialogue | `BazaarWindow`, `TradeRequestWindow`, `DialogueWindow` | `WorldView`, combat hand containers | Establishes modal exclusivity and interaction overlays. |
| 6 | **Loading & Transition** | Black screen, world gate loading | `LoadingTransitionGate` (or empty tree) | All HUD controls, all minigames | Proves all candidate activity flags instantly clear during loading screens. |

### Build, Layout, & Owner Pins
Sampling must be bound to immutable target metadata:
- **Client Image SHA-256 Allowlist**: Strictly checked against approved hashes in [`ProjectMaelstrom/ProjectMaelstrom/Utilities/W101UiTreeOperation.cs:46–48`](file:///f:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/W101UiTreeOperation.cs#L46) (`d410a246...` or `544ab726...`).
- **Target Triple**: OS process handle, process ID (PID), and process start ticks must remain identical across the sampling burst.
- **Owner Binding**: Verified match against local user client endpoints (`ownerBinding == "registry-owner-endpoints-matched"`).
- **Layout Version**: Bound to layout reader identifier (e.g. `archived-wizwalker-client-zone`).

### Operational Out-of-Scope Declarations
- **NO Live Sessions by Autonomous Agents**: All sampling is observational only, initiated either manually by the human owner or automatically via an unauthorized read-only waiter when the game is foregrounded.
- **NO Input Dispatch**: Zero mouse clicks, keyboard presses, or memory writes.
- **NO Geometric Extrapolation**: Raw layout rectangles from the UI tree are structural attributes only; they are not client-pixel click targets.

---

## 2. Versioned UI Schema Catalog Format

The catalog is formally specified as a versioned, declarative JSON schema (`ui-schema-catalog.v1.json`).

### Cardinal Rule on Dormant Nodes
> **Hidden nodes (`visible: false`) are NEVER evidence of an active game activity.**  
> In Wizard101, minigame and combat windows are statically allocated in the root DOM at engine initialization. They remain dormant in memory regardless of what the wizard is doing. A node's candidate meaning remains strictly `tier: "candidate"` until an activity is observed with that node demonstrably `visible: true`.

### Catalog JSON Schema Definition

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "MaelstromW101UiSchemaCatalog",
  "type": "object",
  "required": ["schema_version", "catalog_id", "layout_pin", "nodes"],
  "properties": {
    "schema_version": { "type": "integer", "const": 1 },
    "catalog_id": { "type": "string" },
    "client_image_sha256": { "type": "string" },
    "layout_pin": { "type": "string" },
    "nodes": {
      "type": "object",
      "additionalProperties": {
        "type": "object",
        "required": [
          "parent_node",
          "observed_contexts",
          "visibility_by_context",
          "candidate_meaning",
          "evidence_sources",
          "qualification_tier"
        ],
        "properties": {
          "parent_node": { "type": ["string", "null"] },
          "related_nodes": { "type": "array", "items": { "type": "string" } },
          "observed_contexts": {
            "type": "array",
            "items": { "type": "string", "enum": ["overworld", "combat", "diego_active", "minigame_other", "menu_modal", "loading"] }
          },
          "visibility_by_context": {
            "type": "object",
            "properties": {
              "overworld": { "$ref": "#/$defs/node_state" },
              "combat": { "$ref": "#/$defs/node_state" },
              "diego_active": { "$ref": "#/$defs/node_state" }
            }
          },
          "candidate_meaning": { "type": "string" },
          "evidence_sources": {
            "type": "array",
            "items": {
              "type": "object",
              "required": ["receipt_id", "sample_count", "observed_visible"],
              "properties": {
                "receipt_id": { "type": "string" },
                "sample_count": { "type": "integer" },
                "observed_visible": { "type": "boolean" },
                "report_citation": { "type": "string" }
              }
            }
          },
          "qualification_tier": {
            "type": "string",
            "enum": ["candidate", "observed", "qualified"]
          }
        }
      }
    }
  },
  "$defs": {
    "node_state": {
      "type": "object",
      "required": ["visible"],
      "properties": {
        "visible": { "type": "boolean" },
        "enabled": { "type": ["boolean", "null"] }
      }
    }
  }
}
```

### Exemplar Catalog Entries Grounded in Committed Evidence

```json
{
  "DiegoMeter": {
    "parent_node": "Root",
    "related_nodes": ["DuelTimerWindow", "PlanningPhase"],
    "observed_contexts": ["overworld", "combat"],
    "visibility_by_context": {
      "overworld": { "visible": false, "enabled": null },
      "combat": { "visible": false, "enabled": null }
    },
    "candidate_meaning": "Score/performance meter during Dueling Diego active minigame",
    "evidence_sources": [
      {
        "receipt_id": "cc76a2990cc447f8978bb9266d461ccb",
        "sample_count": 25,
        "observed_visible": false,
        "report_citation": "docs/coordination/results/W101-UI-NAMING-01.md:30"
      },
      {
        "receipt_id": "a26b8b8ecc314bac8503ba981bb15df8",
        "sample_count": 25,
        "observed_visible": false,
        "report_citation": "docs/coordination/results/W101-COMBAT-UI-NAMING-01.md:27"
      }
    ],
    "qualification_tier": "candidate"
  },
  "SchoolPipPanel": {
    "parent_node": "CombatantControl",
    "related_nodes": ["Hand", "Card1", "Pips", "ShadowPips"],
    "observed_contexts": ["overworld", "combat"],
    "visibility_by_context": {
      "overworld": { "visible": false, "enabled": null },
      "combat": { "visible": true, "enabled": true }
    },
    "candidate_meaning": "Active planning phase panel displaying school power pips in card combat",
    "evidence_sources": [
      {
        "receipt_id": "a26b8b8ecc314bac8503ba981bb15df8",
        "sample_count": 9,
        "observed_visible": true,
        "report_citation": "docs/coordination/results/W101-COMBAT-UI-NAMING-01.md:26"
      }
    ],
    "qualification_tier": "observed"
  }
}
```

---

## 3. Dueling Diego Activity-Flag Architecture (`activity.diego.visible`)

To safely wake a specialized internal react system without polling continuously with expensive CV or taking premature control, we specify the composite flag: `activity.diego.visible`.

### Exact Activation Predicate

The candidate flag `activity.diego.visible` evaluates to `true` **if and only if** all of the following conditions are simultaneously met:

$$\begin{aligned}
\text{DiegoActive} \iff &(\text{NodeVisible}(\text{"DiegoMeter"}) == \text{true}) \land (\text{NodeVisible}(\text{"DuelTimerWindow"}) == \text{true}) \\
&\land (\text{ConsecutiveSamples} \ge 3 \text{ over } \Delta t \ge 600\text{ ms}) \\
&\land (\text{TargetBindingVerified} == \text{true}) \\
&\land (\text{ClientImageHashMatched} == \text{true}) \\
&\land (\text{Loading} == \text{false}) \land (\text{NativeIdle} == \text{false})
\end{aligned}$$

1. **Required Visible Node Set**:
   - `DiegoMeter.Visible == true`
   - `DuelTimerWindow.Visible == true`
   - (Note: `PlanningPhase.Visible` may be optional if Diego uses a distinct stage naming).
2. **Temporal Debounce (Consecutive Fresh Samples)**:
   - Must be observed with `visible: true` in **$\ge 3$ consecutive tree readings** across at least **$600\text{ ms}$**.
   - Prevents transient layout flashes during window initialization from firing the trigger.
3. **Session & Target Validation**:
   - Exact client HWND, PID, and start ticks match the verified target triple.
   - Client image SHA-256 matches the authorized allowlist.
4. **Negative Guard Invariants**:
   - `W101UiTreeResult.Loading` must be strictly `false`.
   - `States["nativeIdle"]` must not be asserted.
   - No modal dialogue window is present (`States["dialogue"] != true`).

### Immediate Fail-Closed Clearing Conditions

The flag `activity.diego.visible` must **immediately clear to `false`** (or `unknown`) upon any of the following events:
1. **Any Single Negative Reading**: Any tree sample where `DiegoMeter.Visible == false` or `DuelTimerWindow.Visible == false`.
2. **Focus Loss**: The game window loses foreground focus (`IsCurrent(target) == false`).
3. **Loading Screen**: `W101UiTreeResult.Loading == true` or loading transition gate detected.
4. **Target Invalidation**: Process termination, handle change, or target PID mismatch.
5. **Time-to-Live (TTL) Expiry**: If no fresh UI tree observation arrives within **$1500\text{ ms}$**, the flag automatically expires to `unknown`.

### Operating Role: Non-Input Trigger Only

```
┌────────────────────────────────────────────────────────┐
│ UI TREE SAMPLER (W101UiTreeOperation)                  │
│ Evaluates: DiegoMeter.Visible && DuelTimer.Visible    │
└──────────────────────────┬─────────────────────────────┘
                           │
                           ▼ (3 consecutive samples)
┌────────────────────────────────────────────────────────┐
│ CANDIDATE FLAG: activity.diego.visible = true          │
│ - Zero input dispatched                                │
│ - Zero desktop control acquired                        │
│ - Qualification: candidate                             │
└──────────────────────────┬─────────────────────────────┘
                           │
                           ▼ (Event Notification)
┌────────────────────────────────────────────────────────┐
│ DUELING DIEGO REACT SYSTEM (Internal State Machine)   │
│ - Wakes from dormant sleep                             │
│ - Prepares fast perceptual templates / scancode maps   │
│ - Awaits explicit scheduler lease before acting        │
└────────────────────────────────────────────────────────┘
```

The flag is strictly an **internal signal / trigger**. It never requests an input lease, never calls Win32 input APIs, never dispatches keystrokes, and never declares that the minigame has been played or won.

---

## 4. Integration with `W101-BACKGROUND-API-01`

The UI Schema Catalog directly integrates into the continuous background facts service specified in [`docs/coordination/tasks/W101-BACKGROUND-API-01.md`](file:///f:/Modlists/AutoWizard101/docs/coordination/tasks/W101-BACKGROUND-API-01.md):

### Published Fact Contracts
The background facts service publishes normalized facts conforming to `JobFactCatalog`:
- `ui.node.<normalized_name>.visible`: Boolean value indicating stable visibility.
- `ui.node.<normalized_name>.candidate`: Structural record containing parent, context, and observation timestamps.
- `activity.diego.visible`: Composite candidate flag as specified in Section 3.

### Fact Catalog Constraints
1. **Default Availability**: Per `W101-BACKGROUND-API-01` §3.3, all proposed `ui.node.*` and `activity.*` fact IDs default to:
   $$\text{ProducerAvailable} = \text{false}$$
   They cannot be marked available until a concrete, verified background reader is implemented and registered.
2. **Qualification Tiers**:
   - `unknown`: No valid target, window minimized, reader timed out, or TTL ($2.0\text{ s}$) expired.
   - `candidate`: Structural node name observed via archived layout reader (`status: "candidate"`).
   - `observed`: Node name observed with verified owner binding in an approved client build.
   - `qualified`: Verified correlation with ground-truth user gameplay in an active activity.
3. **No Promotion Without Evidence**: No candidate node fact may be promoted to `qualified` without an owner-authorized run demonstrating real gameplay correspondence.

---

## 5. Balance Section: Verified Artifacts vs Proposed Features

To maintain strict truthfulness and avoid repeating past defects, this section explicitly delineates verified evidence from proposed architectural design:

| Feature / Metric | Status in Committed Tree (HEAD `67d41da`) | Proposed in this Brief |
|---|---|---|
| **94 Overworld UI Node Names** | ✅ **VERIFIED**: Observed across 25 trees in Khrysalis overworld (`docs/coordination/results/W101-UI-NAMING-01.md:28–30`), receipt `cc76a2990cc447f8978bb9266d461ccb`. | Formatted into versioned catalog entries with parent/child relations. |
| **136 Combat UI Node Names** | ✅ **VERIFIED**: Observed across 25 trees during Khrysalis boss fight (`docs/coordination/results/W101-COMBAT-UI-NAMING-01.md:22–27`), receipt `a26b8b8ecc314bac8503ba981bb15df8`. | Formatted into combat planning phase entries (`SchoolPipPanel`, `Hand`, `Card1`..`Card7`). |
| **Dormant Diego Nodes** | ✅ **VERIFIED**: `PlanningPhase`, `DiegoMeter`, and `DuelTimerWindow` were `visible: false` in all 25 samples of both runs. Owner confirmed minigame was not running. | Specified that hidden nodes are NEVER activity evidence. |
| **2-Tree Snapshot Cap Diagnosis** | ✅ **VERIFIED**: `tools/w101_ui_tree.py:2019–2026` caps sampling at 2 trees when `includeResourceContext` is set. Dropping it allowed 25 trees in 25 s. | Sampling matrix specifies omitting resource context during extended tree discovery. |
| **`activity.diego.visible` Flag** | ⚠️ **PROPOSED ONLY**: Does not exist in any committed C# or Python file. | Specified as a 3-sample debounced, fail-closed non-input trigger. |
| **`ui-schema-catalog.v1.json`** | ⚠️ **PROPOSED ONLY**: Catalog schema designed here; no JSON file currently exists in tree. | Specified JSON schema and structure ready for artifact authoring. |
| **Background Facts Publication** | ⚠️ **PROPOSED ONLY**: `W101-BACKGROUND-API-01` is a design brief; no continuous background service is running. | Integrated candidate fact names defaulting to `ProducerAvailable = false`. |
| **Live Dueling Diego Observation** | ❌ **UNVERIFIED / UNATTEMPTED**: No Maelstrom observation pass has ever captured an active Dueling Diego minigame. | Outlined in Sampling Matrix as a future calibration gate requiring human play. |

---

## 6. Certification of Operating Boundaries

This design brief was authored under strict report-only operating boundaries:
- **0** source code files modified.
- **0** test runs or builds executed.
- **0** live game, desktop, or background sessions initiated.
- **0** modifications to `claims.json` or project board state.
- All numbers, receipts, node names, and file citations were extracted directly from committed primary source artifacts at HEAD (`67d41da`).
