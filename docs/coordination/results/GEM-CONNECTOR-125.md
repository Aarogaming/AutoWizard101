# GEM-CONNECTOR-125: Dynamic Navigation Connector Geometry & Atlas Planning Audit (Revised v13)

**Task ID**: `GEM-CONNECTOR-125`  
**Claim Owner**: `gemini` (Active in `docs/coordination/claims.json`)  
**Status**: Read-only static review and architectural gap audit; offline analysis only.  
**Deliverable Path**: `docs/coordination/results/GEM-CONNECTOR-125.md`  
**Revision Directives**: Revised per `LIA-0082` through `LIA-0101` to:
1. Tighten `ActorBody` height inference per `LIA-0101`: remove unsourced Havok capsule assertions; state grounded finding that observed variance ($175.75 \to 213.86$) disproves treating offset 132 as a fixed constant, while physical collision shapes remain unknown and unproven.
2. Tighten floor sheet boundary filtering inference per `LIA-0101`: frame 3D vertical filtering in `NavigationFloorSheet.cs:59–62` as an uncalibrated hypothesis, noting that 2D distance is an intentional conservative guard and specifying prerequisites (swept-body envelope, overpass classification, adjoining face continuity) before any repair.
3. Review Task 141 empirical trial evidence (`W101-CONNECTOR-TRIAL-141.md`, `.local/connector-forward-141.json`, `.local/connector-heading-141b.json`).
4. Retain exact source citations for `ActorBody` offset 132 (`"This body's height"`) and Box parameters (`w101_bcd_evidence.py:41`), confirming both visual and collision engine ground-truth are not found in available source code.
5. Document client heading coordinate convention: client target yaw $4.016607489684186\text{ rad}$ (distinct from planar mathematical angle $0.6958\text{ rad}$).
6. Maintain strict classification: `contextQualified = false` must remain unpromoted; zero source/build/live authority.
7. Scoped strictly to at most three current actionable findings with exact line references and proposed pure unit tests.

---

## 1. Executive Summary & Task 141 Live Telemetry Demarcation

This static audit reviews native connector geometry verification (`NavigationConnectorGeometry.cs`, `NavigationFloorSheet.cs`), dynamic route planning (`NavigationAtlas.cs`), and the live trace transport and observation pipeline (`W101TraceTail.cs`, `W101PoseStreamConsumer.cs`, `W101PoseStreamObservation.cs`).

### 1.1 Task 141 Empirical Trial Evidence (`W101-CONNECTOR-TRIAL-141.md`)
- **Closed-Loop Alignment Evidence**:
  - Run 1 (`6ac08ac7cf324929af24c927d176627b`): Four turns ($\le 100\text{ms}$) shifted yaw $5.53338 \to 4.43005\text{ rad}$ at unchanged XYZ; turn-budget exhausted with observed late response $\approx 2.71\text{ rad/s}$. Idle independently verified.
  - Run 2 (`4fb4ec931ae64c169b4417777fe75d29`): Bounded $140\text{ms}$ A turn reached yaw $3.9776608944\text{ rad}$ against client target $4.0166074897\text{ rad}$ (angular error $0.0389466\text{ rad}$, confirmed twice). XYZ unchanged; native Idle verified.
- **Empirical Forward Step Evidence (`afb00f8890094cbb82160f465b9ec574`)**:
  - Dispatched $W$ 60ms through the shared owner under `W101NavigationVisit`.
  - Position changed from $(707.1978, -231.1267, -27.5061)$ to $(736.1363, -204.9817, -27.6098)$.
  - Planar displacement: $\Delta XY = 38.999978\text{ units}$. Elevation delta: $\Delta Z = -0.1038\text{ units}$.
  - Movement direction: $\Delta X = +28.9385, \Delta Y = +26.1450 \implies \text{heading} \approx 42.1^\circ$, advancing cleanly along the corridor toward vertex 113.
  - Same candidate Commons zone (`WizardCity/WC_Hub`) and registry-owner endpoint binding; key release and native Idle confirmed.
- **Strict Boundary Declarations**:
  - The observed $39\text{ unit}$ step qualifies physical clearance across that initial measured segment only; it does **not** certify the remaining $198\text{ units}$ to vertex 113, swept clearance for arbitrary radii, or route completion to Pet Pavilion.
  - Telemetry explicitly asserts `contextQualified = false`.
  - Zero changes to production code or test suites; zero uncoordinated builds or tests; zero live client interaction or foreground control.

---

## 2. Tightened Inferences: ActorBody Height Semantics & Floor Sheet Bounds (LIA-0099, LIA-0100, LIA-0101)

### 2.1 Grounded Findings on `ActorBody` Offset 132 (LIA-0101)
Per coordinator directive `LIA-0101`, unsourced generalizations regarding Havok collision capsules are removed. The factual determinations are grounded strictly in code and receipts:

1. **Exact Source Text in `wizwalker/memory_objects/actor_body.py:97–104`**:
   ```python
   async def height(self) -> float:
       """
       This body's height

       Returns:
           Float representing height
       """
       return await self.read_value_from_offset(132, "float")
   ```
   - The source documents only `"This body's height"` and reads an IEEE 754 single-precision float at offset 132.
   - The source does **not** establish whether this field represents visual mesh extents, Havok capsule parameters, animation bounding boxes, or authoring scalars.
   - Both visual-only and collision-only interpretations are **unproven hypotheses**; exact engine semantics are **not found** in repository or vendor code.
2. **Observed Temporal Variance in Live Telemetry**:
   - Task 140 Stationary Observation (`67750c63fa8c4b1aac9bdeab570a8453`): offset 132 read $213.1475067138672$ ($\text{scale} = 1.0$).
   - Task 141 Pre-Step Observation (`afb00f8890094cbb82160f465b9ec574`): offset 132 read $175.75241088867188$ ($\text{scale} = 1.0$).
   - Task 141 Post-Step Observation (`afb00f8890094cbb82160f465b9ec574`): offset 132 read $213.8563690185547$ ($\text{scale} = 1.0$).
3. **Tightened Architectural Inference**:
   - This observed $\approx 38.10\text{ unit}$ fluctuation across motion on the exact same character with identical scale 1.0 proves solely that **offset 132 is a dynamic, time-varying field**, disproving any assumption that it is a fixed scalar constant.
   - However, this variation alone does **not** prove what physical collision shapes do, whether they are static or dynamic, or how the physics engine evaluates clearances.
   - Because physical collider geometry remains unexposed and unmeasured, offset 132 cannot be used as a static clearance threshold in path planning.

---

### 2.2 Floor Sheet Boundary Filtering as an Uncalibrated Hypothesis (LIA-0101)
In `ProjectMaelstrom/ProjectMaelstrom/Utilities/NavigationFloorSheet.cs:59–62`:
```csharp
// Global XY boundary distance is conservative even for distant folded sheets.
foreach (var (edge, owners) in edges)
    if (owners.Any(seen.Contains) && (owners.Count != 2 || !owners.All(seen.Contains) || !adjacency[owners[0]].Contains(owners[1])))
        if (Distance(start, end, edge.Item1, edge.Item2) < radius) return null;
```

1. **Intentional Conservative Guard**:
   - The existing code comment explicitly affirms that global 2D $(X, Y)$ boundary distance is an intentional conservative mechanism to prevent avatar drop-offs or clipping when floor sheets fold or curve.
2. **The 3D Vertical Envelope Proposal is a Hypothesis**:
   - Filtering boundary checks by vertical proximity ($[\min(Z) - \text{tol}, \max(Z) + \text{height}]$) is an uncalibrated engineering hypothesis, **not an unconditional repair**.
   - An unconditional vertical filter risks severe safety regressions: in multi-level terrain, drop-offs, sheer cliffs, and floor edges *below* the character or vertical wall limits *above* the character represent true physical boundaries that must not be ignored.
3. **Required Engineering Prerequisites Before Repair**:
   - Before any 3D vertical boundary check could be safely introduced, the following must be formally qualified:
     a) **Swept-Body Vertical Envelope**: Calibrated physical bounds for the avatar ($Z_{\text{feet}}$ to $Z_{\text{head}}$);
     b) **Overhead Geometry Classification**: Reliable classification distinguishing traversable overhead archways/overpasses from sheer vertical walls or drop-off boundaries;
     c) **Adjoining Face Continuity**: Qualification of manifold slope continuity and vertical headroom clearance across all triangles adjoining the boundary edge.

---

### 2.3 Heading Coordinate Convention vs. Planar Vector Heading
- As demonstrated in Task 141, the Wizard101 client uses an offset coordinate frame where the target yaw command for the $(707.2, -231.1) \to (889.3, -79.1)$ vector is:
  $$\text{Client Target Yaw} = 4.016607489684186\text{ rad}$$
- In Task 141 retry `4fb4ec931ae64c169b4417777fe75d29`, the client aligned to yaw $3.9776608944\text{ rad}$ (angular error $0.0389466\text{ rad}$).
- Planar mathematical angle $0.6958\text{ rad}$ cannot be substituted as a live client turn command.

---

### 2.4 Independent BCD Box Size/Axis Conventions (Proxy 88)
- **Exact Source Text in `tools/w101_bcd_evidence.py:40–44`**:
  ```python
  if kind == "BOX":
      # Transform the enclosing local box, not its much larger sphere.
      # Retain full dimensions as half-extents until conventions qualify.
      extents = [
          abs(scale) * sum(abs(rotation[i * 3 + j]) * dims[j] for j in range(3))
          for i in range(3)
      ]
  ```
- **Factual Determination**:
  1. The code explicitly notes that using full dimensions as half-extents is provisional (`"# Retain full dimensions as half-extents until conventions qualify."`).
  2. Ground-truth engine documentation for whether BCD box `params` represent full side lengths or half-extents is **not found** in repository or vendor sources.
  3. Deimos `cube_to_vertices` halves dimensions but also swaps width and length axes ($X/Y$). Cherry-picking halving while ignoring axis swapping is mathematically unsound.
  4. Proxy 88 clearance across the remainder of the connector can only be verified empirically or through ground-truth disassembly of the client's shape builder.

---

## 3. Three Highest-Priority Current Actionable Findings

### Finding 1: Variable ActorBody Height Semantics and Unmeasured Physical Capsule Profile
- **Exact Call Chain & Evidence**:
  - `tools/w101_pose_reader.py:13–41` (`read_dimensions`): Reads offset 132.
  - `W101-CONNECTOR-TRIAL-141.md:14–17`: Live readings fluctuate between $175.7524$ and $213.8564$ across motion on the same character at scale 1.0.
  - `wizwalker/memory_objects/actor_body.py:97–104`: Contains only `"This body's height"`.
- **Mechanism & Architectural Impact**:
  1. Offset 132 is dynamic over time; treating it as a fixed collision height causes arbitrary clearance rejections.
  2. The actual physical collision capsule radius and height remain uncalibrated.
- **Smallest Reusable Repair**:
  Retain `bodyDimensionsCandidate.collisionQualified = false`. In navigation planning, avoid rejecting corridors based on offset 132; instead, evaluate corridor passability through bounded empirical trials.
- **Proposed Pure Unit Test**:
  `tools/tests/test_w101_pose_reader.py:test_dimensions_read_retains_collision_unqualified`

---

### Finding 2: Unresolved BCD Box Parameter Conventions & Remaining Segment to Vertex 113
- **Exact Call Chain & Evidence**:
  - `tools/w101_bcd_evidence.py:40–45`: Encloses boxes using full dimensions as half-extents (`factor = 1.0`), flagging Proxy 88 as an obstruction.
  - `W101-CONNECTOR-TRIAL-141.md:10`: The live forward step advanced $39.0\text{ units}$, leaving $\approx 198\text{ units}$ of the dynamic connector unverified.
- **Mechanism & Architectural Impact**:
  1. Whether Proxy 88 is an obstruction remains mathematically unprovable from static metadata without ground-truth box parameter conventions.
  2. The avatar has physically proven passage only across the first $39\text{ units}$; swept corridor clearance to vertex 113 remains unqualified.
- **Smallest Reusable Repair**:
  Continue bounded forward steps under `W101NavigationVisit` and `GameInputLease` with native Idle verification to empirically advance toward vertex 113 while monitoring for physical collision stalls.
- **Proposed Pure Unit Test**:
  `ProjectMaelstrom.Tests/NavigationAtlasTests.cs:PlanDynamic_WhenBoxConventionUnresolved_RequiresEmpiricalSegmentQualification`

---

### Finding 3: Multi-Layer Overpass / Balcony Boundary Rejection Hypothesis in Floor Sheet Geometry
- **Exact Call Chain & Evidence**:
  - `ProjectMaelstrom/ProjectMaelstrom/Utilities/NavigationFloorSheet.cs:59–62`:
    ```csharp
    // Global XY boundary distance is conservative even for distant folded sheets.
    foreach (var (edge, owners) in edges)
        if (owners.Any(seen.Contains) && (owners.Count != 2 || !owners.All(seen.Contains) || !adjacency[owners[0]].Contains(owners[1])))
            if (Distance(start, end, edge.Item1, edge.Item2) < radius) return null;
    ```
  - `ProjectMaelstrom/ProjectMaelstrom/Utilities/NavigationFloorSheet.cs:103–113` (`Distance`).
- **Mechanism & Architectural Impact**:
  1. Boundary distance checks operate strictly in 2D $(X, Y)$, which is an intentional conservative guard for folded sheets, but risks rejecting paths beneath overhead overpasses or balconies.
  2. Proposing an unconditional vertical filter is premature without qualifying the swept-body vertical envelope, distinguishing overpasses from cliffs/drop-offs, and confirming adjoining face continuity.
- **Smallest Reusable Repair**:
  Formulate vertical boundary filtering as a conditional research hypothesis; require that any future 3D boundary evaluation depend on calibrated body envelopes and classified architectural overpasses.
- **Proposed Pure Unit Test**:
  `ProjectMaelstrom.Tests/NavigationFloorSheetTests.cs:Trace_WhenEvaluatingMultiTierBoundaries_RequiresClassifiedOverpassEnvelope`

---

## 4. Proposed Pure Test Specifications

To address these three findings cleanly without modifying production code:

| Finding # | Proposed Test Name | Target Test Suite | Scope & Invariant Verified |
| :--- | :--- | :--- | :--- |
| **Finding 1** | `test_dimensions_read_retains_collision_unqualified` | `tools/tests/test_w101_pose_reader.py` | Asserts that `read_dimensions` always returns `collisionQualified: false` regardless of raw height value. |
| **Finding 2** | `PlanDynamic_WhenBoxConventionUnresolved_RequiresEmpiricalSegmentQualification` | `ProjectMaelstrom.Tests/NavigationAtlasTests.cs` | Asserts that dynamic connectors with unverified box blockers require empirical traversal qualification before promotion. |
| **Finding 3** | `Trace_WhenEvaluatingMultiTierBoundaries_RequiresClassifiedOverpassEnvelope` | `ProjectMaelstrom.Tests/NavigationFloorSheetTests.cs` | Asserts that 3D boundary filtering requires a qualified overpass envelope and does not compromise conservative 2D cliff drop-off rejection. |

> [!NOTE]
> All proposed unit tests are strictly **[PROPOSED ONLY - NOT IMPLEMENTED]**. In accordance with the Codex Working Agreement and directives `LIA-0082` through `LIA-0101`, no test files or production source files have been modified.

---

## 5. Handback & Next Steps

This revised report incorporates all directives from `LIA-0098` through `LIA-0101`:
- Tightened `ActorBody` height inference: removed unsourced Havok assertions; established that observed variance ($175.75 \to 213.86$) disproves treating offset 132 as a fixed constant, while physical collision shapes remain unknown and unproven.
- Tightened floor sheet boundary filtering: framed 3D vertical proximity as an uncalibrated hypothesis, noting the intentional conservative guard in `NavigationFloorSheet.cs:59` and detailing prerequisites (swept-body envelope, overpass classification, adjoining face continuity).
- Integrated Task 141 empirical trial evidence: closed-loop heading alignment reached yaw $3.97766\text{ rad}$ (error $0.03895\text{ rad}$ against client target $4.01661\text{ rad}$); forward pulse achieved $39.0\text{ units}$ advance toward vertex 113 with verified native Idle cleanup.
- Quoted exact source for Box parameters (`w101_bcd_evidence.py:41`); noted that full vs. half-extent ground-truth is not found in repository source.

- **Delivered**: `docs/coordination/results/GEM-CONNECTOR-125.md`.
- **Next Step**: Append `GEM-0095` (`Kind: READY_FOR_REVIEW`) to `docs/coordination/gemini/FROM_GEMINI.md` and stand down awaiting root coordinator review.
