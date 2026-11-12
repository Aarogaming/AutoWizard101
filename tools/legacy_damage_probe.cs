// Compile only against the reviewed legacy KursovaLibrary source in an isolated
// console harness. No WinForms application or game client is started.
using System;
using System.Collections.Generic;
using System.Text.Json;
using KursovaLibrary;

var results = new List<object>();
var wizard = new Wizard("fixture", 1, 0, 0, 0);
var boss = new Boss("fixture", 2, 0, 0);
void Check(string name, Func<bool> check)
{
    try { results.Add(new { test = name, status = check() ? "passed" : "failed" }); }
    catch (Exception error) { results.Add(new { test = name, status = "failed", error = error.GetType().Name }); }
}
Check("base_damage", () => new FlatDamageSpell { BaseDamage = 100 }.CalculateDamage([], wizard, boss, 0, 2).Contains("100. And 200"));
Check("blade_and_shield", () => new FlatDamageSpell { BaseDamage = 100 }.CalculateDamage([new Blade { Damage = 40 }, new Shield { Damage = 50 }], wizard, boss, 0, 1).Contains("70. And 70"));
Check("repeatability", () =>
{
    var spell = new FlatDamageSpell { BaseDamage = 100 };
    List<DamageModificator> modifiers = [new Blade { Damage = 40 }];
    return spell.CalculateDamage(modifiers, wizard, boss, 0, 1) == spell.CalculateDamage(modifiers, wizard, boss, 0, 1);
});
Check("flat_spell_enchant_applies", () => new FlatDamageSpell { BaseDamage = 100 }.CalculateDamage([], wizard, boss, 50, 1).Contains("150. And 150"));
Check("zero_pierce_still_applies_resist", () => new FlatDamageSpell { BaseDamage = 100 }.CalculateDamage([], wizard, new Boss("resist", 3, 50, 0), 0, 1).Contains("50. And 50"));
Check("boost_does_not_erase_wizard_damage", () =>
{
    var target = new Boss("boost", 4, 0, 20);
    var lower = new FlatDamageSpell { BaseDamage = 100 }.CalculateDamage([], wizard, target, 0, 1);
    var higher = new FlatDamageSpell { BaseDamage = 100 }.CalculateDamage([], new Wizard("stronger", 5, 0, 50, 0), target, 0, 1);
    return lower != higher;
});
Console.WriteLine(JsonSerializer.Serialize(new { archive = "Wizard101-Damage-Calculator-master.zip", mode = "isolated_pure_library_net9", live = false, checks = results }));
