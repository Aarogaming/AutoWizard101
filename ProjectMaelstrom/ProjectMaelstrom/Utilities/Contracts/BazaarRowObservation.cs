namespace ProjectMaelstrom.Utilities.Contracts;

/// <summary>
/// Structured observation of an individual row in the Bazaar item table.
/// </summary>
public readonly record struct BazaarRowObservation(
    int RowIndex,
    string ItemName,
    int Quantity,
    int UnitPriceGold,
    NormalizedRect RowBounds,
    float Confidence);
