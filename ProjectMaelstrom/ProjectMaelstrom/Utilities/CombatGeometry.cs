using System;
using System.Collections.Generic;
using System.Drawing;

namespace ProjectMaelstrom.Utilities;

/// <summary>
/// Combat card geometry representation for window-relative clicks.
/// </summary>
public record CombatCardGeometry(string Name, int SpellX, int SpellY = 325, int CardWidth = 52);

/// <summary>
/// Native C# implementation of WizSDK and Deimos combat card geometry and click positions.
/// Provides sub-millisecond, deterministic relative coordinate calculations without python or screen scraping overhead.
/// </summary>
public static class CombatGeometry
{
    // Spell area rectangle: (X: 245, Y: 290, Width: 370, Height: 70)
    public static readonly Rectangle SpellArea = new(245, 290, 370, 70);

    // Pass and Flee buttons
    public static readonly Point PassButton = new(254, 398);
    public static readonly Point FleeButton = new(546, 398);

    // Health and Mana globes
    public static readonly Point HealthGlobe = new(23, 563);
    public static readonly Point ManaGlobe = new(79, 591);

    /// <summary>
    /// Calculates the click positions for enchanting a spell with an enchant card,
    /// and returns the resulting enchanted card's position.
    /// Formula ported from wizSDK card.py.
    /// </summary>
    public static (Point Click1, Point Click2, CombatCardGeometry EnchantedCard) CalculateEnchantGeometry(
        CombatCardGeometry enchantCard, CombatCardGeometry spellCard)
    {
        var click1 = new Point(enchantCard.SpellX, enchantCard.SpellY);
        var click2 = new Point(spellCard.SpellX, spellCard.SpellY);

        bool enchantIsBefore = enchantCard.SpellX < spellCard.SpellX;
        int newX = spellCard.SpellX + (spellCard.CardWidth / 2);
        if (enchantIsBefore)
        {
            newX -= spellCard.CardWidth;
        }

        var enchanted = new CombatCardGeometry(
            $"{enchantCard.Name}-{spellCard.Name}",
            newX,
            spellCard.SpellY,
            spellCard.CardWidth);

        return (click1, click2, enchanted);
    }

    /// <summary>
    /// Calculates target coordinates for spell casting.
    /// Targets 0-3 are enemies (top row). Targets 4-7 are allies (bottom row).
    /// Ported from wizSDK card.py.
    /// </summary>
    public static Point CalculateTargetCoordinates(int targetIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(targetIndex);
        if (targetIndex > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(targetIndex), "Target index must be between 0 and 7.");
        }

        if (targetIndex < 4)
        {
            int x = (174 * targetIndex) + 130;
            return new Point(x, 50);
        }
        else
        {
            int x = (174 * (7 - targetIndex)) + 160;
            return new Point(x, 590);
        }
    }

    /// <summary>
    /// Returns the pedestal position check coordinate for enemy index (0..3).
    /// Ported from wizSDK battle.py.
    /// </summary>
    public static Point GetEnemyPedestalCoordinates(int enemyIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(enemyIndex);
        if (enemyIndex > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(enemyIndex), "Enemy index must be between 0 and 3.");
        }

        return new Point((174 * enemyIndex) + 203, 75);
    }

    /// <summary>
    /// Calculates the click coordinate for a card in hand by slot index (0..totalCards-1).
    /// In Wizard101, up to 7 cards are horizontally distributed and centered within the spell area.
    /// </summary>
    public static Point GetHandCardCoordinates(int handIndex, int totalCardsInHand = 7)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(handIndex);
        if (totalCardsInHand is < 1 or > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCardsInHand), "Total cards in hand must be between 1 and 7.");
        }
        if (handIndex >= totalCardsInHand)
        {
            throw new ArgumentOutOfRangeException(nameof(handIndex), "Hand index must be less than total cards in hand.");
        }

        const int cardWidth = 52;
        const int areaX = 245;
        const int areaWidth = 370;
        const int cardY = 325;

        int totalWidth = totalCardsInHand * cardWidth;
        int leftMargin = areaX + ((areaWidth - totalWidth) / 2);
        int cardX = leftMargin + (handIndex * cardWidth) + (cardWidth / 2);

        return new Point(cardX, cardY);
    }
}
