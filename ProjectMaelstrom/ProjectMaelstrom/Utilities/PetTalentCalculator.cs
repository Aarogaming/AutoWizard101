using System;
using System.Collections.Generic;

namespace ProjectMaelstrom.Utilities;

public record PetAttributes(double Strength = 255.0, double Intellect = 250.0, double Agility = 260.0, double Will = 260.0, double Power = 250.0)
{
    public double GetStat(string name) => name switch
    {
        "Str" or "Strength" => Strength,
        "Int" or "Intellect" => Intellect,
        "Agi" or "Agility" => Agility,
        "Will" => Will,
        "Pow" or "Power" => Power,
        _ => 0.0
    };
}

public record PetTalentDefinition(string Name, string TalentType, string TalentSchool, string Stat1, string Stat2, double Modifier);

/// <summary>
/// Native C# implementation of pet talent calculation.
/// Ported from Wizard101-Utilities (petStatCalc.js).
/// Evaluates: (2 * (Stat1 + Stat2) + Power) * Modifier.
/// </summary>
public static class PetTalentCalculator
{
    public static readonly Dictionary<string, PetTalentDefinition> StandardTalents = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Spell-Proof", new PetTalentDefinition("Spell-Proof", "Resist", "Universal", "Str", "Agi", 2.0 / 250.0) },
        { "Spell-Defying", new PetTalentDefinition("Spell-Defying", "Resist", "Universal", "Str", "Agi", 1.0 / 250.0) },
        { "Fire-Dealer", new PetTalentDefinition("Fire-Dealer", "Damage", "Fire", "Str", "Will", 3.0 / 400.0) },
        { "Fire-Giver", new PetTalentDefinition("Fire-Giver", "Damage", "Fire", "Str", "Will", 3.0 / 666.6667) },
        { "Pain-Giver", new PetTalentDefinition("Pain-Giver", "Damage", "Universal", "Str", "Will", 3.0 / 666.6667) },
        { "Storm-Dealer", new PetTalentDefinition("Storm-Dealer", "Damage", "Storm", "Str", "Will", 3.0 / 400.0) },
        { "Storm-Giver", new PetTalentDefinition("Storm-Giver", "Damage", "Storm", "Str", "Will", 3.0 / 666.6667) }
    };

    public static double CalculateTalentValue(PetTalentDefinition talent, PetAttributes stats)
    {
        if (talent.Modifier == 0.0)
        {
            return 0.0;
        }

        double val1 = stats.GetStat(talent.Stat1);
        double val2 = stats.GetStat(talent.Stat2);
        double power = stats.GetStat("Power");

        double raw = ((2.0 * (val1 + val2)) + power) * talent.Modifier;
        return Math.Round(raw, 2);
    }

    public static Dictionary<string, Dictionary<string, double>> CalculatePoolSummary(
        IEnumerable<string> talentNames, PetAttributes stats)
    {
        var summary = new Dictionary<string, Dictionary<string, double>>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in talentNames)
        {
            if (!StandardTalents.TryGetValue(name, out var talent) || talent.Modifier == 0.0)
            {
                continue;
            }

            double val = CalculateTalentValue(talent, stats);
            if (!summary.TryGetValue(talent.TalentType, out var typeDict))
            {
                typeDict = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                summary[talent.TalentType] = typeDict;
            }

            typeDict[talent.TalentSchool] = typeDict.GetValueOrDefault(talent.TalentSchool, 0.0) + val;
        }

        return summary;
    }
}
