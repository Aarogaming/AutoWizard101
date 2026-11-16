# NATIVE-COMPARATOR-01 — Digital Circuit Scan-Cycle Primitives & Perceptual Comparators

**Date**: 2026-11-15T22:24:00-05:00  
**Author**: Gemini (Local Operating Agent / Actualizer)  
**Status**: IMPLEMENTED & TESTED (8/8 PASSING UNIT TESTS; 21/21 COMBINED PASSING SUITE)  
**Deliverables**:
- `ProjectMaelstrom/ProjectMaelstrom/Utilities/Circuit/WorldBits.cs`
- `ProjectMaelstrom/ProjectMaelstrom/Utilities/Circuit/PerceptualGate.cs`
- `ProjectMaelstrom/ProjectMaelstrom.Tests/PerceptualGateTests.cs`

---

## 1. Architectural Motivation & Paradigm Shift

In legacy automation, evaluating whether a button was active or a sigil prompt appeared required invoking heavy Python scripts, spawning subprocesses, running OpenCV template matching, and deserializing JSON outputs. This introduced 30–50 ms of latency, GIL contention, IPC overhead, and memory churn.

This work package replaces that pipeline with a **Synchronous Digital Logic Circuit / PLC Scan Cycle** mental model:
- **Input Latch**: Samples frame pixels directly into a stack-allocated buffer.
- **Signal Conditioning**: Reduces Regions of Interest (ROIs) to a 64-bit Perceptual Difference Hash (`dHash`) or SIMD Color Density window comparator.
- **64-bit Input Status Word (`WorldBits`)**: Packs the entire world perception, window focus, and human lease flags into a single `ulong` without heap allocation.
- **Combinational Logic Gates (`EvaluateRung`)**: Evaluates Card state transition rungs using bitwise AND/OR/NOT operations in ~2 CPU cycles.

---

## 2. Component Specifications

### A. 64-Bit Status Word (`WorldBits.cs`)
```csharp
[Flags]
public enum WorldBits : ulong
{
    None                = 0,
    LeaseActive         = 1UL << 0,  // Input: Human NOT touching mouse/keyboard (GameInputLease valid)
    ClientFocused       = 1UL << 1,  // Input: Target game window is currently foreground
    NativeIdleBanner    = 1UL << 2,  // Comparator: "YOU ARE IDLE" banner dHash match
    BazaarOpen          = 1UL << 3,  // Comparator: Bazaar window header active
    RefreshReady        = 1UL << 4,  // Comparator: Refresh button enabled
    CandidateRowFound   = 1UL << 5,  // Comparator: Selected row price <= MaxPrice && Qty > 0
    BuyMoreButtonActive = 1UL << 6,  // Comparator: "BUY MORE" button lit
    ConfirmModalOpen    = 1UL << 7,  // Comparator: Purchase confirmation dialog present
    GoldAboveFloor      = 1UL << 8,  // Comparator: Current gold > MinGoldReserveFloor
    GoldDebitedEdge     = 1UL << 9,  // Latch: GoldRegister < PreviousGoldRegister
    SigilPromptActive   = 1UL << 10, // Comparator: Press X to enter sigil prompt detected
    LoadingScreenActive = 1UL << 11, // Comparator: Loading screen artwork active
    DanceRoundActive    = 1UL << 12, // Comparator: Pet Dance arena grid / arrow sequence active
    CombatRingActive    = 1UL << 13, // Comparator: Player in active combat ring
    DialogueModalActive = 1UL << 14  // Comparator: NPC quest dialogue box open
}
```

### B. High-Speed Logic Gates (`PerceptualGate.cs`)
1. **Hardware XNOR + PopCount Comparator**:
   ```csharp
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static bool CompareHash(ulong observedHash, ulong targetHash, int maxHammingBits = 6) =>
       BitOperations.PopCount(observedHash ^ targetHash) <= maxHammingBits;
   ```
2. **Combinational Rung Evaluator**:
   ```csharp
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static bool EvaluateRung(WorldBits currentBus, WorldBits requiredHigh, WorldBits requiredLow = WorldBits.None) =>
       (currentBus & requiredHigh) == requiredHigh && (currentBus & requiredLow) == WorldBits.None;
   ```
3. **Zero-Allocation Stack dHash Generator**:
   Downsamples an arbitrary $W \times H$ pixel buffer into a $9 \times 8$ grayscale grid on the stack (`stackalloc byte[72]`) using fast integer luminance `(77*R + 150*G + 29*B) >> 8`, then compares adjacent horizontal columns to generate a standard 64-bit difference hash.
4. **Color Density Window Comparator**:
   Measures pixel density within specified BGR color thresholds for sigil, button, and loading art recognition.

---

## 3. Test Verification Evidence

Executed via .NET 9 Release test runner:
```powershell
Passed!  - Failed: 0, Passed: 8, Skipped: 0, Total: 8, Duration: 14 ms - ProjectMaelstrom.Tests.dll (net9.0)
```

Combined with `BazaarSniperCardTests`:
```powershell
Passed!  - Failed: 0, Passed: 21, Skipped: 0, Total: 21, Duration: 31 ms - ProjectMaelstrom.Tests.dll (net9.0)
```

| Test Case | Scenario Tested | Outcome |
| :--- | :--- | :--- |
| `CompareHash_ExactMatch_ReturnsTrueWithZeroDistance` | Identical 64-bit words; asserts Hamming distance = 0 | **PASSED** |
| `CompareHash_WithinTolerance_ReturnsTrue` | 3 bits flipped; asserts distance = 3 $\le 6$ | **PASSED** |
| `CompareHash_AboveTolerance_ReturnsFalse` | 10 bits set; asserts distance = 10 $> 6$ | **PASSED** |
| `EvaluateRung_CombinationalTruthTable` | Evaluates active, missing, and inhibited bits in status word | **PASSED** |
| `ComputeDHash64_DecreasingGradient_ProducesAllOnes` | Left > right across all 8 rows; asserts `ulong.MaxValue` | **PASSED** |
| `ComputeDHash64_IncreasingGradient_ProducesAllZeros` | Left $\le$ right across all 8 rows; asserts `0UL` | **PASSED** |
| `ComputeDHashFromPixelBuffer_DeterministicAndNoiseTolerant` | 90x80 synthetic pixel buffer; asserts 1-pixel noise distance $\le 2$ | **PASSED** |
| `ComputeColorDensity_CalculatesExactFraction` | 10x10 buffer with 40 red and 60 black pixels; asserts 0.40f density | **PASSED** |

### Code Quality & Compliance
- `dotnet format whitespace --verify-no-changes`: **PASSED** (0 changes).
- `dotnet format analyzers --verify-no-changes`: **PASSED** (0 warnings).
- Warnings as Errors (`-warnaserror`): **PASSED**.
