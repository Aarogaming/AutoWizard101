//! `combat_turn_evaluator`: Turn-based card selection, pip arithmetic, and target arbitration.
//!
//! Evaluates candidate cards in hand against available regular/power pips, blade/trap
//! damage multipliers, and enemy health pools to determine the mathematically optimal
//! combat action within the 30-second decision window.
//!
//! Adheres strictly to `Facts ≠ Authority ≠ Actions`.

use alloc::string::String;
use alloc::vec::Vec;
use serde::{Deserialize, Serialize};

/// Magic schools in Wizard101.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
pub enum MagicSchool {
    Fire,
    Ice,
    Storm,
    Myth,
    Life,
    Death,
    Balance,
    Sun,
    Star,
    Moon,
    Shadow,
}

/// Spell functionality category.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum SpellCardType {
    DamageSingle,
    DamageAoE,
    DamageOverTime,
    BladeSelf,
    TrapEnemy,
    HealSelf,
    ShieldSelf,
    EnchantDamage,
}

/// Boss cheat trigger condition.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub enum CheatTriggerCondition {
    RoundNumber { modulo: u32, offset: u32 },
    SpellCastType(SpellCardType),
    HealthThreshold { ratio: f64 },
    PlayerCount { count: usize },
    LateJoiner,
}

/// Interrupt execution phase within a round.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
pub enum CheatInterruptPhase {
    StartOfRound,
    ImmediateReaction,
    EndOfRound,
    PassiveAura,
}

/// Scripted interrupt effect executed by a boss cheat.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub enum CheatEffect {
    CastSpell {
        spell_name: String,
        target_pedestal: usize,
    },
    ApplyAura {
        aura_name: String,
    },
    RemoveCharms,
    RemoveWards,
    ForceDiscardHand,
    InterruptStun,
}

/// Tactical counter-constraint imposed on the player's turn evaluator.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub enum CounterConstraint {
    ForbidTraps,
    ForbidBlades,
    RequireDamageOverTime,
    CapDamagePerHit(f64),
    ForbidHealing,
}

/// Declarative Boss Cheat Rule representing a scripted encounter mechanic.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct BossCheatRule {
    pub name: String,
    pub trigger: CheatTriggerCondition,
    pub phase: CheatInterruptPhase,
    pub effect: CheatEffect,
    pub counter_constraint: Option<CounterConstraint>,
}

impl BossCheatRule {
    pub fn new(
        name: impl Into<String>,
        trigger: CheatTriggerCondition,
        phase: CheatInterruptPhase,
        effect: CheatEffect,
    ) -> Self {
        Self {
            name: name.into(),
            trigger,
            phase,
            effect,
            counter_constraint: None,
        }
    }

    pub fn with_counter_constraint(mut self, constraint: CounterConstraint) -> Self {
        self.counter_constraint = Some(constraint);
        self
    }
}

/// Candidate spell card in hand.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct CombatCardCandidate {
    pub slot_index: usize,
    pub name: String,
    pub school: MagicSchool,
    pub pip_cost: u32,
    pub shadow_pip_cost: u32,
    pub card_type: SpellCardType,
    pub base_damage: f64,
    pub buff_percentage: f64,
    pub heal_amount: f64,
}

impl CombatCardCandidate {
    pub fn new(
        slot_index: usize,
        name: impl Into<String>,
        school: MagicSchool,
        pip_cost: u32,
        card_type: SpellCardType,
    ) -> Self {
        Self {
            slot_index,
            name: name.into(),
            school,
            pip_cost,
            shadow_pip_cost: 0,
            card_type,
            base_damage: 0.0,
            buff_percentage: 0.0,
            heal_amount: 0.0,
        }
    }

    pub fn with_damage(mut self, base_damage: f64) -> Self {
        self.base_damage = base_damage.max(0.0);
        self
    }

    pub fn with_buff(mut self, buff_percentage: f64) -> Self {
        self.buff_percentage = buff_percentage.max(0.0);
        self
    }

    pub fn with_heal(mut self, heal_amount: f64) -> Self {
        self.heal_amount = heal_amount.max(0.0);
        self
    }
}

/// Dynamic pip pool available for the current combat turn.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Default, Serialize, Deserialize)]
pub struct CombatPips {
    pub regular_pips: u32,
    pub power_pips: u32,
    pub shadow_pips: u32,
}

impl CombatPips {
    pub fn new(regular_pips: u32, power_pips: u32, shadow_pips: u32) -> Self {
        Self {
            regular_pips,
            power_pips,
            shadow_pips,
        }
    }

    /// Computes effective pip capacity for a given card school relative to player primary school.
    pub fn effective_pips(&self, card_school: MagicSchool, player_school: MagicSchool) -> u32 {
        if card_school == player_school || card_school == MagicSchool::Balance {
            self.regular_pips + self.power_pips * 2
        } else {
            self.regular_pips + self.power_pips
        }
    }

    /// Evaluates if a candidate card can be afforded this turn.
    pub fn can_afford(&self, card: &CombatCardCandidate, player_school: MagicSchool) -> bool {
        if card.shadow_pip_cost > self.shadow_pips {
            return false;
        }
        self.effective_pips(card.school, player_school) >= card.pip_cost
    }
}

/// Tracked combatant state on a pedestal (enemy 0..3 or ally 4..7).
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct CombatantState {
    pub pedestal_index: usize,
    pub name: String,
    pub school: MagicSchool,
    pub current_health: f64,
    pub max_health: f64,
    pub is_alive: bool,
    pub blades: Vec<f64>,
    pub traps: Vec<f64>,
    pub school_resistance: f64,
    pub cheats: Vec<BossCheatRule>,
}

impl CombatantState {
    pub fn new(
        pedestal_index: usize,
        name: impl Into<String>,
        school: MagicSchool,
        health: f64,
    ) -> Self {
        Self {
            pedestal_index,
            name: name.into(),
            school,
            current_health: health.max(0.0),
            max_health: health.max(1.0),
            is_alive: health > 0.0,
            blades: Vec::new(),
            traps: Vec::new(),
            school_resistance: 0.0,
            cheats: Vec::new(),
        }
    }

    pub fn with_max_health(mut self, max_health: f64) -> Self {
        self.max_health = max_health.max(1.0);
        self
    }

    pub fn with_traps(mut self, traps: Vec<f64>) -> Self {
        self.traps = traps;
        self
    }

    pub fn with_blades(mut self, blades: Vec<f64>) -> Self {
        self.blades = blades;
        self
    }

    pub fn with_resistance(mut self, resist: f64) -> Self {
        self.school_resistance = resist.clamp(-1.0, 1.0);
        self
    }

    pub fn with_cheat(mut self, cheat: BossCheatRule) -> Self {
        self.cheats.push(cheat);
        self
    }

    pub fn with_cheats(mut self, cheats: Vec<BossCheatRule>) -> Self {
        self.cheats = cheats;
        self
    }

    pub fn health_ratio(&self) -> f64 {
        if self.max_health <= 0.0 {
            0.0
        } else {
            self.current_health / self.max_health
        }
    }
}

/// Evaluator decision output for the turn.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub enum CombatDecision {
    PlayCard {
        slot_index: usize,
        total_cards: usize,
        target_index: usize,
        spell_name: String,
        estimated_damage: f64,
        is_lethal: bool,
    },
    Pass {
        reason: String,
    },
}

/// Concrete Turn-by-Turn Combat Evaluator.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct CombatTurnEvaluator {
    pub player_school: MagicSchool,
    pub gear_damage_multiplier: f64,
}

impl CombatTurnEvaluator {
    pub fn new(player_school: MagicSchool, gear_damage_multiplier: f64) -> Self {
        Self {
            player_school,
            gear_damage_multiplier: gear_damage_multiplier.max(0.0),
        }
    }

    /// Estimates damage of a candidate spell against a target enemy.
    pub fn estimate_damage(
        &self,
        card: &CombatCardCandidate,
        player_blades: &[f64],
        target: &CombatantState,
    ) -> f64 {
        if card.base_damage <= 0.0 {
            return 0.0;
        }

        let mut multiplier = 1.0 + self.gear_damage_multiplier;

        // Apply player blades
        for blade in player_blades {
            multiplier *= 1.0 + blade;
        }

        // Apply enemy traps
        for trap in &target.traps {
            multiplier *= 1.0 + trap;
        }

        // Apply target resistance
        let resist_factor = (1.0 - target.school_resistance).max(0.0);
        multiplier *= resist_factor;

        let raw_damage = card.base_damage * multiplier;

        // Apply boss damage cap cheat if present
        let mut final_damage = raw_damage;
        for cheat in &target.cheats {
            if let Some(CounterConstraint::CapDamagePerHit(cap)) = cheat.counter_constraint {
                if cap > 0.0 && final_damage > cap {
                    final_damage = cap;
                }
            }
        }

        final_damage
    }

    /// Evaluates the turn and returns the optimal combat decision.
    pub fn evaluate_turn(
        &self,
        pips: &CombatPips,
        hand: &[CombatCardCandidate],
        player: &CombatantState,
        enemies: &[CombatantState],
    ) -> CombatDecision {
        let total_cards = hand.len();
        if total_cards == 0 {
            return CombatDecision::Pass {
                reason: String::from("Hand is empty"),
            };
        }

        let alive_enemies: Vec<&CombatantState> = enemies
            .iter()
            .filter(|e| e.is_alive && e.current_health > 0.0)
            .collect();

        if alive_enemies.is_empty() {
            return CombatDecision::Pass {
                reason: String::from("No living enemies remaining"),
            };
        }

        // Aggregate active counter-constraints from living enemies
        let mut forbid_traps = false;
        let mut forbid_blades = false;
        let mut forbid_healing = false;
        let mut require_dot = false;

        for enemy in &alive_enemies {
            for cheat in &enemy.cheats {
                match cheat.counter_constraint {
                    Some(CounterConstraint::ForbidTraps) => forbid_traps = true,
                    Some(CounterConstraint::ForbidBlades) => forbid_blades = true,
                    Some(CounterConstraint::ForbidHealing) => forbid_healing = true,
                    Some(CounterConstraint::RequireDamageOverTime) => require_dot = true,
                    _ => {}
                }
            }
        }

        // 1. Filter affordable cards
        let affordable: Vec<&CombatCardCandidate> = hand
            .iter()
            .filter(|c| pips.can_afford(c, self.player_school))
            .collect();

        if affordable.is_empty() {
            return CombatDecision::Pass {
                reason: String::from("No affordable cards for available pips"),
            };
        }

        // 2. Health check: if critically low health (< 30%), prioritize HealSelf (unless forbidden)
        if player.health_ratio() < 0.30 && !forbid_healing {
            if let Some(heal) = affordable
                .iter()
                .find(|c| c.card_type == SpellCardType::HealSelf)
            {
                return CombatDecision::PlayCard {
                    slot_index: heal.slot_index,
                    total_cards,
                    target_index: player.pedestal_index, // Self (pedestal 4)
                    spell_name: heal.name.clone(),
                    estimated_damage: 0.0,
                    is_lethal: false,
                };
            }
        }

        // 3. DoT Requirement Priority: if boss requires DoT, prioritize affordable DamageOverTime spells
        if require_dot {
            let mut dot_spells: Vec<&CombatCardCandidate> = affordable
                .iter()
                .filter(|c| c.card_type == SpellCardType::DamageOverTime)
                .copied()
                .collect();
            if !dot_spells.is_empty() {
                dot_spells.sort_by(|a, b| {
                    b.base_damage
                        .partial_cmp(&a.base_damage)
                        .unwrap_or(core::cmp::Ordering::Equal)
                });
                let dot_card = dot_spells[0];
                let mut sorted_enemies = alive_enemies.clone();
                sorted_enemies.sort_by(|a, b| {
                    a.current_health
                        .partial_cmp(&b.current_health)
                        .unwrap_or(core::cmp::Ordering::Equal)
                });
                let target_enemy = sorted_enemies[0];
                let d_est = self.estimate_damage(dot_card, &player.blades, target_enemy);
                return CombatDecision::PlayCard {
                    slot_index: dot_card.slot_index,
                    total_cards,
                    target_index: target_enemy.pedestal_index,
                    spell_name: dot_card.name.clone(),
                    estimated_damage: d_est,
                    is_lethal: d_est >= target_enemy.current_health,
                };
            }
        }

        // 4. Multi-enemy AoE priority: in multi-enemy encounters (2+ alive), prioritize AoE damage spells
        if alive_enemies.len() >= 2 && !require_dot {
            if let Some(aoe) = affordable
                .iter()
                .find(|c| c.card_type == SpellCardType::DamageAoE)
            {
                let any_kill = alive_enemies.iter().any(|e| {
                    let d = self.estimate_damage(aoe, &player.blades, e);
                    d >= e.current_health
                });
                let first_est = self.estimate_damage(aoe, &player.blades, alive_enemies[0]);
                return CombatDecision::PlayCard {
                    slot_index: aoe.slot_index,
                    total_cards,
                    target_index: 0, // AoE always targets pedestal 0
                    spell_name: aoe.name.clone(),
                    estimated_damage: first_est,
                    is_lethal: any_kill,
                };
            }
        }

        // 5. Lethal check: can any affordable attack spell kill an enemy right now?
        for card in &affordable {
            if card.card_type == SpellCardType::DamageSingle
                || card.card_type == SpellCardType::DamageOverTime
            {
                if require_dot && card.card_type != SpellCardType::DamageOverTime {
                    continue;
                }
                for enemy in &alive_enemies {
                    let d_est = self.estimate_damage(card, &player.blades, enemy);
                    if d_est >= enemy.current_health {
                        return CombatDecision::PlayCard {
                            slot_index: card.slot_index,
                            total_cards,
                            target_index: enemy.pedestal_index,
                            spell_name: card.name.clone(),
                            estimated_damage: d_est,
                            is_lethal: true,
                        };
                    }
                }
            } else if card.card_type == SpellCardType::DamageAoE && !require_dot {
                // When 1 enemy alive, check if AoE kills it
                let d_est = self.estimate_damage(card, &player.blades, alive_enemies[0]);
                if d_est >= alive_enemies[0].current_health {
                    return CombatDecision::PlayCard {
                        slot_index: card.slot_index,
                        total_cards,
                        target_index: 0, // AoE always targets pedestal 0
                        spell_name: card.name.clone(),
                        estimated_damage: d_est,
                        is_lethal: true,
                    };
                }
            }
        }

        // 6. Buff stacking: if no lethal hit, but a Blade card is affordable and player has < 2 blades active
        if !forbid_blades && player.blades.len() < 2 {
            let mut blade_cards: Vec<&CombatCardCandidate> = affordable
                .iter()
                .filter(|c| c.card_type == SpellCardType::BladeSelf)
                .copied()
                .collect();
            if !blade_cards.is_empty() {
                blade_cards.sort_by(|a, b| {
                    b.buff_percentage
                        .partial_cmp(&a.buff_percentage)
                        .unwrap_or(core::cmp::Ordering::Equal)
                });
                let best_blade = blade_cards[0];
                return CombatDecision::PlayCard {
                    slot_index: best_blade.slot_index,
                    total_cards,
                    target_index: player.pedestal_index, // Self (pedestal 4)
                    spell_name: best_blade.name.clone(),
                    estimated_damage: 0.0,
                    is_lethal: false,
                };
            }
        }

        // 7. Trap stacking: if trap is affordable and target has no traps
        if !forbid_traps {
            let mut trap_cards: Vec<&CombatCardCandidate> = affordable
                .iter()
                .filter(|c| c.card_type == SpellCardType::TrapEnemy)
                .copied()
                .collect();
            if !trap_cards.is_empty() {
                trap_cards.sort_by(|a, b| {
                    b.buff_percentage
                        .partial_cmp(&a.buff_percentage)
                        .unwrap_or(core::cmp::Ordering::Equal)
                });
                let best_trap = trap_cards[0];
                if let Some(untrapped_enemy) = alive_enemies.iter().find(|e| e.traps.is_empty()) {
                    return CombatDecision::PlayCard {
                        slot_index: best_trap.slot_index,
                        total_cards,
                        target_index: untrapped_enemy.pedestal_index,
                        spell_name: best_trap.name.clone(),
                        estimated_damage: 0.0,
                        is_lethal: false,
                    };
                }
            }
        }

        // 8. Standard damage fallback: pick the highest damage card on the lowest-health enemy
        let mut damage_spells: Vec<&CombatCardCandidate> = affordable
            .iter()
            .filter(|c| {
                if require_dot {
                    c.card_type == SpellCardType::DamageOverTime
                } else {
                    c.card_type == SpellCardType::DamageSingle
                        || c.card_type == SpellCardType::DamageOverTime
                }
            })
            .copied()
            .collect();

        if !damage_spells.is_empty() {
            damage_spells.sort_by(|a, b| {
                b.base_damage
                    .partial_cmp(&a.base_damage)
                    .unwrap_or(core::cmp::Ordering::Equal)
            });
            let best_card = damage_spells[0];

            // Target lowest-health enemy
            let mut sorted_enemies = alive_enemies.clone();
            sorted_enemies.sort_by(|a, b| {
                a.current_health
                    .partial_cmp(&b.current_health)
                    .unwrap_or(core::cmp::Ordering::Equal)
            });
            let target_enemy = sorted_enemies[0];
            let d_est = self.estimate_damage(best_card, &player.blades, target_enemy);

            return CombatDecision::PlayCard {
                slot_index: best_card.slot_index,
                total_cards,
                target_index: target_enemy.pedestal_index,
                spell_name: best_card.name.clone(),
                estimated_damage: d_est,
                is_lethal: d_est >= target_enemy.current_health,
            };
        }

        // 9. If nothing productive can be done, pass to bank pips
        CombatDecision::Pass {
            reason: String::from("Passing to accumulate pips for high-value spell next round"),
        }
    }
}
