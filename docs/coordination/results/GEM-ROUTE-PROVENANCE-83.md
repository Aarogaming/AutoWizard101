# GEM-ROUTE-PROVENANCE-83 — Route Provenance Audit: Commons → Pet Pavilion → Dance Approach

**Task ID**: `GEM-ROUTE-PROVENANCE-83`  
**Workstream**: `W101-DANCE`  
**Author**: Gemini  
**Date**: 2026-11-14T10:11:00-05:00  
**Repository Revision**: `6dd15e5` (`maelstrom/rust-architecture-v1`; audited baseline `595ef2c7b8423995de21b055e3712eef195507c8`)  
**Status**: READ-ONLY AUDIT & ARTIFACT PROVENANCE REPORT (UNRUN / PENDING INTEGRATION)  
**Owned Output**: `docs/coordination/results/GEM-ROUTE-PROVENANCE-83.md`  

---

## 1. Executive Summary & Audit Scope

This report fulfills coordinator directive `LIA-0037` and sequential task `GEM-ROUTE-PROVENANCE-83`. It provides a rigorous, evidence-backed provenance audit of all existing route artifacts, coordinate definitions, landmark studies, and legacy waypoint scripts for navigating from **The Commons → Pet Pavilion → Pet Dance Sigil**.

### 1.1 Core Audit Findings
1. **Unmeasured Synthetic Sketches**: Coordinates defined in `tools/zone_routes.py` and packaged under `ProjectMaelstrom/ProjectMaelstrom/Resources/Macros/commons-to-pet-pavilion.macro.json` are **unmeasured, synthetic examples**. They have no real-world game provenance, use an arbitrary local scale, and erroneously span across a zone boundary loading screen as a continuous Euclidean segment. As repaired in `W101-ROUTE-PROVENANCE-06`, these files are explicitly quarantined under schema `maelstrom.route-preview.v1` with `executable: false` and supply zero executable macro nodes.
2. **Legacy Injection Teleportation vs. Walkable Routes**: The legacy implementation in `Scripts/Library/Deimos-Wizard101/src/auto_pet.py` does **not** walk from Commons to the Pet Pavilion. It uses `navmap_tp(cl, pavilion_XYZ)`, which performs client-side memory teleportation directly to the portal door (`XYZ(8426.38, -2165.70, -27.91)`), waits for zone change to `WizardCity/WC_Hub` (`auto_pet.py:21`), and then issues internal `client.goto()` memory walk commands to reach the Dance sigil. These coordinates are native client units but depend on unapproved memory injection and cannot be used for UI-driven keyboard navigation.
3. **Authoritative Human Demonstration Evidence**: The sole verified source of physical navigation is the passive recording documented in `docs/W101_DEMONSTRATION_20261108.md` (`.local/w101-route-study/human-route-01/video/capture.mp4`, SHA-256 `07B52BE94A5C1471E817F84B09C37AFF2C15958460950404119880FC265321EF`). This footage provides visual landmarks and user key timings (58 key holds in `input-review.json`), but **no XYZ ground-truth coordinates**. Furthermore, input-to-decoded-frame alignment remains unverified.
4. **Independent Zone Coordinate Frames**: The Commons (`WizardCity/WC_Hub`) and the Pet Pavilion interior (`WizardCity/WC_Streets/Interiors/WC_PET_Park`) are entirely separate coordinate systems separated by an interactive tunnel portal and loading screen. No single continuous 3D coordinate path connects them.

---

## 2. Primary Waypoint & Transition Provenance Table

Below is the comprehensive audit of every documented waypoint, candidate position, visual landmark, and transition across all repository artifacts for the Commons → Pet Pavilion → Dance approach route.

| ID | Segment / Waypoint Name | Artifact Path & Symbol / Field | Raw Coordinates / Values | Units & Heading Frame | Zone / World Identifier | Start / Dest Anchor | Travel Direction | Evidence Origin & Provenance | Timestamp / Revision | Confidence & Reuse Limitations |
| :---: | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **W-01** | Commons World Gate Center | `Resources/Macros/commons-to-pet-pavilion.macro.json` (`example_waypoints[0]`) | `(0.0, 0.0, 0.0)` | Arbitrary relative units; heading null | `The Commons` (unqualified) | World Gate spawn | Outward | Synthetic sketch in `tools/zone_routes.py`. | 2026-11-11 (`W101-ROUTE-PROVENANCE-06`) | **Zero Navigability**. Unmeasured placeholder; quarantined; `executable: false`. |
| **W-02** | Pet Pavilion Bridge Approach | `Resources/Macros/commons-to-pet-pavilion.macro.json` (`example_waypoints[1]`) | `(145.0, -420.0, 0.0)` | Arbitrary relative units; heading null | `The Commons` (unqualified) | Gate $\to$ Bridge | South-East | Synthetic sketch in `tools/zone_routes.py`. | 2026-11-11 | **Zero Navigability**. Unmeasured placeholder; does not reflect game terrain geometry. |
| **W-03** | Pet Pavilion Canal Bridge | `Resources/Macros/commons-to-pet-pavilion.macro.json` (`example_waypoints[2]`) | `(285.0, -840.0, 10.0)` | Arbitrary relative units; heading null | `The Commons` (unqualified) | Bridge crossing | South-East | Synthetic sketch in `tools/zone_routes.py`. | 2026-11-11 | **Zero Navigability**. Unmeasured placeholder. |
| **W-04** | Pavilion Tunnel Gate Archway | `Resources/Macros/commons-to-pet-pavilion.macro.json` (`example_waypoints[3]`) | `(390.0, -1260.0, 15.0)` | Arbitrary relative units; heading null | `The Commons` (unqualified) | Bridge $\to$ Tunnel | South-East | Synthetic sketch in `tools/zone_routes.py`. | 2026-11-11 | **Zero Navigability**. Treats zone boundary portal as continuous geometry; invalid. |
| **W-05** | Pavilion Inner Courtyard | `Resources/Macros/commons-to-pet-pavilion.macro.json` (`example_waypoints[4]`) | `(430.0, -1620.0, 15.0)` | Arbitrary relative units; heading null | `Pet Pavilion` (unqualified) | Tunnel $\to$ Courtyard | South | Synthetic sketch in `tools/zone_routes.py`. | 2026-11-11 | **Zero Navigability**. Unmeasured placeholder; ignores interior coordinate frame. |
| **W-06** | Pet Dance Game Sigil | `Resources/Macros/commons-to-pet-pavilion.macro.json` (`example_waypoints[5]`) | `(515.0, -1960.0, 20.0)` | Arbitrary relative units; heading null | `Pet Pavilion` (unqualified) | Courtyard $\to$ Sigil | South-East | Synthetic sketch in `tools/zone_routes.py`. | 2026-11-11 | **Zero Navigability**. Unmeasured placeholder; quarantined macro. |
| **W-07** | Pavilion Portal Door (Teleport Target) | `Scripts/Library/Deimos-Wizard101/src/auto_pet.py` (`pavilion_XYZ`) | `(8426.38, -2165.70, -27.91)` | Native game units (float 3D); heading unknown | `WizardCity/WC_Hub` | Commons world spawn | Tunnel portal | Deimos script memory teleport target (`navmap_tp`). | Upstream commit `3e38714` | **High Coordinate Accuracy / Unsuitable Dispatch**. Native units, but reached via memory injection teleport, not walking. |
| **W-08** | Milo Barker Approach | `Scripts/Library/Deimos-Wizard101/src/auto_pet.py` (`navigate_to_dance_game`) | `x=-1738.78, y=-387.35` (Z unknown) | Native game units (float 2D); heading unknown | `WizardCity/WC_Streets/Interiors/WC_PET_Park` | Pavilion entrance spawn | North-West | Deimos `cl.goto()` navigation script. | Upstream commit `3e38714` | **Moderate Coordinate Accuracy / Missing Adapter**. Valid interior coordinate, but relies on internal navmesh goto hook. |
| **W-09** | Pet Game Kiosk Front | `Scripts/Library/Deimos-Wizard101/src/auto_pet.py` (`navigate_to_dance_game`) | `x=-4090.11, y=-1186.37` (Z unknown) | Native game units (float 2D); heading unknown | `WizardCity/WC_Streets/Interiors/WC_PET_Park` | Milo Barker | West | Deimos `cl.goto()` navigation script. | Upstream commit `3e38714` | **Moderate Coordinate Accuracy / Missing Adapter**. Valid waypoint, lacks elevation and orientation. |
| **W-10** | Pet Dance Game Sigil | `Scripts/Library/Deimos-Wizard101/src/auto_pet.py` (`navigate_to_dance_game`) | `x=-4449.37, y=-993.00` | Native game units (float 2D); heading unknown | `WizardCity/WC_Streets/Interiors/WC_PET_Park` | Game front | West | Deimos `cl.goto()` navigation script. | Upstream commit `3e38714` | **Accurate Sigil Position**. Represents physical center of Dance sigil circle. |
| **W-11** | Pet Dance Sigil (Teleport Anchor) | `Scripts/Library/Deimos-Wizard101/src/auto_pet.py` (`auto_pet` line 305) | `(-4450.58, -994.90, -8.04)` | Native game units (float 3D); heading unknown | `WizardCity/WC_Streets/Interiors/WC_PET_Park` | Internal Pavilion | Exact sigil center | Deimos `client.teleport(XYZ(...))` | Upstream commit `3e38714` | **Accurate 3D Position / Unsuitable Dispatch**. Confirms sigil Z elevation is $\approx -8.04$. Direct teleportation. |
| **W-12** | Pet Dance Sigil Exit Step | `Scripts/Library/Deimos-Wizard101/src/auto_pet.py` (`auto_pet` line 344) | `x=-3830.43, y=-1301.03` | Native game units (float 2D); heading unknown | `WizardCity/WC_Streets/Interiors/WC_PET_Park` | Dance sigil | East (away from tent) | Deimos commented-out `# await client.goto(...)` (line 344). | Upstream commit `3e38714` | **Unexecuted Departure Waypoint**. Commented-out dead code; never executed in upstream script. |
| **W-13** | Commons Port Anchor | `docs/W101_DEMONSTRATION_20261108.md` & `tools/route_landmarks.py` | Visual appearance; SIFT correspondence (21 inliers) | Image-space correspondence; no world XYZ | `WizardCity/WC_Hub` | World Gate spawn | Facing bridge | Human demonstration recording (0.00–5.00s); CLI visits `150a1aa9...`, `388ce9a9...`. | 2026-11-08 (`human-route-01`) | **High Visual Confidence / Zero World Pose**. Visually confirmed anchor; requires visual alignment check before movement. |
| **W-14** | Commons Canal Bridge | `docs/W101_DEMONSTRATION_20261108.md` & `route-landmarks-13/` | Visual appearance; ORB/SIFT landmark (`bridge`) | Image-space inlier hull; no world XYZ | `WizardCity/WC_Hub` | Anchor $\to$ Bridge | Forward (W hold with A/D corrections) | Human demonstration recording (approx 15–25s). | 2026-11-08 (`human-route-01`) | **Visual Landmark Only**. Provides progress checkpoint; does not supply steering yaw. |
| **W-15** | Pet Pavilion Signed Tunnel | `docs/W101_DEMONSTRATION_20261108.md` & `route-landmarks-13/` | Visual appearance; tunnel sign reading "PET PAVILION" | Image-space bounding region | `WizardCity/WC_Hub` | Bridge $\to$ Tunnel | Forward | Human demonstration recording (approx 25–35s). | 2026-11-08 (`human-route-01`) | **Visual Landmark Only**. Distinct from Shopping District tunnel (critical collision guard). |
| **W-16** | Zone Loading Transition | `docs/W101_DEMONSTRATION_20261108.md` & `Modules/W101Menus/loading.png` | Loading artwork; 0 key holds; loading screen | State transition; no spatial coordinates | `WC_Hub` $\to$ `WC_PET_Park` | Tunnel portal | Inward | Human demonstration recording (approx 35–45s). | 2026-11-08 | **Mandatory Operational Pause**. Disables movement pulses; awaits zone arrival before resuming. |
| **W-17** | Pavilion Courtyard Approach | `docs/W101_DEMONSTRATION_20261108.md` | Visual appearance of Pavilion grounds | Image-space landmark | `WizardCity/WC_PET_Park` | Interior spawn $\to$ Dance tent | Forward-left | Human demonstration recording (approx 45–55s). | 2026-11-08 | **Visual Landmark Only**. Bounded forward walking toward dance tent. |
| **W-18** | Pet Dance Sigil Ring | `docs/W101_DEMONSTRATION_20261108.md` & `PetDance/Entry/entry-ocr.json` | Dance sigil title + interaction prompt | Screen-space prompt bounds ($y \in [0.6, 0.9]$) | `WizardCity/WC_PET_Park` | Tent approach | Stop on sigil ring | Human demonstration recording (approx 58s). | 2026-11-08 | **Verified Arrival Landmark**. Prompts `'X'` dispatch to open instruction panel. |
| **W-19** | Qualitative Route Description | `ProjectMaelstrom/ProjectMaelstrom/Modules/NavigationKnowledge.cs` | Qualitative text steps: `"Face straight ahead and angle slightly left"`, etc. | Informal human language; no numbers | Unqualified (contains stale `"Riverwood"` label) | Commons spawn $\to$ Pavilion | Qualitative | Static developer notes (`NavigationKnowledge.Routes["CommonsToPetPavilion"]`). | Unrevised legacy module | **Informal Planning Reference Only**. Non-executable; contains fictional/stale labels. |

---

## 3. Critical Route Gaps, Spatial Discontinuities & Failure Modes

### 3.1 The Zone Discontinuity Boundary
The transition between The Commons and the Pet Pavilion is **not a contiguous spatial path**:
- `The Commons` is zone `WizardCity/WC_Hub`. Its tunnel portal is located near $(X=8426.38, Y=-2165.70, Z=-27.91)$.
- Walking through the portal triggers an engine zone transfer, displaying a loading screen (`loading.png`) that unloads the Commons world model and loads `WizardCity/WC_Streets/Interiors/WC_PET_Park`.
- Inside the Pavilion, the character spawns at the interior tunnel entrance, where coordinates are centered around Milo Barker $(X \approx -1738, Y \approx -387)$ and the Dance Sigil $(X=-4449.37, Y=-993.00, Z=-8.04)$.
- **Failure Mode**: Any route runner that treats Commons $\to$ Pavilion as a continuous linear path will experience complete navigation collapse at the tunnel boundary. The route runner must split navigation into two isolated stages mediated by an explicit `LoadingTransition` wait state.

### 3.2 Starting View Alignment & The Shopping District Tunnel Trap
- In human demonstration `human-route-01`, the wizard begins at the Commons world port spawn.
- However, the recording contains **no `End` key or home port command**. A newly spawned wizard may face in varying directions depending on prior movement or zone entry.
- Directly to the right of the Commons port spawn is the arched entrance to the **Shopping District** (`docs/W101_ROUTE_DEMONSTRATION.md#L64`).
- **Failure Mode**: If a macro or forward-pulse runner begins walking forward without verifying start orientation, it will enter the Shopping District tunnel instead of the Pet Pavilion bridge. Visual consensus via SIFT/ORB (`tools/route_landmarks.py`) or a calibrated turn-to-anchor protocol is strictly mandatory before issuing forward pulses.

### 3.3 Collision, Drift & Lack of Spatial Feedback
- The physical walk across the Commons canal bridge features narrow bridge railings and stone archways.
- In `input-review.json`, the human demonstrator did not hold `'W'` continuously; they issued 58 separate key holds with frequent short `'A'` and `'D'` pulses to correct drift.
- **Failure Mode**: Blind timed replay of a forward key hold (e.g. 7 seconds of `'W'`) will accumulate slight directional drift, colliding with bridge walls or falling into canal geometry with no way to recover. *(Historical Note at Audit Time)*: UI telemetry did not expose live pose $(X, Y, Z, \text{yaw})$. While a bounded pose stream consumer (`W101PoseStreamConsumer`) has since been integrated at HEAD (Task 132), end-to-end route following still lacks production qualification. Unless pose reading (`tools/w101_pose_reader.py:71`) or visual landmark tracking (`route_landmarks.py`) provides closed-loop corrections, blind forward dispatch will fail.

### 3.4 Interaction Prompt Identity & Sigil Arrival
- Arriving inside the Pet Pavilion tent, the character approaches the Dance sigil.
- Adjacent to the sigil are other interactive entities (Milo Barker NPC, snack vendor kiosks).
- **Failure Mode**: Pressing `'X'` prematurely or off-target may open dialogue with Milo Barker or open the pet snack shop rather than the Pet Dance game. The runner must verify the Dance game title bounds (`VerifiedSigilTitleBounds` in lower third of screen) before dispatching `'X'`.

### 3.5 Completion & Native Idle Gating
- At the conclusion of a route demonstration or abort condition, the player must be left in a safe, verified state.
- In `human-route-01`, the user opened the game menu (`Escape`) and navigated to the native Idle options tab.
- **Safety Invariant**: A route run must never stop or release control while the wizard is in a vulnerable overworld state. If navigation is interrupted or completed, the controller must finalise via `PetDanceNativeIdle` or return to verified native Idle.

---

## 4. Prioritized Offline Acceptance Matrix for Route Navigation

Before any live route traversal or motion dispatch is authorized, the following five focused offline tests must be executed against current motion and navigation contracts.

| Check ID | Verification Area | Target Symbol & Contract | Preconditions & Fixture Inputs | Simulated Anomaly / Condition | Expected Behavioral Assertion |
| :---: | :--- | :--- | :--- | :--- | :--- |
| **RC-01** | **Level-Change Rejection** | `WaypointFeedback.Observe` ([WaypointFeedback.cs](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/WaypointFeedback.cs#L22)) | Current pose $(0, 0, 0)$, destination $(100, 0, 0)$, maximum pulse $80\text{ ms}$. | Input pose has $Z = 15.0$ ($|\Delta Z| = 15 > 10\text{ threshold}$). | Decision state is `"stopped"`, reason `"level-changed"`, pulse $0\text{ ms}$. Prevents moving between invalid vertical planes. |
| **RC-02** | **Heading Error Threshold** | `WaypointFeedback.Observe` ([WaypointFeedback.cs](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/WaypointFeedback.cs#L42)) | Current pose $(0, 0, 0)$, destination $(100, 0, 0)$, pose heading $\pi$. | Heading error is $\approx \pi\text{ rad}$ ($> 0.15\text{ rad threshold}$). | Decision state is `"stopped"`, reason `"heading-correction-needs-calibration"`. Suppresses forward pulses when misaligned. |
| **RC-03** | **Unmeasured Coordinate Lock** | `ZoneRoute.to_macro_nodes` ([tools/zone_routes.py](file:///F:/Modlists/AutoWizard101/tools/zone_routes.py#L35)) | `COMMONS_TO_PET_PAVILION` sketch from `zone_routes.py`. | Calling `to_macro_nodes()` or `to_macro_graph()`. | Raises `ValueError("Unqualified example coordinates cannot become navigation actions...")`. Fails closed; exports zero nodes. |
| **RC-04** | **Memory Read Consistency** | `w101_pose_reader.read_pose` ([tools/w101_pose_reader.py:71](file:///F:/Modlists/AutoWizard101/tools/w101_pose_reader.py#L71)) | Simulated memory transport with 72-byte buffer. | Buffer has invalid actor marker (`marker != 8`) or pose changed between reads. | Returns `status: "unknown"`, `reason: "actor-marker-mismatch"` or `"pose-changed-during-read"`. Rejects torn reads without throwing. |
| **RC-05** | **Landmark Area Consensus** | `route_landmarks.geometry` ([tools/route_landmarks.py](file:///F:/Modlists/AutoWizard101/tools/route_landmarks.py#L20)) | Inlier matches between live frame and Commons anchor reference. | Inlier convex hull area is $1.13\%$ ($< 2.5\%\text{ area threshold}$). | Returns `alignmentCandidate: False`, `referenceCenterShiftX: None`. Suppresses steering cues from localized text/UI matches. |

---

## 5. Corrections to Prior Legacy Map & Provenance Assertions

Without editing archived reports (`GEM-LEGACY-MAP-73` and `GEM-DANCE-AUDIT-71`), the following critical corrections are established for current and future work:

1. **Clarification on Deimos-Wizard101 Navigation**:
   - `GEM-LEGACY-MAP-73` classified Deimos navigation routines as reference material. This audit refines that finding: Deimos's `auto_pet.py` **does not navigate the overworld Commons route**. It performs an instant client memory teleport (`navmap_tp`) directly to the Pavilion entrance door, followed by internal memory walk commands (`cl.goto`). It contains zero walkable overworld waypoints between the Commons spawn and the Pavilion tunnel.
2. **Quarantine Status of Packaged Macro Files**:
   - Packaged files in `ProjectMaelstrom/ProjectMaelstrom/Resources/Macros/*.macro.json` retain their legacy `.macro.json` extensions solely to prevent breaking existing build manifests, but they are **not executable macros**. They contain no `nodes` array and supply zero actions to `macro_from_dict` or `MacroExecutor`. Any claim that these represent operational navigation routines is obsolete.
3. **Qualitative Knowledge vs. Ground Truth**:
   - `NavigationKnowledge.cs` contains stale descriptions originating from outdated templates (e.g. referencing `"Riverwood apartments"` for the character Dorm). These strings must be treated as informal developer sketches and never as an authoritative spatial ontology or routing database.

---

## 6. Traceability, Git State & Coordination Summary

- **Target Deliverable**: `docs/coordination/results/GEM-ROUTE-PROVENANCE-83.md`
- **Claim Authority**: `docs/coordination/claims.json` (task `GEM-ROUTE-PROVENANCE-83`, owner: `gemini`, state: `released` per `LIA-0108`)
- **Git Commit Baseline**: `6dd15e5` (`maelstrom/rust-architecture-v1`; historical baseline `595ef2c`)
- **Coordination Status**: Handed back via `GEM-0031` and accepted as historical artifact provenance per `LIA-0108`.
- **Operating Boundaries Upheld**:
  - Strictly read-only audit of existing source, indexes, and documentation.
  - Zero C# or Python production code modifications.
  - Zero build or test executions (`dotnet test` / `pytest` withheld for root build slot).
  - Zero live game interactions, hook installations, or desktop input dispatches.
