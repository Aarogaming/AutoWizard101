# GEM-EXPLORATION-CONTRACT-86 — Exploration Recorder Contract & Route Integration Audit

**Task ID**: `GEM-EXPLORATION-CONTRACT-86`  
**Workstream**: `W101-NAVIGATION`  
**Author**: Gemini  
**Date**: 2026-11-14T11:50:00-05:00 (Revised per coordinator review `LIA-0040`)  
**Repository Revision**: `6dd15e5` (`maelstrom/rust-architecture-v1`; audited baseline `7888cf251388dd904bac3977c70f73d0a6a31424`)  
**Status**: READ-ONLY AUDIT & PROPOSED CONTRACT SPECIFICATION (UNRUN / PENDING INTEGRATION)  
**Owned Output**: `docs/coordination/results/GEM-EXPLORATION-CONTRACT-86.md`  

---

## 1. Executive Summary & Architecture Context

This report fulfills coordinator directive `LIA-0039` and incorporates the bounded review corrections from `LIA-0040` for task `GEM-EXPLORATION-CONTRACT-86`. It audits existing navigation, feedback, transition, and replay contracts in `ProjectMaelstrom` and identifies the smallest reusable seams and proposed data structures for a future **Exploration Recorder**.

### 1.1 Core Architectural Principles & Scope Boundaries
1. **Observed Traversal vs. Free-Space Inference**: Coordinates alone do **not** prove walkability, terrain slope, or obstacle clearance. Free-space inference (e.g. interpolating Euclidean paths across arbitrary coordinates or between unlinked zones) is invalid. Only physical traversal verified by continuous observation constitutes route evidence.
2. **Zone Discontinuity by Design**: Overworld movement is partitioned into **zone-local coordinate frames**. Cross-zone transitions are non-Euclidean discontinuities mediated by interactive portals, loading screens, and loading gates (`LoadingTransitionGate` / `W101NavigationTransition`).
3. **Recovery Latches vs. Ordinary Motion Failures**: A failed motion or low-progress condition halts the active waypoint pulse and reports a stopped state. Full recovery latching on the shared scheduler lease occurs when input or cleanup state is uncertain; ordinary blocked motion or waypoint divergence simply fails the attempt or requests replanning without claiming an unverified Idle state.
4. **Offline Prototyping vs. Execution Permission**: `CardPrototype` and `CardQualification` provide offline configuration, hashing, and qualification history. `CardQualificationStatus.GrantsExecutionPermission` is permanently `false`; card authoring does not grant automatic live execution or autonomous overworld navigation permission.
5. **Experimental Controller Limits vs. Universal Terrain Rules**: The thresholds in `WaypointFeedback` and `HeadingFeedback` are bounded heuristics for those specific experimental controllers, not universal laws of W101 terrain or hard physical recording limits.

---

## 2. Survey of Existing Reusable Contracts & Symbols

The codebase contains established, tested components that provide the mathematical and state-machine foundation for navigation feedback and transition gating:

| Component / Symbol | Source Location | Implemented Contract | Exploration Recorder Reuse & Limitations |
| :--- | :--- | :--- | :--- |
| `WaypointPose` | [WaypointFeedback.cs#L3](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/WaypointFeedback.cs#L3) | Immutable 5D spatial record: `(double X, double Y, double Z, double Heading, long Timestamp)`. | Reusable vertex data structure for recorded path points. |
| `WaypointFeedback` | [WaypointFeedback.cs#L9-L48](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/WaypointFeedback.cs#L9-L48) | Forward motion feedback: speed adaptation, low-progress stall detection ($\ge 2$ stalls), elevation change guard ($|\Delta Z| > 10$), large displacement guard ($> 120$). | Controller feedback during playback. Tolerances ($10$, $120$) are controller safety guards, not universal recording limits. |
| `HeadingFeedback` | [HeadingFeedback.cs#L9-L76](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/HeadingFeedback.cs#L9-L76) | Turn motion feedback: rate adaptation, translation during turn guard ($> 20$), large angular delta guard ($> 1.8\text{ rad}$), direction mismatch guard. | Controller feedback during orientation turns. Tolerances ($20$, $1.8\text{ rad}$) are controller safety guards, not universal recording limits. |
| `W101NavigationTransition` | [W101NavigationTransition.cs#L5-L65](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/W101NavigationTransition.cs#L5-L65) | `WalkAsync` (loading detection halting walk), `WaitAsync` (loading screen timeout and clear verification). | Reusable model for recording and gating cross-zone loading edges. |
| `LoadingTransitionGate` | [LoadingTransitionGate.cs](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/LoadingTransitionGate.cs) (35 lines) | State machine for loading onset, blackout detection, and clear confirmation. | Gating cross-zone transitions during recording and playback. |
| `w101_pose_reader.py` | [w101_pose_reader.py#L71](file:///F:/Modlists/AutoWizard101/tools/w101_pose_reader.py#L71) (`read_pose`) | Bounded memory reader: reads parent, marker `0x474 == 8`, pose $(X, Y, Z, \text{pitch}, \text{roll}, \text{yaw})$. | Bounded read-only pose observation producer (under approved W101 client-side exception). |
| `CardPrototype` / `CardQualification` | [CardPrototype.cs](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardPrototype.cs), [CardQualification.cs](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardQualification.cs) | Declarative offline card prototype and qualification ledger. Replay testing currently wired to `review.routine`. | Provides hashing and offline qualification evidence model. Does not provide automatic route promotion or execution permission. |

---

## 3. Proposed Exploration Recorder Contract (Draft Specification)

The following schema represents a **proposed draft** (`maelstrom.exploration-route.v1-draft`). Root has not adopted a canonical exploration schema, sampling cap, or promotion rule.

### 3.1 Proposed Zone-Local Path Representation
A proposed route consists of disjoint **Zone Paths**, each confined to a single, contiguous game coordinate space:

```json
{
  "schema": "maelstrom.exploration-route.v1-draft",
  "route_id": "exp-commons-to-pavilion-01",
  "created_utc": "2026-11-14T10:40:00Z",
  "environment": {
    "game_id": "wizard101",
    "world_id": "WizardCity",
    "zone_id": "WizardCity/WC_Hub",
    "wizard_subject": "Aron Deathstrider",
    "client_geometry": [1280, 720],
    "capture_geometry": [1282, 760]
  },
  "segments": [
    {
      "segment_id": "seg-01-commons-bridge",
      "zone_id": "WizardCity/WC_Hub",
      "coordinate_frame": "archived-minus-sin-minus-cos",
      "start_anchor": "Commons World Gate",
      "end_anchor": "Pet Pavilion Tunnel Portal",
      "vertices": [
        { "x": 0.0, "y": 0.0, "z": 0.0, "yaw": -1.5708, "time_ms": 1000.0, "speed": 0.6 },
        { "x": 12.5, "y": -45.0, "z": 0.0, "yaw": -1.5650, "time_ms": 1850.0, "speed": 0.62 }
      ],
      "metrics": {
        "total_distance": 2450.5,
        "duration_seconds": 32.4,
        "max_step_distance": 35.0,
        "elevation_delta": 4.2
      }
    }
  ]
}
```

- **Vertices**: Stream of observed poses ($X, Y, Z, \text{yaw}, \text{timestamp}$).
- **Proposed Sampling Cap**: A suggested maximum displacement threshold (e.g. $\le 40\text{ units}$ between recorded vertices) is proposed as a heuristic to avoid recording teleportations or sparse discontinuous hops; it is not an adopted engine constant.
- **Speed Bounds**: Heuristic linear speed checks ($0.2 \le v \le 1.2\text{ units/ms}$) flag anomalous coordinate jumps.

### 3.2 Proposed Cross-Zone Transitions & Loading Edges
When crossing an interactive portal, doorway, or teleport ring, coordinate continuity breaks. An explicit `CrossZoneEdge` structure is proposed:

```json
{
  "cross_zone_edges": [
    {
      "edge_id": "edge-commons-to-pavilion",
      "from_segment": "seg-01-commons-bridge",
      "to_segment": "seg-02-pavilion-courtyard",
      "from_zone": "WizardCity/WC_Hub",
      "to_zone": "WizardCity/WC_Streets/Interiors/WC_PET_Park",
      "portal_type": "walk_through_tunnel",
      "portal_entry_pose": { "x": 8426.38, "y": -2165.70, "z": -27.91, "yaw": -2.35 },
      "saw_loading": true,
      "loading_duration_ms": 3850.0,
      "portal_exit_pose": { "x": -1738.78, "y": -387.35, "z": 0.0, "yaw": -1.57 },
      "destination_verified": true
    }
  ]
}
```

- **Discontinuity Invariant**: Coordinates in `from_zone` are mathematically independent of `to_zone`. Replay must halt forward motion, enter `W101NavigationTransition.WaitAsync`, verify loading blackout/clear via `LoadingTransitionGate`, and acquire fresh pose in the destination zone before resuming.

### 3.3 Low Progress, Stalls & Controller Feedback Observations
During recording or playback, the recorder monitors motion feedback signals:

| Feedback Signal | Controller Detection Rule (from `WaypointFeedback` / `HeadingFeedback`) | Meaning & Limitation | Proposed Recorder Observation |
| :--- | :--- | :--- | :--- |
| **Forward Motion Stall** | $\Delta \text{distance} < 2\text{ units}$ over forward pulse ($\ge 2$ consecutive stalls). | **Evidence of low forward progress**, which may be caused by geometry, input lag, or player state; **not proof of a physical collision at an exact coordinate**. | Log `"low_progress_stall"`; record last observed pose. Do not label as verified physical obstacle point. |
| **Angular Turn Stall** | $|\Delta \text{yaw}| < 0.01\text{ rad}$ over turn pulse ($\ge 2$ consecutive stalls). | Evidence of insufficient angular progress during turn pulse. | Log `"turn_stalled"`; halt turn correction. |
| **Path Divergence** | Distance to next waypoint increases ($> \text{oldDistance} + 10$). | Steering error or unexpected drift relative to active waypoint. | Log `"path_divergence"`; interrupt segment. |
| **Unexpected Jump** | Step displacement $> 120\text{ units}$ or $|\Delta \text{yaw}| > 1.8\text{ rad}$. | Violates experimental controller safety guards; possible memory tear or teleport. | Discard vertex as `"controller_guard_violation"`. |

### 3.4 Target, Identity & Environment Bindings
A clear separation must be maintained between **reusable route identity** and **ephemeral runtime attempt binding**:
1. **Reusable Route Identity**:
   - Must **not** require the original PID, as PIDs change on every client launch.
   - Binds game (`wizard101`), world (`WizardCity`), zone (`WC_Hub`), and character identity (`wizard_subject`, e.g. `"Aron Deathstrider"`) or private space (`dorm_id`/`house_id`) where relevant.
2. **Ephemeral Runtime Attempt Binding**:
   - Live execution attempts bind `targetId` (including process start time / PID) to ensure observations and input leases are pinned to a specific live client instance.

### 3.5 Proposed Promotion Lifecycle (Draft Model)

The following 4-stage lifecycle represents a **proposed conceptual workflow**, not an adopted automated pipeline:

$$\text{Draft Recording} \xrightarrow{\text{Proposed Repeat Verification}} \text{Repeat-Verified} \xrightarrow{\text{Offline Replay Test}} \text{ReplayPassed} \xrightarrow{\text{User Card Authoring}} \text{CardPrototype}$$

1. **Stage 1: Draft Recording**:
   - Single recorded demonstration. Unqualified for automated playback.
2. **Stage 2: Repeat-Verified (Proposal)**:
   - Proposed heuristic: route traversed autonomously under supervision to demonstrate repeat reproducibility. (Root has not adopted a two-traversal rule; this is a proposed qualification criterion).
3. **Stage 3: ReplayPassed (Offline Reproduction)**:
   - Route and observations frozen into a qualification evidence package.
   - Deterministic offline replay succeeds against recorded fixture streams.
4. **Stage 4: CardPrototype Authoring**:
   - User configures card metadata in `CardPrototypeForm`.
   - Card remains declarative configuration with `CardQualificationStatus.GrantsExecutionPermission = false`.

---

## 4. Producer Inventory & Missing Production Seams

To implement an Exploration Recorder cleanly, several production telemetry and control gaps must be addressed:

| Required Capability | Existing Producer / Artifact | Current Status & Availability | Identified Architectural Gap |
| :--- | :--- | :--- | :--- |
| **Live 5D Pose Stream** | `W101PoseStreamConsumer.cs` / `w101_pose_reader.py#L71` | **Consumer Available; End-to-End Unwired**. Bounded native stream consumer integrated at HEAD (Task 132); end-to-end follower still has no production caller. | Wire verified stream consumer into an autonomous production navigation controller. |
| **Zone Identification** | `ZoneTopologyService.cs` / `W101ZoneObserver` | **Available; Unqualified in Navigation**. 55 WizardCity internal paths mapped in native dictionary; continuous zone fence integration into active movement loop remains open. | Active zone fence callback wired to cancel in-flight movement pulses on unpredicted zone boundary crossings. |
| **Loading State** | `W101NavigationTransition.WalkAsync` / `LoadingTransitionGate` | **Implemented** in `ProjectMaelstrom.Utilities`. | Event callback when loading blackout begins and clears. |
| **Passive Input Observation** | `GameInputLease.Current` | **Implemented** for active ownership. | Passive observation hook to record user-driven movement without seizing input ownership. |
| **Overworld Navigation Finalizer** | `PetDanceNativeIdle` (Dance-specific) / `W101IdleCycleOperation` | **Dance-specific or cycle-only**. | General overworld Idle routine (Escape-to-Idle failsafe verified for overworld navigation). |

---

## 5. Prioritized Offline Acceptance Matrix

The following five unit checks validate the proposed contracts and boundary rules:

| Case ID | Verification Area | Target Symbol & Contract | Preconditions & Input Route | Trigger / Condition | Expected Assertion / Behavior |
| :---: | :--- | :--- | :--- | :--- | :--- |
| **ER-01** | **Discontinuity Rejection** | `ExplorationRouteValidator.Validate` (proposed) | Draft route contains vertices from `WC_Hub` and `WC_PET_Park` in a single continuous segment without a transition edge. | Validation execution. | Rejects route; requires explicit `CrossZoneEdge` for zone boundary crossings. |
| **ER-02** | **Step Displacement Cap** | `ExplorationRouteValidator.Validate` (proposed) | Draft route segment contains step displacement $> 40\text{ units}$ (proposed heuristic cap). | Validation execution. | Rejects route segment as sparse / discontinuous. |
| **ER-03** | **Low Progress Stall Reporting** | `WaypointFeedback.Observe` ([WaypointFeedback.cs#L32-L34](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/WaypointFeedback.cs#L32-L34)) | Feed 2 consecutive poses with moved distance $< 2\text{ units}$ after forward pulse. | `_stalls >= 2`. | `decision.State == "stopped"`, `decision.Reason == "movement-stalled"`. Reports low-progress stop; does not assert physical collision or trigger full recovery latch unless cleanup is uncertain. |
| **ER-04** | **Identity Isolation Guard** | `ExplorationRouteValidator.Validate` (proposed) | Route authored for character `"Aron"` in `"WizardCity"`. | Attempt to validate route against mismatched character `"Belinda"`. | Rejects execution attempt due to wizard identity mismatch. Does not reject due to differing PID. |
| **ER-05** | **Prototype Execution Denial** | `CardPrototype.ReplayBinding` ([CardPrototype.cs#L55-L71](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardPrototype.cs#L55-L71)) | Inspect `CardQualificationStatus` on any prototype card referencing an exploration route. | Query `GrantsExecutionPermission`. | Returns `false`. Prototypes remain declarative offline configuration without execution authority. |

---

## 6. Traceability, Git State & Coordination Handback

- **Deliverable Path**: `docs/coordination/results/GEM-EXPLORATION-CONTRACT-86.md`
- **Claim Authority**: `docs/coordination/claims.json` (task `GEM-EXPLORATION-CONTRACT-86`, owner: `gemini`, state: `active`)
- **Git Commit Baseline**: `7888cf251388dd904bac3977c70f73d0a6a31424` (`maelstrom/rust-architecture-v1`)
- **Coordination Status**: Handed back via `GEM-0037` in `docs/coordination/gemini/FROM_GEMINI.md`.
- **Policy Compliance**:
  - Exclusively read-only static contract audit and specification.
  - Zero production code edits; zero test executions (`dotnet test` withheld for coordinator build slot).
  - Zero game process attachment, hook installation, or desktop input dispatches.
  - Distinguishes proposed draft structures from adopted standards; replaces unsupported success claims with concrete gaps.
