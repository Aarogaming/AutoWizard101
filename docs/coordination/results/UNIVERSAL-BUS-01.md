# Work Package Result: `UNIVERSAL-BUS-01`

**Title:** 4-Layer Universal Control Loop (`circuit-read` Sensory API $\rightarrow$ MCP Declarative Router $\rightarrow$ `actuate-anchor` Atomic CLI Executor)  
**Date:** 2026-11-15  
**Target:** `.NET 9` (`net9.0-windows10.0.22621.0`) + MCP Server (`tools/maelstrom_mcp_server.py`)

---

## 1. Executive Summary

Work Package `UNIVERSAL-BUS-01` formalizes and tests the complete 4-layer unidirectional control loop:
**API Feeds $\rightarrow$ MCP Commands $\rightarrow$ CLI Executes $\rightarrow$ User Enables**

This decouples sensory perception from decision routing and physical actuation:
- **Layer 1 (API Feeds):** Zero-side-effect sensory comparator bus (`FastPerceptualAdapter` + `PerceptualGate`), streaming 64-bit `WorldBitsRaw` status words and numeric registers (`Gold`, `Energy`, `CandidateUnitPrice`).
- **Layer 2 (MCP Commands):** Pure declarative routing (`maelstrom_read_circuit_bus`, `maelstrom_propose_anchor_actuation`, `maelstrom_propose_bazaar_action`).
- **Layer 3 (CLI Executes):** Bounded atomic transaction actuator (`AtomicAnchorActuator`) asserting window identity, pulsing input, and polling Layer 1 for verified `StateReceipt`.
- **Layer 4 (User Enables):** Hardware deadman switch (`GameInputLease`) and financial safety reserve floors.

---

## 2. Deliverables Summary

### Deliverable A: Layer 1 Sensory Payload (`ProjectMaelstrom/ProjectMaelstrom/Utilities/Circuit/CircuitSnapshot.cs`)
- Immutable, zero-allocation-friendly record:
  - `SchemaVersion: "circuit.v1"`
  - `TimestampUtcTicks: long`
  - `WorldBitsRaw: ulong`
  - `ActiveBitNames: IReadOnlyList<string>`
  - `Registers: CircuitRegisters(Gold, Energy, CandidateRowIndex, CandidateUnitPrice, CandidateQuantity)`
  - `AnchorConfidences: IReadOnlyDictionary<string, float>`
- **Invariant:** Serializes `WorldBits` as raw `ulong` alongside explicit `ActiveBitNames` array, eliminating the .NET 9 `System.Text.Json` `JsonStringEnumConverter` composite flags regression (`dotnet/runtime#114770`).

### Deliverable B: Layer 1 Fast Perceptual Adapter (`ProjectMaelstrom/ProjectMaelstrom/Utilities/Circuit/FastPerceptualAdapter.cs`)
- Implements both `IWorldState` and `ICircuitFeed`:
  - `Task<CircuitSnapshot> ReadCircuitSnapshotAsync(CancellationToken ct = default)`
- Backed by `IWorldState`, `_leaseProbe`, `_focusProbe`, and `_minGoldReserveFloor`.
- Perceptually evaluates `ui_anchors.v1.json`:
  - `native_idle_banner` $\rightarrow$ `WorldBits.NativeIdleBanner`
  - `bazaar_player_gold_label` $\rightarrow$ `WorldBits.BazaarOpen` & `WorldBits.GoldAboveFloor`
  - `bazaar_buy_more_button` $\rightarrow$ `WorldBits.BuyMoreButtonActive`
  - `bazaar_buy_confirm_dialog_button` $\rightarrow$ `WorldBits.ConfirmModalOpen`

### Deliverable C: Layer 3 Atomic Actuator (`ProjectMaelstrom/ProjectMaelstrom/Utilities/Circuit/AtomicAnchorActuator.cs` & `CircuitServer.cs`)
- `AtomicAnchorActuator.ActuateAsync`:
  1. Checks Layer 4 deadman switch (`actuator.HasActiveLease`). If false, immediately returns unverified receipt (`"LEASE_NOT_HELD"`) without pulsing input.
  2. Executes single bounded click via `actuator.ClickAnchorAsync(anchorId, ct)`.
  3. Polls Layer 1 (`world.WaitForReceiptAsync`) on `receiptAnchorId` up to `timeout`.
  4. Returns verified cryptographic `StateReceipt`.
- `CircuitServer.HandleSnapshotRequestAsync`:
  - Dispatches `"circuit-snapshot"` over named-pipe IPC and in-process hosts in sub-millisecond execution.

### Deliverable D: Layer 2 MCP Declarative Tools (`tools/maelstrom_mcp_server.py`)
- **`maelstrom_read_circuit_bus`**:
  - Queries Layer 1 named pipe (`call_deck("circuit-snapshot", ...)`) and returns `world_bits_raw`, `active_bit_names`, and `registers`.
- **`maelstrom_propose_anchor_actuation`**:
  - Validates `anchor_id` and `receipt_anchor_id` against `ui_anchors.v1.json`.
  - Asserts `LeaseActive` in the latest circuit snapshot.
  - Emits bounded `executive_intent` and exact `cli_invocation` string.
- **`maelstrom_propose_bazaar_action`**:
  - Declarative combinational ladder logic evaluating `CircuitSnapshot` against user policy (`target_item`, `max_unit_price`, `min_gold_reserve_floor`).

---

## 3. Test & Verification Evidence

### 1. .NET Unit Tests (`ProjectMaelstrom.Tests/`)
- **`UniversalBusTests.cs` (4/4 passed)**:
  - `CircuitFeed_Serializes64BitWorldBitsAndRegistersLosslessly`: Verified `WorldBitsRaw` (`ulong`), `Registers`, and `ActiveBitNames` roundtrip losslessly.
  - `FastPerceptualAdapter_DropsLeaseBit_WhenHumanTouchesInput`: Verified `WorldBits.LeaseActive` drops immediately when `_leaseProbe()` returns false.
  - `FastPerceptualAdapter_SetsGoldAboveFloorBit_OnlyWhenAboveReserve`: Verified `GoldAboveFloor` is set only when `Gold > MinGoldReserveFloor`.
  - `AtomicActuator_RefusesExecution_WhenLeaseBitIsLow`: Verified Layer 3 refuses execution without clicking if `HasActiveLease` is revoked.
- **`CircuitSystemTests.cs` (3/3 passed)**:
  - Verified `AnchorRegistry`, `WaitForReceiptAsync`, and full end-to-end `BazaarSniperCard` loop.
- **`BazaarSniperCardTests.cs` (13/13 passed)** & **`PerceptualGateTests.cs` (8/8 passed)**.
- **Total .NET Suite**: **28/28 passed in 72 ms**.

### 2. Python MCP Unit Tests (`tools/tests/test_maelstrom_mcp_server.py`)
- **28/28 passed in 0.017s**:
  - Verified `maelstrom_read_circuit_bus` bridge.
  - Verified `maelstrom_propose_anchor_actuation` target validation, receipt validation, and deadman switch trip (`HOLD_LEASE_INACTIVE`).
  - Verified `maelstrom_propose_bazaar_action` row selection and confirmation modal routing.

### 3. Static Code Quality & Invariant Gates
- `dotnet format whitespace --verify-no-changes`: Passed (0 changes).
- `dotnet format analyzers --verify-no-changes`: Passed (0 warnings).
- `python -m compileall -q`: Passed (0 syntax errors).
- `python -m black --check`: Passed (clean formatting).
- `python tools/check_project_board.py`: Passed (30 workstreams valid).
- `cargo test --workspace`: **All 76+ Rust tests passing cleanly** across 5 crates.
