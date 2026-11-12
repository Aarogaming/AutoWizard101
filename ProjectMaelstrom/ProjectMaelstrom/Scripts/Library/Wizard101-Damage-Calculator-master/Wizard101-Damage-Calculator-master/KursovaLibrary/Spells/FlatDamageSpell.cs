using System.Collections.Generic;

namespace KursovaLibrary
{
    public class FlatDamageSpell : Spell
    {
        public override double BaseDamage { get; set; }

        public override string CalculateDamage(List<DamageModificator> _dm, Wizard wizard, Boss boss, double Enchant, double Critical)
        {
            var modifiers = new List<DamageModificator>(_dm ?? []);
            modifiers.Reverse();

            modifiers = CheckPierce(wizard?.Pierce ?? 0, boss?.Resist ?? 0, modifiers);

            double damage = BaseDamage + Enchant;

            foreach (DamageModificator d in modifiers)
            {
                damage *= d.Damage;
            }

            damage *= CheckStats(wizard, boss);

            damage += wizard?.FlatDamage ?? 0;

            return $"Your Damage will be: {(int)damage}. And {(int)(damage * Critical)} if it Crits.";
        }
    }
}
