namespace ProjectMaelstrom.Utilities.Circuit;

/// <summary>
/// 64-bit Input Status Word representing the entire discrete perception and lease state.
/// Updated on every scan cycle without heap allocations.
/// </summary>
[Flags]
public enum WorldBits : ulong
{
    None                = 0,
    LeaseActive         = 1UL << 0,  // Input: Human NOT touching mouse/keyboard (GameInputLease valid)
    ClientFocused       = 1UL << 1,  // Input: Target game window is currently foreground
    NativeIdleBanner    = 1UL << 2,  // Comparator: "YOU ARE IDLE" banner dHash match
    BazaarOpen          = 1UL << 3,  // Comparator: Bazaar window header active
    RefreshReady        = 1UL << 4,  // Comparator: Refresh button enabled (not grayed out)
    CandidateRowFound   = 1UL << 5,  // Comparator: Selected row price <= MaxPrice && Qty > 0
    BuyMoreButtonActive = 1UL << 6,  // Comparator: "BUY MORE" button lit
    ConfirmModalOpen    = 1UL << 7,  // Comparator: Purchase confirmation dialog present
    GoldAboveFloor      = 1UL << 8,  // Comparator: Current gold > MinGoldReserveFloor
    GoldDebitedEdge     = 1UL << 9,  // Latch / Edge: GoldRegister < PreviousGoldRegister
    SigilPromptActive   = 1UL << 10, // Comparator: Press X to enter sigil prompt detected
    LoadingScreenActive = 1UL << 11, // Comparator: Loading screen artwork active
    DanceRoundActive    = 1UL << 12, // Comparator: Pet Dance arena grid / arrow sequence active
    CombatRingActive    = 1UL << 13, // Comparator: Player in active combat ring
    DialogueModalActive = 1UL << 14  // Comparator: NPC quest dialogue box open
}
