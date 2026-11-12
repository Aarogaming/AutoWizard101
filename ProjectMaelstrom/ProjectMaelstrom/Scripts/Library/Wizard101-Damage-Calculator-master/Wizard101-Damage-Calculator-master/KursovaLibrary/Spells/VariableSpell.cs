using System.Collections.Generic;

namespace KursovaLibrary
{
    public class VariableSpell : Spell
    {
        public double MaxDamage { get; set; }
        public override double BaseDamage { get; set; }

        public override string CalculateDamage(List<DamageModificator> _dm, Wizard wizard, Boss boss, double Enchant, double Critical)
        {
            var modifiers = new List<DamageModificator>(_dm ?? []);
            modifiers.Reverse();

            modifiers = CheckPierce(wizard?.Pierce ?? 0, boss?.Resist ?? 0, modifiers);

            double minDamage = BaseDamage + Enchant;
            double maxDamage = MaxDamage + Enchant;

            foreach (DamageModificator d in modifiers)
            {
                minDamage *= d.Damage;
                maxDamage *= d.Damage;
            }

            minDamage *= CheckStats(wizard, boss);
            maxDamage *= CheckStats(wizard, boss);

            minDamage += wizard?.FlatDamage ?? 0;
            maxDamage += wizard?.FlatDamage ?? 0;

            return $"Your Damage will be between: {(int)minDamage} and {(int)maxDamage}. And between {(int)(minDamage * Critical)} and {(int)(maxDamage * Critical)} if it Crits.";
        }
    }
}
