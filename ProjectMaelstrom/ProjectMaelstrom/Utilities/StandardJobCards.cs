using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProjectMaelstrom.Utilities;

/// <summary>
/// Default Fallback Card: Dispatches non-invasive spin/step pulses to keep the game alive
/// when no other job card in the deck is actively executing.
/// </summary>
public sealed class AutoIdleJobCard : IJobCard
{
    public string Id => "job.auto-idle";
    public string Title => "Anti-Timeout Auto-Idle";
    public string Category => "System";
    public int Priority => 999; // Lowest priority (evaluated last)

    public bool ShouldTrigger(JobExecutionContext context) => true; // Always valid as fallback

    public Task<JobCardStatus> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Legacy proposals cannot dispatch or claim an outcome. Use the shared JobDeckService registry.
        return Task.FromResult(JobCardStatus.Unsupported);
    }
}

/// <summary>
/// Health & Potion Guard Card: Triggers when health or mana drops below safety thresholds.
/// If potions are empty, routes to Fairgrounds for Potion Motion refill.
/// </summary>
public sealed class HealthPotionGuardCard : IJobCard
{
    public string Id => "job.health-potion-guard";
    public string Title => "Health & Potion Guard";
    public string Category => "Safety";
    public int Priority => 10; // High priority: evaluated before combat or questing

    public double HealthThreshold { get; init; } = 0.35;
    public double ManaThreshold { get; init; } = 0.20;

    public bool ShouldTrigger(JobExecutionContext context)
    {
        return !context.Snapshot.InCombat &&
               (context.Snapshot.HealthRatio < HealthThreshold || context.Snapshot.ManaRatio < ManaThreshold);
    }

    public Task<JobCardStatus> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Legacy proposals cannot dispatch or claim an outcome. Use the shared JobDeckService registry.
        return Task.FromResult(JobCardStatus.Unsupported);
    }
}

/// <summary>
/// Inventory / Backpack Capacity Card: Triggers when backpack reaches maximum capacity (80 or 150).
/// Pauses current loop and routes to Olde Town Bazaar to sell surplus gear.
/// </summary>
public sealed class BackpackFullBazaarCard : IJobCard
{
    public string Id => "job.backpack-full-bazaar";
    public string Title => "Bazaar Inventory Dump";
    public string Category => "Trading";
    public int Priority => 20;

    public bool ShouldTrigger(JobExecutionContext context)
    {
        return !context.Snapshot.InCombat &&
               context.Snapshot.BackpackCount >= context.Snapshot.BackpackCapacity;
    }

    public Task<JobCardStatus> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Legacy proposals cannot dispatch or claim an outcome. Use the shared JobDeckService registry.
        return Task.FromResult(JobCardStatus.Unsupported);
    }
}

/// <summary>
/// Pet Training Dance Card: Triggers when player has available Energy and is ready to train.
/// Runs Pet Dance game in Pet Pavilion.
/// </summary>
public sealed class PetTrainingDanceCard : IJobCard
{
    public string Id => "job.pet-training-dance";
    public string Title => "Pet Pavilion Dance Training";
    public string Category => "Pets";
    public int Priority => 30;

    public int MinEnergyRequired { get; init; } = 10;

    public bool ShouldTrigger(JobExecutionContext context)
    {
        return !context.Snapshot.InCombat &&
               context.Snapshot.EnergyAvailable >= MinEnergyRequired;
    }

    public Task<JobCardStatus> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Legacy proposals cannot dispatch or claim an outcome. Use the shared JobDeckService registry.
        return Task.FromResult(JobCardStatus.Unsupported);
    }
}

/// <summary>
/// Combat & Boss Farming Card: Triggers whenever the wizard enters battle circles.
/// Evaluates full-depth MCTS forward rollouts and generates deterministic click sequences using CombatGeometry.
/// </summary>
public sealed class ActiveCombatCard : IJobCard
{
    private readonly CombatMctsSimulator _simulator = new();

    public string Id => "job.active-combat";
    public string Title => "Combat Turn Execution";
    public string Category => "Combat";
    public int Priority => 1; // Highest priority: always runs while in battle

    public CombatMctsSimulator Simulator => _simulator;

    public bool ShouldTrigger(JobExecutionContext context) => context.Snapshot.InCombat;

    /// <summary>
    /// Plans the optimal turn action using MCTS forward rollouts.
    /// </summary>
    public SimulationTurnResult PlanTurn(
        CombatantState player,
        System.Collections.Generic.IReadOnlyList<CombatantState> enemies,
        System.Collections.Generic.IReadOnlyList<CombatCardState> hand,
        System.Collections.Generic.IReadOnlyList<CombatantState>? allies = null)
    {
        return _simulator.SelectBestAction(player, enemies, hand, allies);
    }

    /// <summary>
    /// Resolves a simulation decision into window-relative mouse click actions.
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<CombatClickAction> ResolveTurnClicks(
        SimulationTurnResult decision,
        int totalCardsInHand = 7)
    {
        return _simulator.ResolveClickSequence(decision, totalCardsInHand);
    }

    public Task<JobCardStatus> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Legacy proposals cannot dispatch or claim an outcome. Use the shared JobDeckService registry.
        return Task.FromResult(JobCardStatus.Unsupported);
    }
}

/// <summary>
/// Overworld Opportunistic Harvest Card: Triggers while traveling/questing when a nearby reagent node
/// (Black Lotus, Ore, Red Mandrake) or treasure chest is detected within a safe deviation radius.
/// Verifies that no roaming mob threat pull radius overlaps the pickup path, collects with 'X', and yields back.
/// Zero Energy cost; operates continuously during all overworld movement.
/// </summary>
public sealed class OverworldHarvestCard : IJobCard
{
    private readonly OverworldOpportunityCollector _collector = new();

    public string Id => "job.overworld-harvest";
    public string Title => "Overworld Reagent & Chest Harvest";
    public string Category => "Harvesting";
    public int Priority => 45; // Evaluated right above quest progression (50), allowing opportunistic pickups!

    public OverworldOpportunityCollector Collector => _collector;

    public bool ShouldTrigger(JobExecutionContext context)
    {
        return !context.Snapshot.InCombat;
    }

    public Task<JobCardStatus> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(JobCardStatus.Unsupported);
    }
}

/// <summary>
/// Main Questing & Pathing Card: The standard progression card for walking, dialogues, and goals.
/// </summary>
public sealed class QuestProgressionCard : IJobCard
{
    public string Id => "job.quest-progression";
    public string Title => "Main Quest Progression";
    public string Category => "Quests";
    public int Priority => 50;

    public bool ShouldTrigger(JobExecutionContext context) => !context.Snapshot.InCombat;

    public Task<JobCardStatus> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Legacy proposals cannot dispatch or claim an outcome. Use the shared JobDeckService registry.
        return Task.FromResult(JobCardStatus.Unsupported);
    }
}

/// <summary>
/// Bazaar Procurement Card: Restocks and snipes reagents (Diamond, Agave Nectar, Aethyr Ore, Scrap Iron)
/// and rare Treasure Cards (Empower, Aftershock, Loremaster, Reindeer Knight) from the Olde Town Bazaar.
/// Zero Energy cost; gated strictly by gold budget and backpack capacity.
/// </summary>
public sealed class BazaarProcurementCard : IJobCard
{
    public string Id => "job.bazaar-procurement";
    public string Title => "Bazaar Reagent & TC Procurement";
    public string Category => "Trading";
    public int Priority => 25;

    public string ProcurementType { get; init; } = "Both"; // "Reagents", "TreasureCards", "Both"
    public System.Collections.Generic.IReadOnlyList<string> TargetItems { get; init; } = ["Diamond", "Empower", "Agave Nectar", "Aethyr Ore"];
    public int MaxSpendGold { get; init; } = 150000;

    public bool ShouldTrigger(JobExecutionContext context)
    {
        return !context.Snapshot.InCombat &&
               context.Snapshot.BackpackCount < context.Snapshot.BackpackCapacity;
    }

    public Task<JobCardStatus> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(JobCardStatus.Unsupported);
    }
}

/// <summary>
/// Boss Gauntlet & Sigil Speed-Farming Card: Automates rapid dungeon boss farming loops.
/// Accredits the community's Halfang Bristlecrown cave routine and Asgard sigil speed-clears.
/// Enters the cave/sigil, initiates combat, loots drops, exits, and repeats continuously with ZERO Energy cost.
/// </summary>
public sealed class BossGauntletFarmCard : IJobCard
{
    public string Id => "job.boss-gauntlet-farm";
    public string Title => "Boss Gauntlet & Cave Gold Farming";
    public string Category => "Farming";
    public int Priority => 40;

    public string TargetDungeon { get; init; } = "Vestrilund/HalfangCave";
    public double MinimumHealthRatio { get; init; } = 0.30;
    public double MinimumManaRatio { get; init; } = 0.20;

    public bool ShouldTrigger(JobExecutionContext context)
    {
        return !context.Snapshot.InCombat &&
               context.Snapshot.BackpackCount < context.Snapshot.BackpackCapacity &&
               context.Snapshot.HealthRatio >= MinimumHealthRatio &&
               context.Snapshot.ManaRatio >= MinimumManaRatio;
    }

    public Task<JobCardStatus> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(JobCardStatus.Unsupported);
    }
}

/// <summary>
/// Gardening & Crop Care Card: Consumes Energy to inspect plant status, satisfy pests/needs
/// (Downpour, Flute Ensemble, Pest Zapper), and harvest mature or elder crops.
/// </summary>
public sealed class GardeningCareCard : IJobCard
{
    public string Id => "job.gardening-care";
    public string Title => "Gardening & Crop Care";
    public string Category => "Gardening";
    public int Priority => 15; // Evaluated after emergency combat/potions, before zero-energy farming

    public int MinEnergyRequired { get; init; } = 15;
    public string TargetCrop { get; init; } = "Couch Potatoes";

    public bool ShouldTrigger(JobExecutionContext context)
    {
        return !context.Snapshot.InCombat &&
               context.Snapshot.EnergyAvailable >= MinEnergyRequired;
    }

    public Task<JobCardStatus> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(JobCardStatus.Unsupported);
    }
}

/// <summary>
/// Pet Snack Feeding Card: Automatically selects and feeds optimal pet snacks matching pet school likes
/// and stat goals using PetTalentCalculator and GrubNinja attribute profiles.
/// </summary>
public sealed class PetSnackFeedingCard : IJobCard
{
    public string Id => "job.pet-snack-feeding";
    public string Title => "Pet Snack Feeding";
    public string Category => "Pets";
    public int Priority => 32;

    public int MinEnergyRequired { get; init; } = 2;
    public string TargetAttribute { get; init; } = "Strength";

    public bool ShouldTrigger(JobExecutionContext context)
    {
        return !context.Snapshot.InCombat &&
               context.Snapshot.EnergyAvailable >= MinEnergyRequired;
    }

    public Task<JobCardStatus> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(JobCardStatus.Unsupported);
    }
}

/// <summary>
/// Tower Shield Watchdog: Continuous invariant guard that monitors for client disconnects,
/// game freezes, or anomalous crash dialogs to safely abort active routines.
/// </summary>
public sealed class TowerShieldWatchdog : IJobCard
{
    public string Id => "shield.tower-shield";
    public string Title => "Tower Shield (Disconnect & Freeze Guard)";
    public string Category => "Safety";
    public int Priority => 0; // Highest safety priority

    public bool ShouldTrigger(JobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Snapshot.HealthRatio < 0 || context.Snapshot.HealthRatio > 1.0;
    }

    public Task<JobCardStatus> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(JobCardStatus.Unsupported);
    }
}

/// <summary>
/// Emergency Potion Treasure Card (TC): High-priority side-deck interrupt that bypasses
/// the standard hand draw to immediately consume a potion bottle when health or mana reaches critical failure levels.
/// </summary>
public sealed class EmergencyPotionTC : IJobCard
{
    public string Id => "tc.emergency-potion";
    public string Title => "Emergency Potion Quaff";
    public string Category => "Safety";
    public int Priority => 2; // High safety priority

    public double HealthCriticalThreshold { get; init; } = 0.25;
    public double ManaCriticalThreshold { get; init; } = 0.10;

    public bool ShouldTrigger(JobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return (context.Snapshot.HealthRatio < HealthCriticalThreshold || context.Snapshot.ManaRatio < ManaCriticalThreshold) &&
               context.Snapshot.PotionsAvailable > 0;
    }

    public Task<JobCardStatus> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(JobCardStatus.Unsupported);
    }
}
