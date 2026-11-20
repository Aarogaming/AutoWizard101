//! `minigame_domain`: Deterministic rules, thresholds, energy costs, and zero-waste exit logic.
//!
//! Models Pet Pavilion mini-games, Fairegrounds recovery, Silver Chest puzzle locks,
//! and the exact Wizard101 damage stack calculation pipeline.
//!
//! Status (audit 2026-11-20): a draft transcription of `docs/W101_ONBOARDING_CONTEXT_CORPUS.md`, an unsourced
//! AI-generated specification, and nothing outside this module's tests calls it. A cross-check against the
//! official player guide found the energy costs, the 4/8 XP cap, the 70-snack threshold and the Promenade XP
//! value unsupported and the Maze input string contradicted (corrected below); the item docs say which.
//!
//! Follows `Facts ≠ Authority ≠ Actions`.

use alloc::string::String;
use alloc::vec::Vec;
use serde::{Deserialize, Serialize};

/// Pet age bracket determining energy expenditure per mini-game run. The official player guide lists the ranks
/// Baby, Teen, Adult, Ancient, Epic and Mega; `Ultra` is not on that page.
#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Hash, Serialize, Deserialize)]
pub enum PetAgeBracket {
    Baby,
    Teen,
    Adult,
    Ancient,
    Epic,
    Mega,
    Ultra,
}

impl PetAgeBracket {
    /// Draft energy cost per Pet Pavilion mini-game run for this pet age. The 2..14 progression comes from the
    /// unsourced onboarding page: the official guide says only that training games need Energy, and the live
    /// dance-entry code reads the cost the game displays (`txtEnergyCost`) instead of using a table.
    pub fn energy_cost(&self) -> u32 {
        match self {
            PetAgeBracket::Baby => 2,
            PetAgeBracket::Teen => 4,
            PetAgeBracket::Adult => 6,
            PetAgeBracket::Ancient => 8,
            PetAgeBracket::Epic => 10,
            PetAgeBracket::Mega => 12,
            PetAgeBracket::Ultra => 14,
        }
    }

    /// Draft maximum base Pet XP per run: 4, or 8 with Double Pet XP. Both numbers are unsourced, the bracket is
    /// ignored, and Double Pet XP is documented only as a past member event (February 2015, see
    /// `docs/W101_PET_SNACK_RESEARCH_20261108.md`), not a standing mode.
    pub fn max_base_xp(&self, is_double_pet_xp: bool) -> u32 {
        if is_double_pet_xp {
            8
        } else {
            4
        }
    }
}

/// Automation viability tier for mini-games.
#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Hash, Serialize, Deserialize)]
pub enum AutomationPriorityTier {
    Tier1Recommended,
    Tier2ViableFallback,
    Tier3Constrained,
    Tier4Deprioritized,
    Excluded,
}

/// Canonical Pet Pavilion mini-games.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
pub enum PetPavilionMiniGame {
    DanceGame,
    GobblerDrop,
    MazeGame,
    CannonGame,
    GrumpyGobblers,
    WayOfNinjaPig,
}

impl PetPavilionMiniGame {
    /// Returns the automation priority tier.
    pub fn priority_tier(&self) -> AutomationPriorityTier {
        match self {
            PetPavilionMiniGame::DanceGame => AutomationPriorityTier::Tier1Recommended,
            PetPavilionMiniGame::GobblerDrop | PetPavilionMiniGame::MazeGame => {
                AutomationPriorityTier::Tier2ViableFallback
            }
            PetPavilionMiniGame::CannonGame => AutomationPriorityTier::Tier3Constrained,
            PetPavilionMiniGame::GrumpyGobblers => AutomationPriorityTier::Tier4Deprioritized,
            PetPavilionMiniGame::WayOfNinjaPig => AutomationPriorityTier::Excluded,
        }
    }

    /// Whether this mini-game is recommended for unattended routine execution.
    pub fn is_recommended_for_automation(&self) -> bool {
        matches!(
            self.priority_tier(),
            AutomationPriorityTier::Tier1Recommended | AutomationPriorityTier::Tier2ViableFallback
        )
    }

    /// Primary input mechanics descriptor. The official guide lists arrow keys for the dance and drop games,
    /// arrow keys plus the mouse for the maze, and arrow keys, a power gauge and Space or Fire for the cannon
    /// games; no pet game is documented as using WASD.
    pub fn input_modality(&self) -> &'static str {
        match self {
            PetPavilionMiniGame::DanceGame => "DirectionalArrowKeys(Up, Down, Left, Right)",
            PetPavilionMiniGame::GobblerDrop => "LateralTracking(Left, Right)",
            PetPavilionMiniGame::MazeGame => "2DGridNavigation(ArrowKeysAndMouse)",
            PetPavilionMiniGame::CannonGame => "AimAngleAndPowerMeter",
            PetPavilionMiniGame::GrumpyGobblers => "SlingshotTrajectoryAngle",
            PetPavilionMiniGame::WayOfNinjaPig => "3DPlatformerLocomotion",
        }
    }
}

/// Goal mode for a Pet Pavilion mini-game run.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
pub enum PetGameGoal {
    /// Win counting for event milestones (e.g. Pet Promenade: 3 of 5 rounds in Dance Game).
    PromenadeWinOnly,
    /// Full Pet XP training (requires hitting 4 XP threshold).
    MaxPetXp,
}

/// Evaluator recommendation for mini-game execution and zero-waste exit.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub enum PetGameExitDecision {
    /// Goal not yet reached; continue active play.
    ContinuePlaying { current_progress: f64 },
    /// Score threshold reached; exit immediately to prevent zero-XP wasted runtime.
    ExitImmediately { reason: String, xp_awarded: u32 },
    /// In progress, but explicitly avoid prolonging pickups (e.g., +10s clocks).
    AvoidPickups {
        pickup_name: String,
        current_progress: f64,
    },
}

/// Zero-waste exit evaluator for the Pet Dance Game.
///
/// It sees only the rounds won. The official guide says the game ends after 5 rounds or 3 failures, so
/// `ContinuePlaying` can be returned for a game that has already ended in failure. The 3-round Promenade rule
/// and the XP values (2 for Promenade, 4 for a full game) are unsourced; the 4 is a constant, not a reading.
pub fn evaluate_dance_game_exit(rounds_won: u32, goal: PetGameGoal) -> PetGameExitDecision {
    match goal {
        PetGameGoal::PromenadeWinOnly => {
            if rounds_won >= 3 {
                PetGameExitDecision::ExitImmediately {
                    reason: String::from("Pet Promenade win condition met (3 of 5 rounds won)"),
                    xp_awarded: 2, // Partial XP on 3 rounds
                }
            } else {
                PetGameExitDecision::ContinuePlaying {
                    current_progress: rounds_won as f64 / 3.0,
                }
            }
        }
        PetGameGoal::MaxPetXp => {
            if rounds_won >= 5 {
                PetGameExitDecision::ExitImmediately {
                    reason: String::from("Maximum 4 XP threshold reached (5 of 5 rounds won)"),
                    xp_awarded: 4,
                }
            } else {
                PetGameExitDecision::ContinuePlaying {
                    current_progress: rounds_won as f64 / 5.0,
                }
            }
        }
    }
}

/// Zero-waste exit evaluator for the Pet Maze Game.
///
/// The 70-snack threshold, the "+10s Clock" pickup and the 4 XP are unsourced: the official guide says only to
/// collect as much food as possible before time runs out while avoiding the ghosts. Below the threshold the
/// result is always `AvoidPickups`, whatever the count.
pub fn evaluate_maze_game_exit(snacks_collected: u32) -> PetGameExitDecision {
    const MAZE_MAX_SNACK_THRESHOLD: u32 = 70;
    if snacks_collected >= MAZE_MAX_SNACK_THRESHOLD {
        PetGameExitDecision::ExitImmediately {
            reason: String::from(
                "Maximum 70 snacks collected (4 XP hard cap reached); stop to allow timer expiry",
            ),
            xp_awarded: 4,
        }
    } else {
        PetGameExitDecision::AvoidPickups {
            pickup_name: String::from("+10s Clock"),
            current_progress: snacks_collected as f64 / MAZE_MAX_SNACK_THRESHOLD as f64,
        }
    }
}

/// Zero-waste exit evaluator for Gobbler Drop (the official guide calls it the Pet Drop Game).
///
/// The comparison is inclusive, so a zero target is met by any score and exits at once. The 4 XP is a constant
/// and the target score has no source.
pub fn evaluate_gobbler_drop_exit(current_score: u32, target_score: u32) -> PetGameExitDecision {
    if current_score >= target_score {
        PetGameExitDecision::ExitImmediately {
            reason: String::from(
                "Score threshold reached; skip clock pickups to let timer expire cleanly",
            ),
            xp_awarded: 4,
        }
    } else {
        // Here `current_score < target_score`, so the target is positive and the ratio is below 1.
        PetGameExitDecision::ContinuePlaying {
            current_progress: current_score as f64 / target_score as f64,
        }
    }
}

/// Fairegrounds mini-games listed by the onboarding page as refilling mana wisps and potion bottles. The official
/// Mini Games page confirms that mini-games recharge the mana globe (and that gold and items stop after 20 games
/// in 24 hours while mana keeps refilling); it says nothing about potion bottles or about these six names.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
pub enum FairegroundsMiniGame {
    PotionMotion,
    DoodleDug,
    SkullRiders,
    HotAir,
    SorceryStones,
    Concentration,
}

impl FairegroundsMiniGame {
    /// Whether concluding this mini-game fully restores player mana and potion bottles (always `true`, without
    /// per-game evidence).
    pub fn refills_mana_and_potions_on_exit(&self) -> bool {
        true
    }
}

/// Silver Chest world puzzle mechanics.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
pub enum SilverChestPuzzleType {
    /// 2D tile rotation grid connecting circuit power source to target sink.
    ShockALock,
    /// Spacebar timing oscillator aligning moving indicator inside highlight bar.
    CatchAKeyLockpick,
}

/// Operational policy tracker for World Silver Chests.
///
/// `max_retries` is exhausted by that many failures, so the default of 2 allows two attempts in total (it matches
/// "fails twice", not one attempt plus two retries). The onboarding page's cooldown table of coordinates is not
/// implemented: only `cooldown_ttl_seconds` is carried, and `record_failure` returns `true` at the cap.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct SilverChestPolicy {
    pub max_retries: u32,
    pub current_attempts: u32,
    pub cooldown_ttl_seconds: u32,
}

impl Default for SilverChestPolicy {
    fn default() -> Self {
        Self {
            max_retries: 2,
            current_attempts: 0,
            cooldown_ttl_seconds: 300,
        }
    }
}

impl SilverChestPolicy {
    pub fn new(max_retries: u32, cooldown_ttl_seconds: u32) -> Self {
        Self {
            max_retries,
            current_attempts: 0,
            cooldown_ttl_seconds,
        }
    }

    /// Whether another attempt is permissible under the retry cap policy.
    pub fn can_retry(&self) -> bool {
        self.current_attempts < self.max_retries
    }

    /// Records an attempt failure; returns true if retry cap is exhausted and cooldown required.
    pub fn record_failure(&mut self) -> bool {
        self.current_attempts = self.current_attempts.saturating_add(1);
        !self.can_retry()
    }
}

/// Cantrip field magic chest types.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
pub enum CantripChestType {
    Solo,
    ThreeLock { required_wizards: usize },
}

impl CantripChestType {
    /// Evaluates if the chest can be safely unlocked based on nearby verified player count. Only the head count is
    /// modelled; the page's 5-second simultaneous-cast window is not, and the chest rules have no source.
    pub fn can_attempt(&self, nearby_verified_players: usize) -> bool {
        match self {
            CantripChestType::Solo => true,
            CantripChestType::ThreeLock { required_wizards } => {
                // Total wizards available = 1 (player) + nearby_verified_players
                (nearby_verified_players + 1) >= *required_wizards
            }
        }
    }
}

/// Input vector for a draft multiplicative damage pipeline. The stage order and the combination rules are not
/// verified against the game, and the numbers in this module's tests are invented fixtures.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct DamagePipelineInputs {
    pub base_damage: f64,
    pub enchant_damage: f64,
    pub gear_damage_percent: f64,
    pub flat_damage: f64,
    pub aura_multiplier: f64,
    pub charms_lifo: Vec<f64>,
    pub global_bubble_multiplier: f64,
    pub wards_lifo: Vec<f64>,
    pub critical_multiplier: f64,
    pub enemy_resist: f64,
    pub player_pierce: f64,
}

impl DamagePipelineInputs {
    pub fn new(base_damage: f64) -> Self {
        Self {
            base_damage,
            enchant_damage: 0.0,
            gear_damage_percent: 0.0,
            flat_damage: 0.0,
            aura_multiplier: 0.0,
            charms_lifo: Vec::new(),
            global_bubble_multiplier: 0.0,
            wards_lifo: Vec::new(),
            critical_multiplier: 1.0,
            enemy_resist: 0.0,
            player_pierce: 0.0,
        }
    }
}

/// Calculates spell damage through a draft multiplicative pipeline (the stage order is not verified against the
/// game; the module's tests compare the function with its own arithmetic on invented inputs):
///
/// $$\text{Damage} = \left\lfloor \left(\text{Base} + \text{Enchant}\right) \times \left(1.0 + \text{Gear\%}\right) + \text{FlatDmg} \right\rfloor \times \left(1.0 + \text{Aura}\right) \times \prod \left(1.0 + \text{Charm}\right) \times \left(1.0 + \text{Global}\right) \times \prod \left(1.0 + \text{Ward}\right) \times \text{CritMult} \times \left(1.0 - R_{\text{eff}}\right)$$
///
/// where `R_eff = max(Resist - Pierce, 0)` when `Resist > 0` and `R_eff = Resist` otherwise: pierce cancels
/// positive resist but never creates a boost, and a negative resist (an enemy weakness) amplifies the damage.
/// The last factor is floored at 0 and the result is rounded to the nearest integer.
///
/// Things the names suggest but the code does not do: `charms_lifo` and `wards_lifo` are multiplied together,
/// so the order of each list has no effect (no stack order is modelled), and a `critical_multiplier` below 1.0
/// is treated as 1.0.
pub fn calculate_pipeline_damage(inputs: &DamagePipelineInputs) -> u32 {
    if inputs.base_damage <= 0.0 {
        return 0;
    }

    // 1. Base + Enchant with Gear% and Flat Damage floor
    let base_plus_enchant = (inputs.base_damage + inputs.enchant_damage).max(0.0);
    let gear_factor = 1.0 + inputs.gear_damage_percent;
    let pre_buff_dmg = (base_plus_enchant * gear_factor + inputs.flat_damage).floor();

    if pre_buff_dmg <= 0.0 {
        return 0;
    }

    let mut running_damage = pre_buff_dmg;

    // 2. Aura multiplier
    running_damage *= 1.0 + inputs.aura_multiplier;

    // 3. Charms (Blades & Weaknesses in LIFO order)
    for charm in &inputs.charms_lifo {
        running_damage *= (1.0 + charm).max(0.0);
    }

    // 4. Global field bubble multiplier
    running_damage *= 1.0 + inputs.global_bubble_multiplier;

    // 5. Wards (Traps & Shields in LIFO order)
    for ward in &inputs.wards_lifo {
        running_damage *= (1.0 + ward).max(0.0);
    }

    // 6. Critical multiplier
    running_damage *= inputs.critical_multiplier.max(1.0);

    // 7. Enemy Resist mitigated by Player Pierce (or amplified by natural Boost)
    let effective_resist = if inputs.enemy_resist > 0.0 {
        (inputs.enemy_resist - inputs.player_pierce).max(0.0)
    } else {
        inputs.enemy_resist
    };
    let resist_factor = (1.0 - effective_resist).max(0.0);
    running_damage *= resist_factor;

    running_damage.round().max(0.0) as u32
}
