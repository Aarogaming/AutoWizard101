using System.Collections.Generic;

namespace KursovaLibrary
{
    public class DoTSpell : Spell
    {
        public double DoT { get; set; }

        public override double BaseDamage { get; set; }

        public override string CalculateDamage(List<DamageModificator> _dm, Wizard wizard, Boss boss, double Enchant, double Critical)
        {
            var modifiers = new List<DamageModificator>(_dm ?? []);
            modifiers.Reverse();

            modifiers = CheckPierce(wizard?.Pierce ?? 0, boss?.Resist ?? 0, modifiers);

            double baseDamage = BaseDamage;
            double dot = DoT;

            if (baseDamage > 0)
            {
                baseDamage += Enchant;
            }
            else
            {
                dot += Enchant;
            }

            foreach (DamageModificator d in modifiers)
            {
                baseDamage *= d.Damage;
                dot *= d.Damage;
            }

            baseDamage *= CheckStats(wizard, boss);
            dot *= CheckStats(wizard, boss);

            baseDamage += wizard?.FlatDamage ?? 0;
            dot += wizard?.FlatDamage ?? 0;

            return $"Your Damage will be: {(int)baseDamage} + {(int)dot} overtime. And {(int)(baseDamage * Critical)} + {(int)(dot * Critical)} overtime if it Crits.";
        }
    }
}
