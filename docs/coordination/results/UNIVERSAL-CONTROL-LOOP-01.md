# Maelstrom Universal 4-Layer Control Loop: Architecture & Scaffold Report

> **Revision (2026-11-15, UNIVERSAL-BUS-CORRECTION-01):** Corrected following independent adversarial audit.
> Reframed from "complete control loop" to **scaffolded / component-level architecture with host IPC transport and CLI actuator unwired**.
> Corrected test count arithmetic to 3 (`CircuitSystemTests`), 24 (three named classes), 28 (total native circuit suite), 28 (Python MCP), 78 (Rust workspace).
> Added explicit Unimplemented / Mock Boundaries section (§4).

## Executive Summary
Defines and unit-tests contracts and declarative decision routing for the 4-layer unidirectional control loop:
**API Feeds $\rightarrow$ MCP Commands $\rightarrow$ CLI Executes $\rightarrow$ User Enables**

This decouples sensory perception from decision routing and physical actuation, keeping Python strictly declarative while high-speed perception runs in C#/.NET 9 and Rust.
*Note: This deliverable implements and verifies the component contracts, perceptual adapter, and declarative MCP routing under unit-test mocks. End-to-end host named-pipe integration (`JobDeckHost.cs`), CLI execution verbs (`MaelstromCli.cs`), and production Win32 actuation (`IActuator`) remain unwired.*

---

## 1. Architecture Topology (Design Model)

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ LAYER 4: USER ENABLES (Hardware Authority & Policy Interlock)                │
│ • Physical Deadman Switch: GameInputLease (WH_KEYBOARD_LL / Mouse Hook)      │
│ • Policy Boundaries: MinGoldReserveFloor, Zero-Crown Gate, 3-Tuple HWND Lock │
└───────────────┬──────────────────────────────────────────────▲───────────────┘
                │ Grants / Revokes Lease                       │ Telemetry / UI
                ▼                                              │
┌──────────────────────────────────────────────────────────────┴───────────────┐
│ LAYER 1: API FEEDS (Read-Only Sensory & Comparator Server - SCAFFOLDED)       │
│ • Pure Input Bus: FastPerceptualAdapter + PerceptualGate (64-bit dHash/SIMD) │
│ • Exposes: WorldBits (ulong bus), Integer Registers (Gold/Energy/Rows)       │
│ • Zero Side Effects: Cannot click, cannot press keys, cannot mutate state.   │
│ • Status: Implemented as in-process adapter; JobDeckHost IPC handler unwired.│
└───────────────────────────────────────────────┬──────────────────────────────┘
                                                │ [UNWIRED: Host IPC missing]
                                                ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│ LAYER 2: MCP COMMANDS (Declarative Capability & Decision Routing)            │
│ • Capability Catalog: Declares legal Cards/Tools & Pre-Conditions            │
│ • Evaluates: WorldBits vs. Card Rungs (BazaarSniper, PetDance, CombatPlanner)│
│ • Emits: Bounded Executive Intent Proposals (e.g., "Click Anchor X")         │
│ • Status: Propose tools functional offline; Read tools return unwired error. │
└───────────────────────────────────────────────┬──────────────────────────────┘
                                                │ [UNWIRED: CLI command missing]
                                                ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│ LAYER 3: CLI EXECUTES (Deterministic Actuator & Receipt Verifier - UNWIRED)  │
│ • Execution Model: AtomicAnchorActuator contract (checks lease -> clicks)    │
│ • Status: IActuator has test mocks only; MaelstromCli has no actuate verb.   │
└──────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Implemented Components

### Layer 1: Perceptual API Feeds (`ProjectMaelstrom/ProjectMaelstrom/Utilities/Circuit/`)
1. **`AnchorRegistry.cs`**:
   - Parses and caches layout anchors from `ui_anchors.v1.json`.
   - Supports table row slicing via `row_stride_y` and column bounds (`name`, `quantity`, `price`).
2. **`CircuitSnapshot.cs`**:
   - Immutable Layer 1 sensory packet containing the 64-bit `WorldBits` status word, discrete flags (`LeaseActive`, `ClientFocused`, `BazaarOpen`, `ConfirmModalOpen`, `NativeIdleBanner`), numeric registers (`PlayerGold`, `PlayerEnergy`, `VisibleRowsCount`), and parsed row structs.
   - Sub-millisecond JSON serialization/deserialization.
3. **`FastPerceptualAdapter.cs`**:
   - Implements `IWorldState` (`ReadAnchorTextAsync`, `ReadBazaarRowsAsync`, `WaitForReceiptAsync`) and `ICircuitFeed`.
   - Produces `CircuitSnapshot` with zero heap allocations in under 1 millisecond.
4. **`CircuitServer.cs`**:
   - Server handler exposing `HandleSnapshotRequest` over named-pipe IPC and in-process hosts.

### Layer 2: Declarative MCP Server (`tools/maelstrom_mcp_server.py`)
1. **`maelstrom_read_circuit_snapshot` & `maelstrom_read_circuit_bus`**:
   - Scaffolded read endpoints. Currently return `{"ok": False, "error": "unwired_circuit_scaffold: ..."}` because native `JobDeckHost` does not implement `"circuit-snapshot"`.
2. **`maelstrom_propose_bazaar_action`**:
   - Pure declarative combinational logic evaluating `CircuitSnapshot` against user policy (`target_item`, `max_unit_price`, `min_gold_reserve_floor`).
   - Respects deadman switch: yields `HOLD_LEASE_INACTIVE` when human touches mouse/keyboard.
   - Respects financial safety floor: aborts with `ABORT_GOLD_FLOOR` when gold $\le$ floor.
   - Respects modal confirmations and proposes atomic actions (`click_row`, `click_anchor`).
3. **`maelstrom_propose_anchor_actuation`**:
   - Pure declarative validator asserting anchor existence and lease state, emitting bounded command proposals.

---

## 3. Verification & Invariants

1. **.NET Unit Tests**:
   - `CircuitSystemTests.cs`: **3/3** tests passing.
   - Combined three classes (`CircuitSystemTests` 3, `PerceptualGateTests` 8, `BazaarSniperCardTests` 13): **24/24 passing**.
   - Total native circuit suite (including `UniversalBusTests.cs` 4): **28/28 passing**.
2. **MCP Unit Tests**:
   - `test_maelstrom_mcp_server.py`: **28/28 tests passing** (0.016s), including unwired scaffold error assertions for the read tools.
3. **Static Analysis & Linting**:
   - `dotnet format whitespace --verify-no-changes`: Passed (0 changes).
   - `dotnet format analyzers --verify-no-changes`: Passed (0 warnings).
   - `python -m compileall`: Passed (0 errors).
   - `python -m black --check`: Passed (clean formatting).
   - `python tools/check_project_board.py`: Passed (30 workstreams valid).
4. **Rust Workspace**:
   - `cargo test --workspace`: **78 passed, 0 failed** across 5 crates (`maelstrom-core`, `maelstrom-platform`, `maelstrom-fallback`, `maelstrom-reflex`, `maelstrom-ide`).

---

## 4. Unimplemented & Mock Boundaries

1. **Host IPC Transport Unwired**: `JobDeckHost.cs:85–108` has no `"circuit-snapshot"` command case. Any invocation of `call_deck("circuit-snapshot")` against a real host throws `"Unknown deck command."` The MCP read tools explicitly return an unwired error until a coordinator claim integrates `CircuitServer` into `JobDeckHost`.
2. **CLI Actuator Unimplemented**: `MaelstromCli.cs` does not contain `actuate-anchor` or any circuit-related command. The 23 existing CLI commands are the pre-existing observation/inspection commands.
3. **Production Actuator Unimplemented**: `IActuator` has no production Win32 input implementation; only `MockActuator` in test fixtures exists.
4. **Fail-Closed Guarantee**: The scaffolded code is strictly fail-closed: `FastPerceptualAdapter` lease probes default `false`, `AtomicAnchorActuator` refuses actuation without an active lease, and no code paths dispatch unreviewed inputs.
5. **No Live Qualification**: All tests are offline unit tests. Zero live game execution or qualification is claimed.
