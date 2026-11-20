# BAZAAR-SNIPER-CARD-01 — Bazaar Reagent/TC Sniper Card & Ports/Adapters Foundation

**Date**: 2026-11-15T22:20:00-05:00  
**Author**: Gemini (Local Operating Agent / Actualizer)  
**Status**: IMPLEMENTED & TESTED (13/13 PASSING OFFLINE UNIT TESTS)  
**Deliverables**:
- `ProjectMaelstrom/ProjectMaelstrom/Utilities/Contracts/NormalizedRect.cs`
- `ProjectMaelstrom/ProjectMaelstrom/Utilities/Contracts/OcrObservation.cs`
- `ProjectMaelstrom/ProjectMaelstrom/Utilities/Contracts/StateReceipt.cs`
- `ProjectMaelstrom/ProjectMaelstrom/Utilities/Contracts/BazaarRowObservation.cs`
- `ProjectMaelstrom/ProjectMaelstrom/Utilities/Contracts/IWorldState.cs`
- `ProjectMaelstrom/ProjectMaelstrom/Utilities/Contracts/IActuator.cs`
- `ProjectMaelstrom/ProjectMaelstrom/Resources/ui_anchors.v1.json`
- `ProjectMaelstrom/ProjectMaelstrom/Utilities/Cards/BazaarSniperCard.cs`
- `ProjectMaelstrom/ProjectMaelstrom.Tests/BazaarSniperCardTests.cs`

---

## 1. Architectural Context & Motivation

Legacy automation bots (`AutoWizard101`, `Deimos`, `wizSDK`) intertwined high-level decision logic with raw screen coordinates, unmanaged Win32 loops, and brittle memory offsets. When KingsIsle updated UI layouts, resolutions, or memory structures, the monolithic bots broke completely.

This implementation establishes the **Ports & Adapters (Hexagonal) Architecture** for Maelstrom Cards:
1. **Pure Domain Card (`BazaarSniperCard`)**: Contains invariant game theory, state machine transitions, financial floors, and priority queues. Has **zero** dependencies on Win32, DirectX, or pixel coordinates.
2. **Perception & Actuation Boundary (`IWorldState`, `IActuator`)**: Defines abstract contracts for reading anchor text, parsing dynamic table rows, polling for state receipts, checking input leases, and dispatching normalized clicks.
3. **Declarative Layout Manifest (`ui_anchors.v1.json`)**: Encapsulates volatile client coordinates as normalized viewport rectangles ($0.0 \dots 1.0$) with row strides and expected OCR tokens. Post-patch UI adjustments require editing JSON or calibrating anchors—never recompiling Cards.

---

## 2. Finite State Machine (FSM) Lifecycle

`BazaarSniperCard` implements an 8-state receipt-gated lifecycle:

```
[ VerifyShopOpen ] ─────── (Shop Open & Gold > Floor) ───────> [ RefreshListing ]
        │                                                               │
        │ (Unreadable / Floor Reached)                                  │ (Click Refresh & Table Receipt)
        v                                                               v
[ RecoveryAbort ] <─────── (Refresh Timeout / Abort) ───────── [ ScanRows ]
                                                                        │
                                                                        │ (Target Found)
                                                                        v
[ RefreshListing ] <── (Modal/Receipt Sniped) ── [ SelectTargetRow ]
        │                                                               │
        │                                                               │ (Click Row & Button Receipt)
        v                                                               v
[ VerifyGoldReceipt ] <── (Confirm Click) ─── [ ConfirmPurchase ] <── [ OpenBuyModal ]
        │
        ├── (Gold Strictly Debited) ──> [ VerifyShopOpen ] (Loop Ready)
        └── (Receipt Timeout) ────────> [ RefreshListing ] (Reset without Phantom Ledger Entry)
```

### Safety & Invariant Guarantees
- **Human Takeover First**: At the start of `StepAsync()`, `HasActiveLease` is verified. If the human touches the mouse or keyboard, the FSM instantly pauses in place with zero latency.
- **Contested Item Recovery**: If another player buys the item before our modal opens, the card safely reverts to `RefreshListing` rather than throwing an unhandled exception.
- **Strict Gold Debit Verification**: The post-purchase receipt requires `newGold < _goldBeforePurchase`. If the purchase failed or the item went out of stock, gold is not debited, preventing phantom inventory tracking.
- **Failsafe Native Idle Recovery**: Any unrecoverable error (e.g. unreadable gold balance, table refresh timeout) immediately halts the state machine into `RecoveryAbort`, ready for handoff to `PetDanceNativeIdle`.

---

## 3. Offline Verification Evidence

All 13 unit tests pass deterministically in **34 milliseconds** with zero live client dependencies:

```
Passed! - Failed: 0, Passed: 13, Skipped: 0, Total: 13, Duration: 34 ms - ProjectMaelstrom.Tests.dll (net9.0)
```

### Verified Test Cases
1. `Sniper_SuccessfulCycle_DebitsGoldAndReturnsToVerifyShop`: Verifies the complete 7-step purchase loop from open shop through gold debit.
2. `Sniper_HumanTakeover_PausesInPlaceWithoutStepping`: Asserts zero clicks and zero state changes while human holds input lease.
3. `Sniper_GoldFloorReached_AbortsToRecovery`: Asserts immediate abort when gold balance drops below configured minimum reserve.
4. `Sniper_GoldUnreadable_AbortsToRecovery`: Asserts abort when gold text is unparseable or OCR confidence is below threshold.
5. `Sniper_NoMatchingRow_LoopsToVerifyShop`: Asserts clean looping back to verify/refresh when no item matches target criteria or price ceiling.
6. `Sniper_ItemSnipedBeforeModal_RevertsToRefreshListing`: Verifies clean reset to refresh when another player snipes the item before the modal opens.
7. `Sniper_ItemSnipedBeforeConfirm_NoGoldDebit_RevertsToRefreshListing`: Verifies that an unconfirmed purchase without gold debit cleanly resets without recording phantom purchases.
8. `Sniper_TableRefreshTimeout_AbortsToRecovery`: Asserts abort when the Bazaar table fails to refresh within the bounded deadline.
9. `TryParseGold_HandlesCommasAndSymbols` (5 Theory Inlines): Verifies robust parsing across `"150,000"`, `"150.000 Gold"`, `"  50,420 "`, `"$999"`, and `"0"`.

### Code Quality & Compliance
- `dotnet format whitespace --verify-no-changes`: PASSED (0 changes).
- `dotnet format analyzers --verify-no-changes`: PASSED (0 warnings).
- Warnings as Errors (`-warnaserror`): PASSED.
