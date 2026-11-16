# GEM-MAP-METADATA-107: W101 Map, Zone, and Entity Metadata Audit (Revised v6)

**Task ID**: `GEM-MAP-METADATA-107`  
**Claim Owner**: `gemini` (Active in `docs/coordination/claims.json`)  
**Status**: Read-only static review and architectural gap audit; offline analysis only.  
**Deliverable Path**: `docs/coordination/results/GEM-MAP-METADATA-107.md`  
**Revision Context**: Revised per `LIA-0069` to correct observer cleanup/restoration framing (awaiting cleanup does not prove hook restoration; worker cleanup evidence and outer process deadlines are mandatory), define continuous sample context fences (target lifetime, actor identity, loading/zone fences), specify a formal clock-conversion uncertainty envelope, and contrast minimum arrival evidence for open portals versus named prompts with concrete counterexamples.

---

## 1. Executive Summary & Boundaries

Under directives `LIA-0058` through `LIA-0069`, this report reviews repository assets, imported libraries (`wizwalker`, `Deimos`, `Automatus-v2`), candidate graph bindings (`.local/nav-binding-108/` and `.local/nav-binding-109/`), measured live movement evidence (`02-pulse/read-23/ui-result.json`), and native scheduler contracts (`ZoneTopologyService`, `NavigationAtlas`, `W101PathSession`, `W101MovingProbe`) for destination-name navigation planning.

### Strict Boundary Declarations
- **Zero Production Edits**: No changes to production source files in `ProjectMaelstrom/` or `tools/`. Root and internal implementation agents own code/build lanes.
- **Zero Builds or Test Runs**: No execution of native build slots or test runners; offline review only.
- **Zero Live Interactions / Process Injection**: No memory reads, hooks, client control, or network manipulation.
- **Zero Extraction of Installed Files**: No extraction or tampering with installed client game data.
- **Status Distinction**: Measured pulse evidence qualifies candidate telemetry feasibility, **not** persistent moving-feed qualification or a completed autonomous route. All pulse durations, settle times, and footprint geometries are **hypothetical development estimates**.

---

## 2. Inventory of Exact Metadata Sources

### 2.1 Current Zone Identification
- **Inspected Legacy Source (`wizwalker`)**:
  - `Client.zone_name()` (`wizwalker/client.py:140–146`): Awaits `ClientZone.zone_name()`.
  - `ClientZone.zone_name()` (`wizwalker/memory/memory_objects/client_zone.py:15–20`): Reads a null-terminated string at offset `88` from the `ClientZone` base pointer in the inspected library copy.
- **Native Implementation (`ProjectMaelstrom`)**:
  - `ZoneTopologyService.cs`: Implements a static dictionary mapping 55 WizardCity internal paths to semantic zone IDs (e.g., `"WizardCity/WC_Hub"` $\leftrightarrow$ `"COMMONS"`, `"WizardCity/WC_Streets/Interiors/WC_PET_Park"` $\leftrightarrow$ `"PET_PAVILION"`).
- **Crucial Boundary & Live Evidence**:
  - Root verified a live Dorm $\rightarrow$ Commons transition in operation `34b60affdf5843c19471c1f94a5726d8`, where candidate zone changed correctly and visual frames corroborated Commons arrival and native Idle. However, stationary samples do not constitute moving-feed telemetry qualification.
  - Archive-derived zone strings and static dictionary keys remain **candidate identifiers** until qualified by continuous live telemetry.

### 2.2 Loaded Named Entities & Landmarks
- **Inspected Legacy Source (`wizwalker`)**:
  - `Client.get_base_entity_list()` (`wizwalker/client.py:149–155`): Traverses `client_object.parent().children()` to return loaded `WizClientObject` instances.
  - `Client.get_base_entities_with_name(name)` (`wizwalker/client.py:175–190`): Filters entities where `entity.object_template().object_name() == name`.
  - `Client.get_base_entities_with_display_name(display_name)` (`wizwalker/client.py:191–214`): Filters entities using localized `entity.display_name`.
- **Known Limitations**:
  - Memory traversal is restricted to loaded objects currently present in the active scene bubble; entities in other zones or distant unloaded cells cannot be enumerated.
  - Template names (internal asset keys) and localized display names require separate qualification. Duplicate entity names require spatial or contextual disambiguation.

### 2.3 In-Game Map & World-to-Map Projection
- **UI Structure**:
  - `wizwalker/client.py:215–226` exposes `get_world_view_window()`, locating `root_window.get_windows_with_name("WorldView")`.
  - `WorldView` is a **candidate UI root** for the 3D rendering viewport; it does **not** demonstrate the identity, hierarchy, or controls of the 2D in-game map panel. The actual in-game map window hierarchy (opened via keyboard shortcut `M`) remains uncharacterized in existing C# bindings.
- **Projection Reality**:
  - The absence of an imported class named `MapWindow` disproves that specific symbol, but does **not** disprove that an affine transformation operates between 3D world coordinates and 2D map pixels.
  - A general 2D affine map validly accommodates translation, rotation, and anisotropic scaling/shear. Whether a given zone map follows an affine projection or exhibits non-linear, hand-drawn artistic distortions remains a **hypothesis until empirically measured** against live observations.

---

## 3. Disproof and Scoping of External / "Argon" Claims

Direct inspection of repository source files and root's installed-archive evidence establishes the following scoped realities:

| Claimed Feature | Actual Source Reality | Evidence & File Location |
| :--- | :--- | :--- |
| **Claim 1: `wizwalker.file_readers.nav` exists** | **DISPROVEN in inspected copy**. The `wizwalker/file_readers` directory contains only `cache_handler.py`, `nif.py`, `wad.py`, and `__init__.py`. Nav parsing originates in Deimos `teleport_math.py`. | `ProjectMaelstrom/Scripts/Library/wizwalker/wizwalker-master/wizwalker/file_readers/` |
| **Claim 2: `wizwalker.file_readers.bcd` exists** | **DISPROVEN in inspected copy**. The `wizwalker/file_readers` directory contains no BCD reader. BCD collision parsing originates in Deimos `collision.py`. | `ProjectMaelstrom/Scripts/Library/wizwalker/wizwalker-master/wizwalker/file_readers/` |
| **Claim 3: `zone.nav` has a trailing portal table** | **DISPROVEN for inspected payloads**. In the two installed archives inspected read-only by root (`WC_Hub.wad` at 8,658 bytes and `WC_PET_Park.wad` at 3,408 bytes), decoded bytes are 100% accounted for by the header, vertex array, and edge array. Zero unparsed trailer bytes exist. | `Deimos-Wizard101-main/src/teleport_math.py:69–96` & `docs/coordination/results/W101-NAV-AUDIT-103.md:31–41` |
| **Claim 4: Fixed linear `MapWindow` transform** | **DISPROVEN as an existing API/symbol**. No class or symbol named `MapWindow` exists in `wizwalker`, `Deimos`, or `ProjectMaelstrom`. However, affine mapping remains a candidate mathematical model to test empirically. | Full repository search (`MapWindow` yields 0 matches). |

### 3.1 Verification of the Real Pet Pavilion Zone Path
- The actual engine path used by game files and internal lookups is:
  $$\text{Zone Path} = \texttt{"WizardCity/WC\_Streets/Interiors/WC\_PET\_Park"}$$
- Provenance:
  1. `ProjectMaelstrom/ProjectMaelstrom/Utilities/ZoneTopologyService.cs:37`: `{ "WizardCity/WC_Streets/Interiors/WC_PET_Park", "PET_PAVILION" }`
  2. `Automatus-v2-master/data/locations.py:33`: `"WizardCity/WC_Streets/Interiors/WC_PET_Park" : "PET_PAVILION"`
  3. Installed WAD filename: `WizardCity-WC_Streets-Interiors-WC_PET_Park.wad`

---

## 4. Documented Unknowns & Gaps

1. **Storage of Collision Triggers and Portals**:
   - Because `zone.nav` in the inspected payloads contains only graph vertices and edges, cross-zone portal triggers are **not** stored as a `zone.nav` trailer.
   - Their exact storage mechanism remains **unknown** (hypothesized to reside in `collision.bcd` volume triggers, entity template definitions, or scene graphs). Cross-zone transitions cannot be inferred from `zone.nav` files alone.
2. **Connectivity Geometry vs. Walkable Clearance**:
   - `zone.nav` contains candidate connectivity geometry (vertices and edges). It does **not** provide swept collision clearance, terrain slope limits, or obstacle boundaries.
   - Storage of reverse edges is a format observation, **not proof of two-way traversability**. Binding-109 identified explicit asymmetry in edge $95 \rightarrow 85$.
3. **Current-Client Pointer Freshness**:
   - Memory offsets in legacy `wizwalker` (e.g., offset `88` for `zone_name`) originate from older client versions and require validation against current live client builds.
4. **Player Arrow Convention**:
   - Whether the player arrow on the in-game map rotates with avatar yaw, camera yaw, or discrete heading steps remains unmeasured.

---

## 5. Proposed Metadata-Only Map Calibration Protocol & Bounded Metrics (PROPOSAL)

> [!NOTE]
> The following protocol is a **proposed offline specification** for when paired live evidence becomes available. Synthetic mathematical solvers verify solver correctness offline, but do not calibrate a real map. All numeric thresholds are **hypothetical and uncalibrated**.

### 5.1 Identity and Context Preconditions
Any calibration dataset must explicitly bind:
- `map_id`: Identifier of the zone map (e.g. `WC_Hub_Map`).
- `map_variant`: Client art variant (e.g., `Standard` vs. `ClassicMode`).
- `floor_level` / `interior_id`: Explicit floor index (for multi-level towers or catacombs).
- `client_resolution`: Render target dimensions (e.g. $1280 \times 720$).
- `zoom_level` and `pan_offset`: Must be verified at default/neutral state (0 zoom, 0 pan).

### 5.2 Control Point Acquisition
- **Training Set**: Collect at least 4 non-collinear stationary world points:
  $$P_i = (X_i, Y_i, Z_i) \quad \text{mapped to map canvas pixels} \quad p_i = (u_i, v_i)$$
  Control points must be widely distributed across the zone (e.g., north, south, east, west landmarks).
- **Held-Out Test Set**: Collect at least 3 independent held-out control points $Q_j = (X_j, Y_j, Z_j) \mapsto q_j = (u_j, v_j)$ not used in fitting.

### 5.3 Mathematical Model & Fitting
Fit a general 2D affine transformation:
$$\begin{pmatrix} u \\ v \end{pmatrix} = \begin{pmatrix} a & b \\ c & d \end{pmatrix} \begin{pmatrix} X \\ Y \end{pmatrix} + \begin{pmatrix} t_u \\ t_v \end{pmatrix}$$
using standard least-squares regression over the training set. A general affine map validly accommodates independent axis scaling, rotation, translation, and non-orthogonal shear (which naturally arises if map art axes are non-orthonormal).

### 5.4 Rejection Criteria & Bounded Metrics
A calibration proposal is **strictly rejected** based on the following proposed bounded metrics:
1. **Held-Out Residual Error Exceeded**: Mean residual error on independent held-out points exceeds tolerance:
   $$\text{MAE}_{\text{held-out}} = \frac{1}{M} \sum_{j=1}^M \| p(Q_j) - q_j \| > 3.0 \text{ pixels (hypothetical proposal)}$$
2. **Maximum Point Residual Error Exceeded**: Maximum residual on any individual held-out check point:
   $$\text{MaxError}_{\text{held-out}} = \max_j \| p(Q_j) - q_j \| > 6.0 \text{ pixels (hypothetical proposal)}$$
3. **Collinear Degeneracy**: Near-zero determinant $\det(A) < 10^{-6}$ or condition number $\kappa(A) > 1000$, indicating degenerate, collinear training sample points.
4. **Anisotropic Scale Feasibility**: Estimated scaling parameters $s_x = \sqrt{a^2 + b^2}, s_y = \sqrt{c^2 + d^2}$ must lie within plausible resolution bounds:
   $$s_x, s_y \in [0.001, 10.0] \text{ pixels/unit}$$
5. **Context Drift**: Any sample collected while loading, during unverified zoom/pan adjustments, across resolution shifts, or across floor levels.

---

## 6. Cross-Language Fixtures, Clocks & Continuous Sample Context

To ensure interoperability between C# native infrastructure, Python prototypes, and Rust bridge modules:

### 6.1 Exact Enum and State String Parity
- Discrete state enumerations (e.g., `NavigationEvidenceGrade`: `Unknown`, `Candidate`, `Repeatable`; `motion.State`: `"arrived"`, `"replan"`, `"paused"`, `"waiting"`, `"invalidated"`, `"stopped"`) must match exact character-for-character strings across all languages. No fuzzy case-insensitive mapping or synthetic default promotions may be used.

### 6.2 Explicit Floating-Point Tolerances
- Cross-language fixtures must **never assert bit-identical floating-point results**. Assertions must declare explicit epsilon tolerances (e.g., $|\Delta X| \le 0.001$, $|\Delta Y| \le 0.001$, $|\Delta \theta| \le 10^{-4}\text{ rad}$).

### 6.3 Exclusion of Dijkstra Visited-Count Parity
- Graph search algorithms (Dijkstra, A*, BFS) operating on `NavigationMap` may explore equal-cost vertices in different orders depending on priority queue tie-breaking or hash-set bucket iteration.
- Cross-language tests must assert **path cost, sequence validity, and destination arrival**, but **strictly exclude visited node counts** unless a deterministic secondary tie-breaker (e.g. vertex index) is explicitly standardized.

### 6.4 Clock Domain Conversion Envelope
- UI tree timestamps use `observedUtcTicks` (Int64, 100ns units since Unix/Windows epoch).
- Motion receipts use host monotonic milliseconds (double, $M_{\text{host}}$).
- **Direct numeric comparison between UTC ticks and monotonic milliseconds is strictly invalid**.
- **Conversion Envelope Specification**:
  1. At session initialization $t_0$, sample paired baseline timestamps $(T_{\text{utc\_0}}, M_{\text{host\_0}})$.
  2. Project any subsequent UI observation UTC ticks $T_{\text{obs}}$ into the host monotonic frame:
     $$\tau(T_{\text{obs}}) = M_{\text{host\_0}} + \frac{T_{\text{obs}} - T_{\text{utc\_0}}}{10,000} \quad (\text{since } 1\text{ ms} = 10,000\text{ ticks})$$
  3. Formulate the synchronization uncertainty bound $\pm \Delta_{\text{sync}}$:
     $$\Delta_{\text{sync}} = \frac{1}{2} (\text{RTT}_{\text{sample}} + \text{RTT}_{\text{receipt}}) + \epsilon_{\text{drift}}$$
  4. An observation $\tau(T)$ can only be causally bound to an actuation receipt $M_{\text{receipt}}$ if $| \tau(T) - M_{\text{receipt}} | \le \Delta_{\text{sync}}$. Samples falling outside this envelope must be rejected as unsynchronized.

### 6.5 Continuous Sample Context Fences
Live telemetry cannot be processed as an unauthenticated coordinate stream. Every moving-pose sample must be validated against four continuous context fences:
1. **Target Lifetime Fence**: Binds the active process ID, process creation time, and game window handle. If the target exits, crashes, or restarts, all in-flight moving samples are immediately invalidated.
2. **Actor Identity Fence**: The wizard subject ID and actor memory base pointer must remain unchanged throughout motion. If character logout, re-selection, or vehicle dismount occurs, the session invalidates immediately.
3. **Loading Fence**: If the `loading` flag goes true or a world fade begins, motion inputs are instantly released, and in-flight waypoints are invalidated.
4. **Zone Fence**: The observed zone (`ClientZone.zone_name`) must strictly match the segment's expected zone identity. Any unpredicted zone change terminates the path follower and latches replanning.

---

## 7. Audit of Candidate Graph Bindings, Live Movement Evidence, and Scheduler Contracts

Static inspection of root's candidate bindings (`.local/nav-binding-108/`, `.local/nav-binding-109/`), measured movement evidence (`02-pulse/read-23/ui-result.json`), collision source primitives (`Deimos/src/collision.py`), and native scheduler contracts (`NavigationAtlas.cs`, `W101PathSession.cs`, `W101MovingProbe.cs`) reveals **three consolidated operational gaps**:

### 7.1 Gap 1: Large Elevation Steps & Collision Primitive Validation along Edges 183 $\rightarrow$ 192 and 199 $\rightarrow$ 209
- **Candidate Path Evidence & Elevation Bottlenecks (`nav-binding-109`)**:
  - The direct Commons path from vertex 89 to 209 spans 14 vertices and $7,471.86$ units.
  - Steep segments measured by root:
    - Edge $183 \rightarrow 192$: Rises $\Delta Z = +137.786$ over $\Delta XY = 565.550$ (slope $\approx 0.244$, or $\sim 13.7^\circ$).
    - Edge $199 \rightarrow 209$: Drops $\Delta Z = -140.176$ over $\Delta XY = 513.608$ (slope $\approx -0.273$, or $\sim 15.3^\circ$).
  - *Alternative Path Analysis*:
    - Excluding edge $199 \rightarrow 209$ forces a 22-vertex detour of $12,228.161$ units ($+63.7\%$ distance).
    - Excluding all edges with $|\Delta Z| > 40$ yields `Found: false` ("unreachable", 101 vertices visited). This mathematically proves that **elevation edges with $|\Delta Z| > 40$ are topological bottlenecks**; reaching Pet Pavilion tunnel from Commons start is impossible without ascending/descending steep structures.
- **Testing Methodology via `collision.bcd` Primitives**:
  - `collision.py` defines `ProxyGeometry` and `ProxyMesh` holding `category_flags` (`WALKABLE = 1 << 1`, `OBJECT = 1 << 0`, `TRIGGER = 1 << 8`). Helper decoders install no hooks; the surrounding observer serializes `PlayerHook` access.
  - Offline testing requires:
    1. *Downward Raycast Floor Contact*: Cast vertical rays at hypothetical 10–20 unit intervals along the 3D line segment against `ProxyMesh` faces marked `WALKABLE` to confirm continuous floor support ($|Z_{\text{segment}} - Z_{\text{floor}}| \le 10$ units).
    2. *Player Cylinder Swept-Volume Obstacle Check*: Sweep an oriented player bounding cylinder (hypothetical $R \approx 20\text{--}30$, $H \approx 60\text{--}90$) along the segment against `OBJECT` proxies to ensure railings or tunnel arches do not occlude the path.
    3. *Trigger Infiltration*: Check if the corridor intersects any `TRIGGER` volume that could induce unintended zone loading or combat initiation.
- **Collision Transform Uncertainties**:
  - Review of `Deimos/src/collision.py:245–350` identifies four mathematical ambiguities: transform order ($R(S \cdot P) + T$ vs. $S(R \cdot P) + T$), row-major vs. column-major 3x3 matrix unpacking, vertex pre-scaling vs. object scale, and coordinate handedness/up-axis alignment.

### 7.2 Gap 2: Heading Convention vs. Planar Displacement and Connector Offsets
- **Measured Live Movement Evidence (`02-pulse/read-23/ui-result.json`)**:
  - Root completed three sampled pulse operations, all verified in native Idle:
    - Right turn probe (`9d137e96875e4661a5705ce91afed023`): yaw delta $+0.241275\text{ rad}$.
    - Left turn probe (`01f67afdbc7840bcb39813988321892c`): yaw delta $-0.291540\text{ rad}$.
    - Forward motion probe (`d9f249d8c80042dd88761feb3304f0cc`): planar displacement $\Delta XY = (48.907349, -36.790131)$.
  - *Rate Inference Warning*: Precise angular or linear velocities **must not be inferred** from nominal $100\text{ ms}$ input requests due to input delivery latency, OS scheduling, and game loop tick quantization.
- **Heading Convention versus Planar Displacement**:
  - The forward displacement vector $(48.907, -36.790)$ forms a planar bearing angle $\text{atan2}(-36.790, 48.907) \approx -0.644\text{ rad}$ ($\approx -36.9^\circ$).
  - In `W101PathSession.DecodeCandidate`, the client yaw angle is transformed as:
    $$\text{heading} = \text{atan2}(-\cos(\text{yaw}), -\sin(\text{yaw}))$$
  - This non-standard trigonometric mapping reflects game engine orientation conventions where $0$ yaw does not align with standard $+X$ Cartesian east. Controllers must strictly bind to this empirical transform.
- **Connector Projection Risks**:
  - In candidate bindings, Commons start vertex 89 has offset $219.78$ XY and heading error $-2.76\text{ rad}$ ($\approx -158^\circ$, nearly backward); goal vertex 209 has offset $269.15$ XY. Traversing $>200$ units of unverified free space with extreme initial turns risks collisions before graph acquisition.

### 7.3 Gap 3: Readiness Handshake, Guaranteed Cleanup, and Minimum Arrival Evidence
- **Readiness Handshake & Cleanup Contract Precision (`W101MovingProbe.cs`)**:
  - `W101MovingProbe.WindowReady` enforces a strict $100\text{ ms}$ freshness window on lines matching `kind == "pose-window-ready"` with `safe == true`.
  - `RunAsync` executes under an 8-second linked CTS ceiling (`stop.CancelAfter(TimeSpan.FromSeconds(8))`) and polls readiness at 10ms intervals.
  - **Correction on Guaranteed Restoration**: Awaiting cleanup (`await reading.ConfigureAwait(false)` in `finally`) does **not** prove successful hook restoration. If an uncooperative observer hangs, deadlocks, or throws, awaiting it can hang indefinitely or exit without unhooking. Robust safety requires:
    1. An outer process/wall-clock deadline bounding the cleanup phase.
    2. Explicit worker cleanup evidence (e.g., receipt confirming original memory bytes restored).
    3. Escalation to `RequireRecovery("Observer cleanup unconfirmed")` halting all dispatches if unverified.
- **Minimum Arrival Evidence: Open Portal vs. Named Prompt**:
  - *Named Prompt Arrival (e.g. Pet Dance Sigil, NPC Interaction)*:
    - *Counterexample*: A character arrives within 15 units of the Dance tent but facing away; no "Dance Game" prompt appears. Pressing 'X' does nothing or opens an unintended menu.
    - *Minimum Evidence*: Spatial proximity within anchor tolerance AND readable interaction prompt matching expected title (`RequiredPrompt`) verified in the UI tree (`PromptVerified == true`).
  - *Open Portal / Threshold Arrival (e.g. Commons to Pet Pavilion Tunnel)*:
    - *Counterexample 1*: Waiting for an 'X' prompt at an open portal results in a permanent timeout deadlock, as open portals have no interaction prompt.
    - *Counterexample 2*: Stopping 5 units short of the trigger volume due to deceleration. If coordinate proximity alone is accepted, the system falsely assumes it reached Pet Pavilion while still standing in Commons.
    - *Minimum Evidence*: Motion into the portal entry corridor AND observed transition event: `loading == true` (screen blackout) followed by verified destination zone change (`zone_name == "WizardCity/WC_Streets/Interiors/WC_PET_Park"`) and verified native Idle. Coordinate arrival is merely a precursor; destination zone confirmation is mandatory.
- **Review of External Steering Proposals**:
  - External/Argon smoothing proposals asserting "three scalars / three seconds / 95 percent" remain unsupported research hypotheses.
  - Clearance formulas lacking geometric swept-volume definitions or collision proofs against `collision.bcd` cannot be adopted.
  - Core safety boundaries remain non-negotiable: all dispatches must flow through `GameInputLease`, input release must be guaranteed on failure/cancellation, and no unhedged background input dispatch or focus theft is authorized.

---

## 8. Three Prioritized Metadata Qualification Tasks

To support destination-name navigation without duplicating Task 104 parser or Task 105 follower implementations:

### Task 1: Offline Zone Topology & WAD Name Consistency Audit
- **Objective**: Verify that every zone entry in `ZoneTopologyService.cs` (55 WizardCity entries) strictly maps to an existing WAD archive naming pattern (`WizardCity-<Zone>.wad`) and Automatus route graph.
- **Verification**: Pure offline automated dictionary consistency check; zero live access required.

### Task 2: Offline Collision Primitive Parser & Transform Ordering Audit
- **Objective**: Implement a standalone offline parser for `collision.bcd` based on `Deimos/src/collision.py`, mathematically resolving matrix layout (row vs. column major), transform order, and scale application against known stationary geometry.
- **Verification**: Test against the 218 KiB `WC_Hub` and 32 KiB `WC_PET_Park` collision payloads; confirm floor mesh alignment beneath candidate nav vertices.

### Task 3: Paired In-Game Map Calibration Data-Collection Harness (Specification Only)
- **Objective**: Author the schema and observation recorder contract for collecting the paired $(X, Y, Z) \leftrightarrow (u, v)$ points specified in Section 5, ensuring all samples record client resolution, map variant, and avatar yaw.
- **Verification**: Schema validation against synthetic test vectors; live data collection deferred until authorized by root coordinator.

---

## 9. Handback & Next Steps

This revised report incorporates all precision corrections regarding observer cleanup deadlines, continuous sample context fences, clock domain conversion envelopes, and open portal vs. named prompt arrival evidence.

- **Delivered**: `docs/coordination/results/GEM-MAP-METADATA-107.md`.
- **Next Step**: Append `GEM-0075` (`Kind: READY_FOR_REVIEW`) to `docs/coordination/gemini/FROM_GEMINI.md` and stand down awaiting coordinator review.
