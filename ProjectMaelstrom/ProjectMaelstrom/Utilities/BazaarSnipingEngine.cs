using System;
using System.Collections.Generic;
using System.Drawing;

namespace ProjectMaelstrom.Utilities;

public record BazaarItemPrice(string ItemName, int PriceGold, int AvailableCount);

/// <summary>
/// Native C# Bazaar Sniping & Purchasing Engine.
/// Ported directly from Farming_Bazaar_orig.py (Wizard101-Bazaar-Tc-Farming-master).
/// Enforces the verified 800 - 2,000 gold purchase bracket for bulk Empower TC sniping
/// and provides window-relative navigation coordinates for background input dispatch.
/// </summary>
public static class BazaarSnipingEngine
{
    // Verified coordinate targets for Bazaar UI windows
    public static readonly Point NextPageButton = new(590, 560);
    public static readonly Point PrevPageButton = new(205, 560);
    public static readonly Point BuyTabButton = new(135, 120);
    public static readonly Point ConfirmBuyButton = new(355, 410);
    public static readonly Point QuantityMaxButton = new(430, 360);

    // Price bracket limits for bulk Empower sniping (from Farming_Bazaar_orig.py)
    public const int EmpowerMinGold = 800;
    public const int EmpowerMaxGold = 2000;

    /// <summary>
    /// Checks whether an Empower TC price falls within the verified bulk purchase bracket.
    /// </summary>
    public static bool IsWithinEmpowerBracket(int goldPrice) =>
        goldPrice >= EmpowerMinGold && goldPrice <= EmpowerMaxGold;

    /// <summary>
    /// Evaluates candidate items on a page and selects the best purchase target.
    /// Prioritizes items within the target bracket that have available inventory.
    /// </summary>
    public static BazaarItemPrice? SelectBestPurchaseTarget(IEnumerable<BazaarItemPrice> pageItems, string targetItemName)
    {
        BazaarItemPrice? best = null;

        foreach (var item in pageItems)
        {
            if (!string.Equals(item.ItemName, targetItemName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (item.AvailableCount <= 0)
            {
                continue;
            }

            if (string.Equals(targetItemName, "Empower", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsWithinEmpowerBracket(item.PriceGold))
                {
                    continue;
                }
            }

            if (best == null || item.PriceGold < best.PriceGold)
            {
                best = item;
            }
        }

        return best;
    }
}
