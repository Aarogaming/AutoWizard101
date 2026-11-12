using System;
using System.Collections.Generic;
using System.Linq;

namespace KursovaLibrary
{
    public abstract class Spell
    {
        public virtual double BaseDamage { get; set; }

        public abstract string CalculateDamage(List<DamageModificator> _dm, Wizard wizard, Boss boss, double Enchant, double Critical);

        public List<DamageModificator> CheckPierce(double pierce, double resist, List<DamageModificator> _dm)
        {
            var _tempdm = new List<DamageModificator>(_dm ?? []);

            if (pierce > 0)
            {
                if (_tempdm.Any(s => s is Shield))
                {
                    double maxshield = Math.Round(_tempdm
                                    .Where(s => s is Shield)
                                    .Min(s => s.Damage), 2);

                    var shieldtoremove = _tempdm
                        .Where(s => s is Shield)
                        .First(s => s.Damage == maxshield);

                    _tempdm.Remove(shieldtoremove);

                    double shieldPercent = 100 - (maxshield * 100);

                    // Recursion
                    if (pierce - shieldPercent > 0)
                    {
                        return CheckPierce(pierce - shieldPercent, resist, _tempdm);
                    }
                    else
                    {
                        double remainingShield = Math.Round(shieldPercent - pierce);
                        if (remainingShield > 0)
                        {
                            _tempdm.Add(new Shield { Damage = remainingShield });
                        }
                        if (resist > 0)
                        {
                            _tempdm.Add(new Shield { Damage = resist });
                        }
                        return _tempdm;
                    }
                }
                else if (resist > 0)
                {
                    if (pierce >= resist)
                    {
                        return _tempdm;
                    }
                    else
                    {
                        _tempdm.Add(new Shield { Damage = Math.Abs(pierce - resist) });
                        return _tempdm;
                    }
                }
                else
                {
                    return _tempdm;
                }
            }
            else
            {
                if (resist > 0)
                {
                    _tempdm.Add(new Shield { Damage = resist });
                }
                return _tempdm;
            }
        }

        protected double CheckStats(Wizard wizard, Boss boss)
        {
            double increase = 1.0;

            if (wizard != null && wizard.PercentDamage != 0)
            {
                increase *= (1.0 + wizard.PercentDamage / 100.0);
            }
            if (boss != null && boss.Boost != 0)
            {
                increase *= (1.0 + boss.Boost / 100.0);
            }
            return increase;
        }
    }
}
