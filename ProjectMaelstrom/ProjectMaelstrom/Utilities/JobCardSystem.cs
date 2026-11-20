using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProjectMaelstrom.Utilities;

/// <summary>
/// Execution status of an individual Job Card.
/// </summary>
public enum JobCardStatus
{
    NotTriggered,
    Running,
    Completed,
    Failed,
    YieldedToIdle,
    Unsupported
}

/// <summary>
/// Context passed into Job Cards containing live wizard telemetry and environment handles.
/// </summary>
public record JobExecutionContext(
    WizardSessionSnapshot Snapshot,
    object? Controller = null,
    string? ActiveMacroId = null);

/// <summary>
/// A modular, bounded "Job Card" in the player's routine deck.
/// Each card defines its own preconditions, execution block, and timeout limits.
/// </summary>
public interface IJobCard
{
    string Id { get; }
    string Title { get; }
    string Category { get; }
    int Priority { get; } // Lower number = higher priority evaluation

    /// <summary>
    /// Checks whether this card's conditions are currently met to trigger execution.
    /// </summary>
    bool ShouldTrigger(JobExecutionContext context);

    /// <summary>
    /// Executes the card's bounded operation asynchronously.
    /// </summary>
    Task<JobCardStatus> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Execution mode for modular versioned Job Cards.
/// </summary>
public enum CardExecutionMode
{
    OneShot,       // Single rising-edge trigger; enters cooldown upon completion
    ContainedLoop, // Bounded autonomous sub-cycle until terminal condition is met
    Continuous     // Continues executing between transitions / holds state
}

/// <summary>
/// Versioned Modular Job Card Interface (V2).
/// Extends IJobCard with execution modes, explicit terminal conditions, and cooldown controls,
/// preserving 100% backward compatibility with all existing V1 cards.
/// </summary>
public interface IJobCardV2 : IJobCard
{
    CardExecutionMode Mode { get; }

    /// <summary>
    /// For Contained Loops: evaluates whether the sub-cycle has achieved its terminal goal.
    /// </summary>
    bool IsTerminalConditionMet(JobExecutionContext context) => true;

    /// <summary>
    /// Cooldown duration in milliseconds after a OneShot card executes.
    /// </summary>
    int CooldownMs => 0;
}

/// <summary>
/// Legacy priority proposal collection; not a runtime controller.
/// Retained for offline compatibility. Execution is unsupported; use JobDeckService.
/// Drawing the Idle proposal does not send input or guarantee login persistence.
/// </summary>
public sealed class RoutineDeck
{
    private readonly List<IJobCard> _cards = [];
    private readonly AutoIdleJobCard _defaultIdleCard;

    public string DeckName { get; }
    public IReadOnlyList<IJobCard> Cards => _cards;
    public IJobCard DefaultIdleCard => _defaultIdleCard;

    public RoutineDeck(string deckName = "Default Deck", AutoIdleJobCard? defaultIdleCard = null)
    {
        DeckName = deckName;
        _defaultIdleCard = defaultIdleCard ?? new AutoIdleJobCard();
    }

    /// <summary>
    /// Stacks a new Job Card into the Routine Deck and orders the deck by priority.
    /// </summary>
    public void AddCard(IJobCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        _cards.Add(card);
        _cards.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }

    /// <summary>
    /// Removes a Job Card from the deck by its ID.
    /// </summary>
    public bool RemoveCard(string cardId)
    {
        int index = _cards.FindIndex(c => string.Equals(c.Id, cardId, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _cards.RemoveAt(index);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Selects the highest-priority Job Card whose trigger conditions are satisfied.
    /// Defaults to the Auto-Idle card if no other card triggers.
    /// </summary>
    public IJobCard DrawNextCard(JobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var card in _cards)
        {
            if (card.ShouldTrigger(context))
            {
                return card;
            }
        }

        return _defaultIdleCard;
    }

    /// <summary>
    /// Executes a single step of the continuous deck loop.
    /// </summary>
    public async Task<JobCardStatus> ExecuteStepAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        var card = DrawNextCard(context);
        return await card.ExecuteAsync(context, cancellationToken);
    }
}
