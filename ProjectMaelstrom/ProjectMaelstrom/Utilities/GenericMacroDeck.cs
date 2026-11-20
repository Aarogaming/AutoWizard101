using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProjectMaelstrom.Utilities;

/// <summary>
/// Status outcome of a single card execution step.
/// </summary>
public record CardExecutionOutcome(
    bool Success,
    string Message,
    bool YieldedToIdle = false,
    bool RequeueImmediately = false);

/// <summary>
/// Phases of the continuous PLC scan cycle for the macro deck.
/// </summary>
public enum ScanPhase
{
    ShieldCheck,
    SideDeckInterrupt,
    HandEvaluation,
    Execution,
    GraveyardReshuffle,
    IdleHold
}

/// <summary>
/// A game-agnostic card (ladder rung) evaluated within a macro deck scan cycle.
/// Each card encapsulates its own prerequisites (continuity) and kinetic reflex action.
/// </summary>
public interface IScanCard<in TContext>
{
    string Id { get; }
    string Title { get; }
    string Category { get; }
    int Priority { get; } // Lower number = evaluated first
    int Cost { get; } // Generalized cost (pips, mana, stamina, etc.)
    bool IsSideDeckInterrupt { get; } // If true, evaluated as a high-priority interrupt (Treasure Card)

    /// <summary>
    /// Evaluates if the environment state satisfies all preconditions (contacts on ladder rung).
    /// </summary>
    bool EvaluatePreconditions(TContext context);

    /// <summary>
    /// Executes the kinetic reflex action when preconditions are met.
    /// </summary>
    Task<CardExecutionOutcome> ExecuteAsync(TContext context, CancellationToken cancellationToken);
}

/// <summary>
/// A continuous passive watchdog (Ward/Shield) that verifies invariant system health during every scan cycle.
/// If any active shield breaches, the engine aborts the active plan and handles recovery.
/// </summary>
public interface IShieldWatchdog<in TContext>
{
    string Id { get; }
    string Title { get; }

    /// <summary>
    /// Checks the invariant condition. Returns true if healthy; returns false if breached.
    /// </summary>
    bool CheckInvariant(TContext context, out string? breachReason);
}

/// <summary>
/// Detailed receipt of a single PLC scan cycle execution.
/// </summary>
public record DeckScanCycleResult<TContext>(
    ScanPhase Phase,
    IScanCard<TContext>? ExecutedCard,
    IShieldWatchdog<TContext>? BreachedShield,
    IReadOnlyList<IScanCard<TContext>> CurrentHand,
    int RemainingDrawPileCount,
    int GraveyardCount,
    string Summary);

/// <summary>
/// Game-Agnostic Continuous PLC Scan Cycle Macro Deck Engine.
/// Implements a dynamic, condition-based execution loop (Draw -> Shield Check -> Side-Deck Check -> Hand Scan -> Execute -> Graveyard -> Reshuffle).
/// Completely eliminates macro desync by holding unfulfilled cards in hand until environmental conditions (pips) are satisfied.
/// </summary>
public class GenericMacroDeck<TContext>
{
    private readonly List<IScanCard<TContext>> _drawPile = [];
    private readonly List<IScanCard<TContext>> _hand = [];
    private readonly List<IScanCard<TContext>> _graveyard = [];
    private readonly List<IScanCard<TContext>> _sideDeck = [];
    private readonly List<IShieldWatchdog<TContext>> _shields = [];
    private readonly IScanCard<TContext>? _defaultIdleCard;

    public string DeckName { get; }
    public int HandCapacity { get; set; } = 5;

    public IReadOnlyList<IScanCard<TContext>> DrawPile => _drawPile;
    public IReadOnlyList<IScanCard<TContext>> Hand => _hand;
    public IReadOnlyList<IScanCard<TContext>> Graveyard => _graveyard;
    public IReadOnlyList<IScanCard<TContext>> SideDeck => _sideDeck;
    public IReadOnlyList<IShieldWatchdog<TContext>> Shields => _shields;
    public IScanCard<TContext>? DefaultIdleCard => _defaultIdleCard;

    public GenericMacroDeck(
        string deckName = "Generic Deck",
        int handCapacity = 5,
        IScanCard<TContext>? defaultIdleCard = null)
    {
        DeckName = deckName;
        HandCapacity = Math.Max(1, handCapacity);
        _defaultIdleCard = defaultIdleCard;
    }

    /// <summary>
    /// Adds a card to the Main Draw Pile.
    /// </summary>
    public void AddMainCard(IScanCard<TContext> card)
    {
        ArgumentNullException.ThrowIfNull(card);
        _drawPile.Add(card);
    }

    /// <summary>
    /// Adds an asynchronous interrupt card (Treasure Card) to the Side Deck.
    /// </summary>
    public void AddSideDeckCard(IScanCard<TContext> card)
    {
        ArgumentNullException.ThrowIfNull(card);
        _sideDeck.Add(card);
        _sideDeck.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }

    /// <summary>
    /// Adds a passive watchdog shield to monitor system invariants.
    /// </summary>
    public void AddShieldWatchdog(IShieldWatchdog<TContext> shield)
    {
        ArgumentNullException.ThrowIfNull(shield);
        _shields.Add(shield);
    }

    /// <summary>
    /// Discards a card currently in hand to the graveyard (e.g. to unjam hand of irrelevant cards).
    /// </summary>
    public bool DiscardHandCard(string cardId)
    {
        int index = _hand.FindIndex(c => string.Equals(c.Id, cardId, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            var card = _hand[index];
            _hand.RemoveAt(index);
            _graveyard.Add(card);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Refills the hand from the draw pile up to HandCapacity.
    /// When both the hand and draw pile are exhausted, automatically triggers a Reshuffle from the graveyard.
    /// </summary>
    public bool RefillHand(out bool reshuffled)
    {
        reshuffled = false;

        // When the entire deck cycle has been completed, reshuffle graveyard into draw pile
        if (_hand.Count == 0 && _drawPile.Count == 0 && _graveyard.Count > 0)
        {
            ReshuffleGraveyard();
            reshuffled = true;
        }

        while (_hand.Count < HandCapacity && _drawPile.Count > 0)
        {
            var drawn = _drawPile[0];
            _drawPile.RemoveAt(0);
            _hand.Add(drawn);
        }

        return _hand.Count > 0;
    }

    /// <summary>
    /// Recycles all cards in the graveyard back into the draw pile.
    /// </summary>
    public void ReshuffleGraveyard()
    {
        if (_graveyard.Count == 0) return;
        _drawPile.AddRange(_graveyard);
        _graveyard.Clear();
    }

    /// <summary>
    /// Executes one continuous PLC scan cycle:
    /// 1. Shield Check: Verify all active watchdog invariants.
    /// 2. Side-Deck Interrupt Check: Evaluate Treasure Card triggers.
    /// 3. Hand Refill: Ensure hand is populated up to HandCapacity (auto-reshuffling if needed).
    /// 4. Hand Evaluation: Check continuity (preconditions) for all cards in hand.
    /// 5. Execution: Dispatch highest priority valid card, then move it to the Graveyard.
    /// 6. Fallback: If no card triggers, hold state or dispatch DefaultIdleCard.
    /// </summary>
    public async Task<DeckScanCycleResult<TContext>> ScanCycleAsync(
        TContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 1. Invariant Shield / Watchdog Check
        foreach (var shield in _shields)
        {
            if (!shield.CheckInvariant(context, out var breachReason))
            {
                return new DeckScanCycleResult<TContext>(
                    ScanPhase.ShieldCheck,
                    null,
                    shield,
                    _hand.AsReadOnly(),
                    _drawPile.Count,
                    _graveyard.Count,
                    $"Shield breached: {shield.Title}. Reason: {breachReason ?? "Unknown breach"}");
            }
        }

        // 2. Side-Deck (Treasure Card) High-Priority Interrupt Evaluation
        foreach (var tc in _sideDeck)
        {
            if (tc.EvaluatePreconditions(context))
            {
                var outcome = await tc.ExecuteAsync(context, cancellationToken);
                return new DeckScanCycleResult<TContext>(
                    ScanPhase.SideDeckInterrupt,
                    tc,
                    null,
                    _hand.AsReadOnly(),
                    _drawPile.Count,
                    _graveyard.Count,
                    $"Side-deck interrupt executed: {tc.Title}. Outcome: {outcome.Message}");
            }
        }

        // 3. Hand Refill & Graveyard Reshuffle
        bool reshuffledOccurred;
        RefillHand(out reshuffledOccurred);

        // 4. Hand Logic Evaluation (check rung continuity)
        var playableCandidates = _hand
            .Where(card => card.EvaluatePreconditions(context))
            .OrderBy(card => card.Priority)
            .ToList();

        if (playableCandidates.Count > 0)
        {
            var bestCard = playableCandidates[0];
            var outcome = await bestCard.ExecuteAsync(context, cancellationToken);

            // Move played card from Hand to Graveyard
            _hand.Remove(bestCard);
            if (!outcome.RequeueImmediately)
            {
                _graveyard.Add(bestCard);
            }
            else
            {
                _hand.Add(bestCard);
            }

            return new DeckScanCycleResult<TContext>(
                ScanPhase.Execution,
                bestCard,
                null,
                _hand.AsReadOnly(),
                _drawPile.Count,
                _graveyard.Count,
                $"Card executed: {bestCard.Title} (Priority {bestCard.Priority}). Status: {outcome.Message}");
        }

        // 5. Idle / Held State (Preconditions not met; waiting for screen/game state to catch up)
        if (_defaultIdleCard != null && _defaultIdleCard.EvaluatePreconditions(context))
        {
            var idleOutcome = await _defaultIdleCard.ExecuteAsync(context, cancellationToken);
            return new DeckScanCycleResult<TContext>(
                ScanPhase.IdleHold,
                _defaultIdleCard,
                null,
                _hand.AsReadOnly(),
                _drawPile.Count,
                _graveyard.Count,
                $"Held state: no hand cards satisfied continuity. Dispatched idle fallback: {idleOutcome.Message}");
        }

        return new DeckScanCycleResult<TContext>(
            ScanPhase.IdleHold,
            null,
            null,
            _hand.AsReadOnly(),
            _drawPile.Count,
            _graveyard.Count,
            "Held state: waiting for environmental conditions/pips to be satisfied.");
    }
}
