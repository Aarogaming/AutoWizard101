using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProjectMaelstrom.Utilities;
using Xunit;

namespace ProjectMaelstrom.Tests;

public class GenericMacroDeckTests
{
    private sealed record TestGameContext(
        int Health,
        int Mana,
        int Energy,
        bool InCombat,
        bool Disconnected,
        int ReagentsNear);

    private sealed class TestScanCard : IScanCard<TestGameContext>
    {
        private readonly Func<TestGameContext, bool> _precondition;
        private readonly Func<TestGameContext, Task<CardExecutionOutcome>> _action;

        public string Id { get; }
        public string Title { get; }
        public string Category { get; }
        public int Priority { get; }
        public int Cost { get; }
        public bool IsSideDeckInterrupt { get; }

        public TestScanCard(
            string id,
            string title,
            int priority,
            int cost,
            Func<TestGameContext, bool> precondition,
            Func<TestGameContext, Task<CardExecutionOutcome>>? action = null,
            bool isSideDeckInterrupt = false,
            string category = "General")
        {
            Id = id;
            Title = title;
            Priority = priority;
            Cost = cost;
            _precondition = precondition;
            _action = action ?? (_ => Task.FromResult(new CardExecutionOutcome(true, $"{title} executed successfully.")));
            IsSideDeckInterrupt = isSideDeckInterrupt;
            Category = category;
        }

        public bool EvaluatePreconditions(TestGameContext context) => _precondition(context);

        public Task<CardExecutionOutcome> ExecuteAsync(TestGameContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _action(context);
        }
    }

    private sealed class DisconnectWatchdogShield : IShieldWatchdog<TestGameContext>
    {
        public string Id => "shield.disconnect-guard";
        public string Title => "Disconnect & Crash Guard";

        public bool CheckInvariant(TestGameContext context, out string? breachReason)
        {
            if (context.Disconnected)
            {
                breachReason = "Connection to server was lost.";
                return false;
            }
            breachReason = null;
            return true;
        }
    }

    [Fact]
    public async Task SchedulerDeck_ExecutesBigLoop_HoldsInsufficientFunds_AndReshuffles()
    {
        var deck = new GenericMacroDeck<TestGameContext>(deckName: "Big Loop Scheduler", handCapacity: 3);

        // Add 3 high-level jobs to the scheduler deck
        // 1. Garden Harvest (Cost 20 energy)
        var gardenCard = new TestScanCard("job.garden", "Garden Harvest", priority: 10, cost: 20,
            ctx => ctx.Energy >= 20 && !ctx.InCombat);

        // 2. Pet Training (Cost 40 energy)
        var petCard = new TestScanCard("job.pet", "Pet Training", priority: 20, cost: 40,
            ctx => ctx.Energy >= 40 && !ctx.InCombat);

        // 3. Reagent Gathering (Cost 0 energy, needs nearby reagents)
        var reagentCard = new TestScanCard("job.reagent", "Reagent Gathering", priority: 30, cost: 0,
            ctx => ctx.ReagentsNear > 0 && !ctx.InCombat);

        deck.AddMainCard(gardenCard);
        deck.AddMainCard(petCard);
        deck.AddMainCard(reagentCard);

        Assert.Equal(3, deck.DrawPile.Count);

        // Context: Energy = 25 (enough for Garden, but NOT enough for Pet Training; 2 reagents nearby)
        var context = new TestGameContext(
            Health: 1000, Mana: 500, Energy: 25,
            InCombat: false, Disconnected: false, ReagentsNear: 2);

        // Cycle 1: GardenCard has higher priority (10) than ReagentCard (30), and meets energy >= 20
        var res1 = await deck.ScanCycleAsync(context, CancellationToken.None);
        Assert.Equal(ScanPhase.Execution, res1.Phase);
        Assert.NotNull(res1.ExecutedCard);
        Assert.Equal("job.garden", res1.ExecutedCard.Id);
        Assert.Single(deck.Graveyard); // Garden moved to graveyard
        Assert.Equal(2, deck.Hand.Count); // Pet and Reagent in hand

        // Cycle 2: PetCard requires 40 energy (we only have 25 -> Insufficient funds).
        // Ladder continuity fails for PetCard, so ReagentCard (Priority 30) executes!
        var res2 = await deck.ScanCycleAsync(context, CancellationToken.None);
        Assert.Equal(ScanPhase.Execution, res2.Phase);
        Assert.NotNull(res2.ExecutedCard);
        Assert.Equal("job.reagent", res2.ExecutedCard.Id);
        Assert.Equal(2, deck.Graveyard.Count); // Garden + Reagent in graveyard
        Assert.Single(deck.Hand); // PetCard still held in hand!

        // Cycle 3: Only PetCard in hand. Energy is still 25. PetCard continuity fails.
        // Engine enters IdleHold state without crashing or misfiring.
        var res3 = await deck.ScanCycleAsync(context, CancellationToken.None);
        Assert.Equal(ScanPhase.IdleHold, res3.Phase);
        Assert.Null(res3.ExecutedCard);

        // Now simulate Energy recharging (or drinking an energy elixir) -> Energy = 50!
        var rechargedContext = context with { Energy = 50 };

        // Cycle 4: PetCard's preconditions are now satisfied! It executes and goes to graveyard.
        var res4 = await deck.ScanCycleAsync(rechargedContext, CancellationToken.None);
        Assert.Equal(ScanPhase.Execution, res4.Phase);
        Assert.Equal("job.pet", res4.ExecutedCard!.Id);
        Assert.Empty(deck.Hand);
        Assert.Equal(3, deck.Graveyard.Count);

        // Cycle 5: Draw pile and Hand are both empty -> triggers Reshuffle of all 3 jobs!
        var res5 = await deck.ScanCycleAsync(rechargedContext, CancellationToken.None);
        Assert.Equal(ScanPhase.Execution, res5.Phase);
        Assert.Equal("job.garden", res5.ExecutedCard!.Id);
        Assert.Equal(2, deck.Hand.Count);
        Assert.Empty(deck.DrawPile);
    }

    [Fact]
    public async Task SideDeck_TreasureCard_PreemptsMainDeckHandImmediately()
    {
        var deck = new GenericMacroDeck<TestGameContext>(deckName: "TC Test Deck", handCapacity: 2);

        var normalWalk = new TestScanCard("job.walk", "Quest Walk", priority: 50, cost: 0, ctx => !ctx.InCombat);
        deck.AddMainCard(normalWalk);

        // Side-Deck Emergency Potion TC
        var potionTc = new TestScanCard("tc.potion", "Emergency Health Potion", priority: 1, cost: 0,
            ctx => ctx.Health < 300, isSideDeckInterrupt: true);
        deck.AddSideDeckCard(potionTc);

        // When health is fine (1000), normal card executes
        var healthyContext = new TestGameContext(Health: 1000, Mana: 200, Energy: 10, InCombat: false, Disconnected: false, ReagentsNear: 0);
        var res1 = await deck.ScanCycleAsync(healthyContext, CancellationToken.None);
        Assert.Equal(ScanPhase.Execution, res1.Phase);
        Assert.Equal("job.walk", res1.ExecutedCard!.Id);

        // When health drops critically (200), TC preempts the entire hand!
        var criticalContext = healthyContext with { Health = 200 };
        var res2 = await deck.ScanCycleAsync(criticalContext, CancellationToken.None);
        Assert.Equal(ScanPhase.SideDeckInterrupt, res2.Phase);
        Assert.Equal("tc.potion", res2.ExecutedCard!.Id);
    }

    [Fact]
    public async Task WatchdogShield_AbortsScanCycle_WhenInvariantBreached()
    {
        var deck = new GenericMacroDeck<TestGameContext>();
        deck.AddShieldWatchdog(new DisconnectWatchdogShield());

        var activeCard = new TestScanCard("job.any", "Any Action", priority: 1, cost: 0, ctx => true);
        deck.AddMainCard(activeCard);

        // Healthy context: shield passes
        var okContext = new TestGameContext(Health: 1000, Mana: 200, Energy: 10, InCombat: false, Disconnected: false, ReagentsNear: 0);
        var res1 = await deck.ScanCycleAsync(okContext, CancellationToken.None);
        Assert.Equal(ScanPhase.Execution, res1.Phase);

        // Disconnected context: shield breaches immediately, preventing any action dispatch
        var disconnectedContext = okContext with { Disconnected = true };
        var res2 = await deck.ScanCycleAsync(disconnectedContext, CancellationToken.None);
        Assert.Equal(ScanPhase.ShieldCheck, res2.Phase);
        Assert.NotNull(res2.BreachedShield);
        Assert.Equal("shield.disconnect-guard", res2.BreachedShield.Id);
        Assert.Null(res2.ExecutedCard);
        Assert.Contains("Connection to server was lost", res2.Summary);
    }

    [Fact]
    public void DiscardHandCard_RemovesCardFromHandToGraveyard()
    {
        var deck = new GenericMacroDeck<TestGameContext>(handCapacity: 2);
        var card1 = new TestScanCard("card.1", "Card 1", 10, 0, _ => true);
        var card2 = new TestScanCard("card.2", "Card 2", 20, 0, _ => true);

        deck.AddMainCard(card1);
        deck.AddMainCard(card2);
        deck.RefillHand(out _);

        Assert.Equal(2, deck.Hand.Count);
        Assert.Empty(deck.Graveyard);

        bool discarded = deck.DiscardHandCard("card.1");
        Assert.True(discarded);
        Assert.Single(deck.Hand);
        Assert.Equal("card.2", deck.Hand[0].Id);
        Assert.Single(deck.Graveyard);
        Assert.Equal("card.1", deck.Graveyard[0].Id);
    }
}
