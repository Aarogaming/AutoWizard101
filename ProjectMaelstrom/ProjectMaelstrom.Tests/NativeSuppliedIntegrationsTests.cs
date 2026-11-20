using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using ProjectMaelstrom.Utilities;
using Xunit;

namespace ProjectMaelstrom.Tests;

public class NativeSuppliedIntegrationsTests
{
    [Fact]
    public void ZoneTopology_ResolvesNames_AndFindsShortestPath()
    {
        var topology = new ZoneTopologyService();

        Assert.Equal("WizardCity/WC_Hub", ZoneTopologyService.GetZonePath("COMMONS"));
        Assert.Equal("WizardCity/WC_Streets/Interiors/WC_OldeTown_AuctionHouse", ZoneTopologyService.GetZonePath("BAZAAR"));
        Assert.Equal("RAVENWOOD", ZoneTopologyService.GetCodename("WizardCity/WC_Ravenwood"));

        var path = topology.FindShortestZonePath("COMMONS", "BAZAAR");
        Assert.Equal(["COMMONS", "SHOPPING_DISTRICT", "OLDE_TOWN", "BAZAAR"], path);

        var pathSchool = topology.FindShortestZonePath("COMMONS", "SCHOOL_OF_FIRE");
        Assert.Equal(["COMMONS", "RAVENWOOD", "SCHOOL_OF_FIRE"], pathSchool);
    }

    [Fact]
    public void PetTalentCalculator_CalculatesExpectedProofAndDealer()
    {
        var maxPet = new PetAttributes(255, 250, 260, 260, 250);

        var proof = PetTalentCalculator.StandardTalents["Spell-Proof"];
        double proofVal = PetTalentCalculator.CalculateTalentValue(proof, maxPet);
        Assert.Equal(10.24, proofVal);

        var dealer = PetTalentCalculator.StandardTalents["Fire-Dealer"];
        double dealerVal = PetTalentCalculator.CalculateTalentValue(dealer, maxPet);
        Assert.Equal(9.60, dealerVal);

        var summary = PetTalentCalculator.CalculatePoolSummary(["Spell-Proof", "Spell-Defying", "Fire-Dealer"], maxPet);
        Assert.True(summary.ContainsKey("Resist"));
        Assert.True(summary.ContainsKey("Damage"));
        Assert.Equal(9.60, summary["Damage"]["Fire"]);
    }

    [Fact]
    public void DeckEncoder_RoundTripsDictionaryTokens()
    {
        var normal = new Dictionary<string, int> { ["Epic"] = 3, ["Tempest"] = 4 };
        var tc = new Dictionary<string, int> { ["Empower TC"] = 5 };
        var item = new Dictionary<string, int> { ["Wand Hit"] = 1 };

        string token = DeckEncoder.Encode(normal, tc, item);
        Assert.False(string.IsNullOrWhiteSpace(token));

        var (decodedNormal, decodedTc, decodedItem) = DeckEncoder.Decode(token);
        Assert.Equal(normal["Epic"], decodedNormal["Epic"]);
        Assert.Equal(normal["Tempest"], decodedNormal["Tempest"]);
        Assert.Equal(tc["Empower TC"], decodedTc["Empower TC"]);
        Assert.Equal(item["Wand Hit"], decodedItem["Wand Hit"]);
    }

    [Fact]
    public void CombatGeometry_CalculatesEnchantAndTargetingClicks()
    {
        var epic = new CombatCardGeometry("Epic", 260, 325, 52);
        var tempest = new CombatCardGeometry("Tempest", 320, 325, 52);

        var (click1, click2, enchanted) = CombatGeometry.CalculateEnchantGeometry(epic, tempest);
        Assert.Equal(new Point(260, 325), click1);
        Assert.Equal(new Point(320, 325), click2);
        Assert.Equal("Epic-Tempest", enchanted.Name);
        Assert.Equal(294, enchanted.SpellX);

        var enemy0 = CombatGeometry.CalculateTargetCoordinates(0);
        Assert.Equal(new Point(130, 50), enemy0);
        var enemy1 = CombatGeometry.CalculateTargetCoordinates(1);
        Assert.Equal(new Point(304, 50), enemy1);

        var ally4 = CombatGeometry.CalculateTargetCoordinates(4);
        Assert.Equal(new Point(682, 590), ally4);

        var ped0 = CombatGeometry.GetEnemyPedestalCoordinates(0);
        Assert.Equal(new Point(203, 75), ped0);
    }

    [Fact]
    public void GardeningCareEngine_CalculatesGrowthMultipliersAndSpells()
    {
        string[] activeLikes = ["Tropical Garden Gnome", "Red Barn Farm", "King Parsley"];
        double multiplier = GardeningCareEngine.CalculateGrowthMultiplier("Couch Potatoes", activeLikes);
        Assert.Equal(1.728, Math.Round(multiplier, 3));

        var (matureHours, elderHours) = GardeningCareEngine.CalculateGrowthTime("Couch Potatoes", activeLikes);
        Assert.Equal(27.78, matureHours);
        Assert.Equal(55.56, elderHours);

        var (spell, cost) = GardeningCareEngine.ResolveGardeningSpell("water");
        Assert.Equal("Downpour", spell);
        Assert.Equal(15, cost);
    }

    [Fact]
    public void BazaarSnipingEngine_FiltersEmpowerBracketsAndSelectsTarget()
    {
        Assert.True(BazaarSnipingEngine.IsWithinEmpowerBracket(800));
        Assert.True(BazaarSnipingEngine.IsWithinEmpowerBracket(1500));
        Assert.False(BazaarSnipingEngine.IsWithinEmpowerBracket(750));
        Assert.False(BazaarSnipingEngine.IsWithinEmpowerBracket(2100));

        var items = new List<BazaarItemPrice>
        {
            new("Empower", 2200, 10),
            new("Empower", 1200, 25),
            new("Empower", 900, 5),
            new("Firecat", 50, 100)
        };

        var best = BazaarSnipingEngine.SelectBestPurchaseTarget(items, "Empower");
        Assert.NotNull(best);
        Assert.Equal(900, best.PriceGold);
        Assert.Equal(5, best.AvailableCount);
    }

    [Fact]
    public void PotionMotionBoard_FindsConnectedMatchesAndBestMove()
    {
        string[][] grid =
        [
            ["blue", "red", "red", "yellow"],
            ["red", "blue", "yellow", "blue"],
            ["yellow", "yellow", "blue", "green"],
            ["green", "blue", "green", "red"]
        ];

        var board = new PotionMotionBoard(grid);
        var move = board.FindBestMove(allowWrapping: true);
        Assert.NotNull(move);
        Assert.True(move.MatchedCellCount >= 3);
        Assert.True(move.Score > 0);
    }

    [Fact]
    public void SessionSupervisor_SwitchesRoutines_OnHealth_Inventory_AndEnergy()
    {
        var supervisor = new SessionSupervisor();

        var lowHpWithPotions = new WizardSessionSnapshot(
            HealthRatio: 0.25, ManaRatio: 0.80, PotionsAvailable: 2,
            BackpackCount: 40, BackpackCapacity: 80, InCombat: false, InDialogue: false,
            CurrentZoneCode: "COMMONS", EnergyAvailable: 50);

        var decision1 = supervisor.EvaluateNextStep(lowHpWithPotions);
        Assert.Equal(WizardActivity.PotionRefill, decision1.NextActivity);
        Assert.Contains("consume 1 potion", decision1.Reason);

        var lowHpNoPotions = lowHpWithPotions with { PotionsAvailable = 0 };
        var decision2 = supervisor.EvaluateNextStep(lowHpNoPotions);
        Assert.Equal(WizardActivity.PotionRefill, decision2.NextActivity);
        Assert.Equal("COMMONS", decision2.TargetZoneCode);
        Assert.Equal("potion-motion-refill", decision2.MacroRoutineId);

        var fullBag = lowHpWithPotions with { HealthRatio = 0.90, BackpackCount = 80 };
        var decision3 = supervisor.EvaluateNextStep(fullBag);
        Assert.Equal(WizardActivity.BazaarTrading, decision3.NextActivity);
        Assert.Equal("BAZAAR", decision3.TargetZoneCode);

        var travelPath = supervisor.ResolveTravelRoute("UNICORN_WAY", "BAZAAR");
        Assert.Equal(["UNICORN_WAY", "COMMONS", "SHOPPING_DISTRICT", "OLDE_TOWN", "BAZAAR"], travelPath);
    }

    [Fact]
    public async Task RoutineDeck_StacksJobCards_SelectsPriority_AndDefaultsToIdle()
    {
        var deck = new RoutineDeck("Main Leveling Deck");

        // Stack the cards into the deck
        deck.AddCard(new ActiveCombatCard());           // Priority 1
        deck.AddCard(new HealthPotionGuardCard());      // Priority 10
        deck.AddCard(new BackpackFullBazaarCard());     // Priority 20
        deck.AddCard(new PetTrainingDanceCard());       // Priority 30
        deck.AddCard(new QuestProgressionCard());       // Priority 50

        Assert.Equal(5, deck.Cards.Count);

        // 1. When in combat, ActiveCombatCard (Priority 1) triggers first
        var inCombatSnapshot = new WizardSessionSnapshot(
            HealthRatio: 0.20, ManaRatio: 0.10, PotionsAvailable: 1,
            BackpackCount: 80, BackpackCapacity: 80, InCombat: true, InDialogue: false,
            CurrentZoneCode: "UNICORN_WAY", EnergyAvailable: 50);

        var context1 = new JobExecutionContext(inCombatSnapshot);
        var card1 = deck.DrawNextCard(context1);
        Assert.IsType<ActiveCombatCard>(card1);
        var status1 = await deck.ExecuteStepAsync(context1, CancellationToken.None);
        Assert.Equal(JobCardStatus.Unsupported, status1);

        // 2. When out of combat with critical health, HealthPotionGuardCard (Priority 10) takes precedence over Bazaar (20)
        var lowHpSnapshot = inCombatSnapshot with { InCombat = false };
        var context2 = new JobExecutionContext(lowHpSnapshot);
        var card2 = deck.DrawNextCard(context2);
        Assert.IsType<HealthPotionGuardCard>(card2);

        // 3. When health is good but bag is full, BackpackFullBazaarCard (Priority 20) triggers
        var fullBagSnapshot = lowHpSnapshot with { HealthRatio = 1.0, ManaRatio = 1.0 };
        var context3 = new JobExecutionContext(fullBagSnapshot);
        var card3 = deck.DrawNextCard(context3);
        Assert.IsType<BackpackFullBazaarCard>(card3);

        // 4. When no specific conditions trigger and not in questing, defaults to AutoIdleJobCard
        var idleDeck = new RoutineDeck("Idle Only Deck");
        // Only add a pet card that requires 50 energy, but wizard has 0
        idleDeck.AddCard(new PetTrainingDanceCard { MinEnergyRequired = 50 });
        var zeroEnergySnapshot = fullBagSnapshot with { BackpackCount = 10, EnergyAvailable = 0 };
        var context4 = new JobExecutionContext(zeroEnergySnapshot);

        var fallbackCard = idleDeck.DrawNextCard(context4);
        Assert.IsType<AutoIdleJobCard>(fallbackCard);
        var fallbackStatus = await idleDeck.ExecuteStepAsync(context4, CancellationToken.None);
        Assert.Equal(JobCardStatus.Unsupported, fallbackStatus);
    }
}
