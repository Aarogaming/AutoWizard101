using System;
using System.Threading;
using System.Threading.Tasks;
using ProjectMaelstrom.Utilities;
using Xunit;

namespace ProjectMaelstrom.Tests;

public class ExpandedJobCardTests
{
    [Fact]
    public void BossGauntletFarmCard_OperatesWithZeroEnergy_GatedOnlyByBackpackAndHealth()
    {
        var farmCard = new BossGauntletFarmCard
        {
            TargetDungeon = "Vestrilund/HalfangCave",
            MinimumHealthRatio = 0.30,
            MinimumManaRatio = 0.20
        };

        Assert.Equal("job.boss-gauntlet-farm", farmCard.Id);
        Assert.Equal(40, farmCard.Priority);

        // Player has ZERO Energy, but has bag space (30/80) and healthy HP/Mana
        var zeroEnergySnapshot = new WizardSessionSnapshot(
            HealthRatio: 1.0,
            ManaRatio: 0.8,
            PotionsAvailable: 3,
            BackpackCount: 30,
            BackpackCapacity: 80,
            InCombat: false,
            InDialogue: false,
            CurrentZoneCode: "VESTRILUND",
            EnergyAvailable: 0); // 0 Energy!

        var context = new JobExecutionContext(zeroEnergySnapshot);

        // Boss farming should trigger freely even with 0 Energy!
        Assert.True(farmCard.ShouldTrigger(context));

        // When backpack fills up (80/80), farming stops
        var fullBagContext = new JobExecutionContext(zeroEnergySnapshot with { BackpackCount = 80 });
        Assert.False(farmCard.ShouldTrigger(fullBagContext));

        // When in combat, out-of-combat farm card yields to ActiveCombatCard
        var inCombatContext = new JobExecutionContext(zeroEnergySnapshot with { InCombat = true });
        Assert.False(farmCard.ShouldTrigger(inCombatContext));
    }

    [Fact]
    public void BazaarProcurementCard_SnipesReagentsAndTCs_WithoutConsumingEnergy()
    {
        var bazaarCard = new BazaarProcurementCard
        {
            ProcurementType = "Both",
            TargetItems = ["Diamond", "Aethyr Ore", "Empower", "Aftershock"],
            MaxSpendGold = 200000
        };

        Assert.Equal("job.bazaar-procurement", bazaarCard.Id);
        Assert.Equal(25, bazaarCard.Priority);
        Assert.Equal(4, bazaarCard.TargetItems.Count);

        var snapshot = new WizardSessionSnapshot(
            HealthRatio: 1.0,
            ManaRatio: 1.0,
            PotionsAvailable: 3,
            BackpackCount: 20,
            BackpackCapacity: 80,
            InCombat: false,
            InDialogue: false,
            CurrentZoneCode: "BAZAAR",
            EnergyAvailable: 0);

        var context = new JobExecutionContext(snapshot);
        Assert.True(bazaarCard.ShouldTrigger(context));

        // Backpack full prevents procurement
        var fullBagContext = new JobExecutionContext(snapshot with { BackpackCount = 80 });
        Assert.False(bazaarCard.ShouldTrigger(fullBagContext));
    }

    [Fact]
    public void GardeningCareCard_StrictlyGatedByEnergy()
    {
        var gardenCard = new GardeningCareCard
        {
            MinEnergyRequired = 25,
            TargetCrop = "Couch Potatoes"
        };

        Assert.Equal("job.gardening-care", gardenCard.Id);
        Assert.Equal(15, gardenCard.Priority);

        // 10 Energy < 25 required -> Insufficient funds, does not trigger
        var lowEnergyContext = new JobExecutionContext(new WizardSessionSnapshot(
            HealthRatio: 1.0, ManaRatio: 1.0, PotionsAvailable: 3,
            BackpackCount: 10, BackpackCapacity: 80, InCombat: false, InDialogue: false,
            CurrentZoneCode: "BARN_FARM", EnergyAvailable: 10));

        Assert.False(gardenCard.ShouldTrigger(lowEnergyContext));

        // 35 Energy >= 25 required -> Triggers!
        var sufficientEnergyContext = new JobExecutionContext(new WizardSessionSnapshot(
            HealthRatio: 1.0, ManaRatio: 1.0, PotionsAvailable: 3,
            BackpackCount: 10, BackpackCapacity: 80, InCombat: false, InDialogue: false,
            CurrentZoneCode: "BARN_FARM", EnergyAvailable: 35));

        Assert.True(gardenCard.ShouldTrigger(sufficientEnergyContext));
    }

    [Fact]
    public void PetSnackFeedingCard_GatedBySnackEnergy()
    {
        var snackCard = new PetSnackFeedingCard
        {
            MinEnergyRequired = 2,
            TargetAttribute = "Strength"
        };

        Assert.Equal("job.pet-snack-feeding", snackCard.Id);
        Assert.Equal(32, snackCard.Priority);

        var noEnergyContext = new JobExecutionContext(new WizardSessionSnapshot(
            HealthRatio: 1.0, ManaRatio: 1.0, PotionsAvailable: 3,
            BackpackCount: 10, BackpackCapacity: 80, InCombat: false, InDialogue: false,
            CurrentZoneCode: "PET_PAVILION", EnergyAvailable: 0));

        Assert.False(snackCard.ShouldTrigger(noEnergyContext));

        var hasEnergyContext = new JobExecutionContext(new WizardSessionSnapshot(
            HealthRatio: 1.0, ManaRatio: 1.0, PotionsAvailable: 3,
            BackpackCount: 10, BackpackCapacity: 80, InCombat: false, InDialogue: false,
            CurrentZoneCode: "PET_PAVILION", EnergyAvailable: 4));

        Assert.True(snackCard.ShouldTrigger(hasEnergyContext));
    }

    [Fact]
    public async Task EmergencyPotionTC_SideDeckInterrupt_TriggersOnCriticalThresholds()
    {
        var potionTc = new EmergencyPotionTC
        {
            HealthCriticalThreshold = 0.25,
            ManaCriticalThreshold = 0.10
        };

        Assert.Equal("tc.emergency-potion", potionTc.Id);
        Assert.Equal(2, potionTc.Priority);

        // Healthy context: does not trigger
        var healthyContext = new JobExecutionContext(new WizardSessionSnapshot(
            HealthRatio: 0.80, ManaRatio: 0.50, PotionsAvailable: 2,
            BackpackCount: 10, BackpackCapacity: 80, InCombat: false, InDialogue: false,
            CurrentZoneCode: "COMMONS", EnergyAvailable: 50));

        Assert.False(potionTc.ShouldTrigger(healthyContext));

        // Critical health context (20% HP) with potions: Triggers!
        var criticalHpContext = new JobExecutionContext(new WizardSessionSnapshot(
            HealthRatio: 0.20, ManaRatio: 0.50, PotionsAvailable: 2,
            BackpackCount: 10, BackpackCapacity: 80, InCombat: false, InDialogue: false,
            CurrentZoneCode: "COMMONS", EnergyAvailable: 50));

        Assert.True(potionTc.ShouldTrigger(criticalHpContext));

        var status = await potionTc.ExecuteAsync(criticalHpContext, CancellationToken.None);
        Assert.Equal(JobCardStatus.Unsupported, status);

        // Critical health but ZERO potions available: Cannot trigger (insufficient funds)
        var zeroPotionsContext = new JobExecutionContext(new WizardSessionSnapshot(
            HealthRatio: 0.20, ManaRatio: 0.50, PotionsAvailable: 0,
            BackpackCount: 10, BackpackCapacity: 80, InCombat: false, InDialogue: false,
            CurrentZoneCode: "COMMONS", EnergyAvailable: 50));

        Assert.False(potionTc.ShouldTrigger(zeroPotionsContext));
    }

    [Fact]
    public void TowerShieldWatchdog_ValidatesTelemetryIntegrity()
    {
        var shield = new TowerShieldWatchdog();
        Assert.Equal("shield.tower-shield", shield.Id);
        Assert.Equal(0, shield.Priority);

        var validContext = new JobExecutionContext(new WizardSessionSnapshot(
            HealthRatio: 0.90, ManaRatio: 0.90, PotionsAvailable: 1,
            BackpackCount: 10, BackpackCapacity: 80, InCombat: false, InDialogue: false,
            CurrentZoneCode: "COMMONS", EnergyAvailable: 50));

        // Healthy snapshot -> does not trigger safety abort
        Assert.False(shield.ShouldTrigger(validContext));

        // Corrupted telemetry (HealthRatio > 1.0) -> triggers safety abort!
        var corruptedContext = new JobExecutionContext(new WizardSessionSnapshot(
            HealthRatio: 2.50, ManaRatio: 0.90, PotionsAvailable: 1,
            BackpackCount: 10, BackpackCapacity: 80, InCombat: false, InDialogue: false,
            CurrentZoneCode: "COMMONS", EnergyAvailable: 50));

        Assert.True(shield.ShouldTrigger(corruptedContext));
    }
}
