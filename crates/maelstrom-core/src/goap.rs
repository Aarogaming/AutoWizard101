//! `goap`: Goal-Oriented Action Planning engine for macro quest prerequisite chaining.
//! Synthesizes optimal, lowest-cost action sequences using A* state-space graph search.
//!
//! Decouples long-horizon quest progression (what to achieve) from tactical reactive
//! Behavior Tree execution (how to execute safely while arbitrating combat and vitals).
//! Adheres strictly to `Facts ≠ Authority ≠ Actions`.

use alloc::collections::BTreeMap;
use alloc::string::{String, ToString};
use alloc::vec::Vec;
use serde::{Deserialize, Serialize};

/// Value representation for a world state property atom.
#[derive(Debug, Clone, PartialEq, Eq, PartialOrd, Ord, Hash, Serialize, Deserialize)]
pub enum StateValue {
    Bool(bool),
    Int(i64),
    Str(String),
}

impl From<bool> for StateValue {
    fn from(b: bool) -> Self {
        StateValue::Bool(b)
    }
}

impl From<i64> for StateValue {
    fn from(i: i64) -> Self {
        StateValue::Int(i)
    }
}

impl From<i32> for StateValue {
    fn from(i: i32) -> Self {
        StateValue::Int(i as i64)
    }
}

impl From<&str> for StateValue {
    fn from(s: &str) -> Self {
        StateValue::Str(s.to_string())
    }
}

impl From<String> for StateValue {
    fn from(s: String) -> Self {
        StateValue::Str(s)
    }
}

/// Associative world state map holding semantic assertions.
#[derive(Debug, Clone, PartialEq, Eq, Default, Serialize, Deserialize)]
pub struct WorldState {
    pub values: BTreeMap<String, StateValue>,
}

impl WorldState {
    pub fn new() -> Self {
        Self {
            values: BTreeMap::new(),
        }
    }

    /// Sets or overwrites a world state property.
    pub fn set(&mut self, key: impl Into<String>, value: impl Into<StateValue>) -> &mut Self {
        self.values.insert(key.into(), value.into());
        self
    }

    /// Retrieves reference to state value for a given key.
    pub fn get(&self, key: &str) -> Option<&StateValue> {
        self.values.get(key)
    }

    /// Retrieves boolean property if present.
    pub fn get_bool(&self, key: &str) -> Option<bool> {
        match self.values.get(key) {
            Some(StateValue::Bool(b)) => Some(*b),
            _ => None,
        }
    }

    /// Retrieves integer property if present.
    pub fn get_int(&self, key: &str) -> Option<i64> {
        match self.values.get(key) {
            Some(StateValue::Int(i)) => Some(*i),
            _ => None,
        }
    }

    /// Retrieves string property if present.
    pub fn get_str(&self, key: &str) -> Option<&str> {
        match self.values.get(key) {
            Some(StateValue::Str(s)) => Some(s.as_str()),
            _ => None,
        }
    }

    /// Returns true if this state satisfies all assertions in `preconditions`.
    pub fn satisfies(&self, preconditions: &WorldState) -> bool {
        for (key, required_val) in &preconditions.values {
            match self.values.get(key) {
                Some(actual_val) if actual_val == required_val => {}
                _ => return false,
            }
        }
        true
    }

    /// Produces a new state by applying all effect mutations from `effects`.
    pub fn apply(&self, effects: &WorldState) -> WorldState {
        let mut new_state = self.clone();
        for (key, val) in &effects.values {
            new_state.values.insert(key.clone(), val.clone());
        }
        new_state
    }

    /// Computes the number of unsatisfied conditions in `goal`.
    ///
    /// This is a count, not a cost; `GoapPlanner::plan` scales it into an admissible bound.
    pub fn unsatisfied_count(&self, goal: &WorldState) -> usize {
        let mut count = 0;
        for (key, target_val) in &goal.values {
            match self.values.get(key) {
                Some(actual_val) if actual_val == target_val => {}
                _ => count += 1,
            }
        }
        count
    }
}

/// An atomic macro action defining preconditions, effect mutations, and execution cost.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct GoapAction {
    /// Human-readable action name identifier.
    pub name: String,
    /// Execution cost metric (e.g. travel distance, duration in seconds, resource cost).
    pub cost: f64,
    /// Required world state preconditions.
    pub preconditions: WorldState,
    /// World state mutations applied upon completion.
    pub effects: WorldState,
}

impl GoapAction {
    pub fn new(name: impl Into<String>, cost: f64) -> Self {
        Self {
            name: name.into(),
            cost: cost.max(0.1),
            preconditions: WorldState::new(),
            effects: WorldState::new(),
        }
    }

    /// Adds a precondition requirement.
    pub fn require(mut self, key: impl Into<String>, value: impl Into<StateValue>) -> Self {
        self.preconditions.set(key, value);
        self
    }

    /// Adds an effect mutation.
    pub fn effect(mut self, key: impl Into<String>, value: impl Into<StateValue>) -> Self {
        self.effects.set(key, value);
        self
    }

    /// Returns true if this action can be executed in the provided state.
    pub fn is_achievable(&self, current: &WorldState) -> bool {
        current.satisfies(&self.preconditions)
    }

    /// Applies this action's effects to the provided state.
    pub fn apply(&self, current: &WorldState) -> WorldState {
        current.apply(&self.effects)
    }
}

/// Search node in the A* state-space graph.
#[derive(Clone)]
struct SearchNode {
    state: WorldState,
    g_cost: f64,
    h_cost: f64,
    f_cost: f64,
    parent_idx: Option<usize>,
    action_idx: Option<usize>,
}

/// Goal-Oriented Action Planner utilizing A* state-space search.
pub struct GoapPlanner {
    pub max_iterations: usize,
}

impl Default for GoapPlanner {
    fn default() -> Self {
        Self {
            max_iterations: 2000,
        }
    }
}

impl GoapPlanner {
    pub fn new(max_iterations: usize) -> Self {
        Self {
            max_iterations: if max_iterations == 0 {
                2000
            } else {
                max_iterations
            },
        }
    }

    /// Plans the optimal sequence of actions bridging `current` to `goal`.
    /// Returns `Some(plan)` in execution order, or `None` if unreachable. `None` is also returned when the
    /// `max_iterations` budget runs out before a plan is found, and the two cases are not distinguished. A goal
    /// that `current` already satisfies returns `Some` of an empty plan. Optimality holds when the search
    /// finishes within the budget (see the heuristic below and its regression test).
    pub fn plan(
        &self,
        current: &WorldState,
        goal: &WorldState,
        available_actions: &[GoapAction],
    ) -> Option<Vec<GoapAction>> {
        // 1. Trivial check: already at goal
        if current.satisfies(goal) {
            return Some(Vec::new());
        }

        // 2. Early reachability prune: verify every unsatisfied goal key can be produced by at least one action
        // (a loop over the unmet atoms times the actions, so linear in their product, not constant time)
        for (key, target_val) in &goal.values {
            if current.get(key) != Some(target_val) {
                let can_produce = available_actions
                    .iter()
                    .any(|a| a.effects.get(key) == Some(target_val));
                if !can_produce {
                    // No action in the repertoire can possibly satisfy this goal atom
                    return None;
                }
            }
        }

        // Admissible heuristic: every action costs at least `min_cost` and fixes at most
        // `max_fix` goal atoms, so at least ceil(unmet / max_fix) actions are still needed.
        let min_cost = available_actions
            .iter()
            .map(|a| a.cost)
            .fold(f64::INFINITY, f64::min);
        let max_fix = available_actions
            .iter()
            .map(|a| {
                goal.values
                    .iter()
                    .filter(|(k, v)| a.effects.get(k.as_str()) == Some(*v))
                    .count()
            })
            .max()
            .unwrap_or(1)
            .max(1);
        let heuristic = |state: &WorldState| -> f64 {
            let unmet = state.unsatisfied_count(goal);
            unmet.div_ceil(max_fix) as f64 * min_cost
        };

        let initial_h = heuristic(current);
        let mut node_arena: Vec<SearchNode> = Vec::with_capacity(64);
        node_arena.push(SearchNode {
            state: current.clone(),
            g_cost: 0.0,
            h_cost: initial_h,
            f_cost: initial_h,
            parent_idx: None,
            action_idx: None,
        });

        // Open list storing indices into `node_arena`
        let mut open_indices: Vec<usize> = alloc::vec![0];
        // Closed list storing explored states and best g_cost to prune cycles
        let mut closed_states: Vec<(WorldState, f64)> = Vec::with_capacity(64);

        let mut iterations = 0;

        while !open_indices.is_empty() && iterations < self.max_iterations {
            iterations += 1;

            // Find lowest f_cost in open list (with tie-breaking on lowest h_cost)
            let mut best_open_pos = 0;
            let mut best_f = node_arena[open_indices[0]].f_cost;
            let mut best_h = node_arena[open_indices[0]].h_cost;

            for (pos, &node_idx) in open_indices.iter().enumerate().skip(1) {
                let n = &node_arena[node_idx];
                if n.f_cost < best_f || ((n.f_cost - best_f).abs() < 1e-6 && n.h_cost < best_h) {
                    best_f = n.f_cost;
                    best_h = n.h_cost;
                    best_open_pos = pos;
                }
            }

            let cur_node_idx = open_indices.swap_remove(best_open_pos);
            let cur_node = node_arena[cur_node_idx].clone();

            // Goal check
            if cur_node.state.satisfies(goal) {
                // Reconstruct action path by following parent indices backwards
                let mut path = Vec::new();
                let mut trace_idx = Some(cur_node_idx);

                while let Some(idx) = trace_idx {
                    if let Some(act_idx) = node_arena[idx].action_idx {
                        path.push(available_actions[act_idx].clone());
                    }
                    trace_idx = node_arena[idx].parent_idx;
                }

                path.reverse();
                return Some(path);
            }

            // Check if state was already explored with lower or equal g_cost
            let dominated = closed_states
                .iter()
                .any(|(s, g)| s == &cur_node.state && *g <= cur_node.g_cost);
            if dominated {
                continue;
            }
            closed_states.push((cur_node.state.clone(), cur_node.g_cost));

            // Expand adjacent actions
            for (action_idx, action) in available_actions.iter().enumerate() {
                if action.is_achievable(&cur_node.state) {
                    let next_state = action.apply(&cur_node.state);

                    // Check if successor state is already closed with lower cost
                    let already_closed = closed_states
                        .iter()
                        .any(|(s, g)| s == &next_state && *g <= (cur_node.g_cost + action.cost));
                    if already_closed {
                        continue;
                    }

                    let next_g = cur_node.g_cost + action.cost;
                    let next_h = heuristic(&next_state);
                    let next_f = next_g + next_h;

                    let new_node_idx = node_arena.len();
                    node_arena.push(SearchNode {
                        state: next_state,
                        g_cost: next_g,
                        h_cost: next_h,
                        f_cost: next_f,
                        parent_idx: Some(cur_node_idx),
                        action_idx: Some(action_idx),
                    });

                    open_indices.push(new_node_idx);
                }
            }
        }

        // Iteration budget exhausted or no path exists
        None
    }
}

/// Canonical deck profile archetype for Wizard101 combat encounters.
#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Hash, Serialize, Deserialize)]
pub enum DeckProfileTag {
    MobAoE,
    BossFeint,
    SchoolPrism,
    HealSupport,
}

impl DeckProfileTag {
    pub fn as_str(&self) -> &'static str {
        match self {
            DeckProfileTag::MobAoE => "mob_aoe",
            DeckProfileTag::BossFeint => "boss_feint",
            DeckProfileTag::SchoolPrism => "school_prism",
            DeckProfileTag::HealSupport => "heal_support",
        }
    }
}

impl From<DeckProfileTag> for StateValue {
    fn from(tag: DeckProfileTag) -> Self {
        StateValue::Str(tag.as_str().to_string())
    }
}

/// Generates a GOAP action ensuring the player has equipped the specified deck profile.
/// Enforces safe-zone preconditions: must not be in combat and must be safe from roaming mob aggro.
pub fn action_ensure_deck_profile(target_profile: DeckProfileTag) -> GoapAction {
    GoapAction::new(
        alloc::format!("EnsureDeckProfile:{}", target_profile.as_str()),
        5.0,
    )
    .require("in_combat", false)
    .require("safe_from_mobs", true)
    .effect("active_deck_profile", target_profile)
}

/// Helper to construct a sigil entry action requiring a specific deck profile.
pub fn action_enter_combat_sigil(sigil_name: &str, required_profile: DeckProfileTag) -> GoapAction {
    GoapAction::new(alloc::format!("EnterSigil:{}", sigil_name), 10.0)
        .require("at_sigil", sigil_name)
        .require("in_combat", false)
        .require("active_deck_profile", required_profile)
        .effect("in_combat", true)
}

/// Helper to construct a street mob pull action requiring a specific deck profile (usually MobAoE).
pub fn action_pull_patrol_mob(mob_name: &str, required_profile: DeckProfileTag) -> GoapAction {
    GoapAction::new(alloc::format!("PullMob:{}", mob_name), 8.0)
        .require("near_target_mob", mob_name)
        .require("in_combat", false)
        .require("active_deck_profile", required_profile)
        .effect("in_combat", true)
}

/// Helper action to retreat to a safe zone when dynamic mobs are too close.
pub fn action_retreat_to_safe_zone() -> GoapAction {
    GoapAction::new("RetreatToSafeZone", 4.0)
        .require("in_combat", false)
        .effect("safe_from_mobs", true)
        .effect("near_target_mob", "none")
}

/// Helper action to set a location marker (Page Down key).
pub fn action_set_marker(current_zone: &str) -> GoapAction {
    GoapAction::new(alloc::format!("SetMarker:{}", current_zone), 0.05)
        .require("at", current_zone)
        .require("in_combat", false)
        .effect("has_marker", true)
        .effect("marker_zone", current_zone)
}

/// Helper action to recall to a previously set marker (Page Up key).
pub fn action_recall_marker(target_zone: &str) -> GoapAction {
    GoapAction::new("RecallMarker:PageUp", 0.1)
        .require("in_combat", false)
        .require("has_marker", true)
        .require("marker_zone", target_zone)
        .effect("at", target_zone)
}

/// Helper action to fast-travel to Dorm/Castle (Home key).
pub fn action_fast_travel_home() -> GoapAction {
    GoapAction::new("FastTravel:Home", 0.1)
        .require("in_combat", false)
        .effect("at", "Dorm")
}

/// Helper action to fast-travel to world Commons hub (End key).
pub fn action_fast_travel_commons() -> GoapAction {
    GoapAction::new("FastTravel:Commons", 0.1)
        .require("in_combat", false)
        .effect("at", "Commons")
}

/// Helper action to consume emergency potion outside combat (Ctrl+O shortcut).
pub fn action_emergency_potion() -> GoapAction {
    GoapAction::new("EmergencyPotion:Ctrl+O", 0.05)
        .require("in_combat", false)
        .effect("health_full", true)
        .effect("mana_full", true)
}

/// Helper action for out-of-combat instant deck profile swap (Ctrl+K shortcut).
pub fn action_swap_deck_shortcut(target_profile: DeckProfileTag) -> GoapAction {
    GoapAction::new(
        alloc::format!("DeckSwapShortcut:{}", target_profile.as_str()),
        0.05,
    )
    .require("in_combat", false)
    .effect("active_deck_profile", target_profile)
}

/// Helper action for environmental multi-step puzzle affordances (e.g. Library Gauntlet puzzles).
pub fn action_interact_puzzle_affordance(
    puzzle_name: &str,
    step_id: &str,
    required_prereq: &str,
    unlocks_predicate: &str,
) -> GoapAction {
    let mut action = GoapAction::new(
        alloc::format!("SolvePuzzleAffordance:{}:{}", puzzle_name, step_id),
        2.0,
    )
    .require("in_combat", false);

    if !required_prereq.is_empty() {
        action = action.require(required_prereq, true);
    }
    action.effect(unlocks_predicate, true)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_world_state_satisfaction_and_application() {
        let mut s = WorldState::new();
        s.set("location", "Commons");
        s.set("mana", 100);
        s.set("has_potion", true);

        let mut req = WorldState::new();
        req.set("location", "Commons");
        req.set("has_potion", true);

        assert!(s.satisfies(&req));

        req.set("mana", 50); // Mismatch: actual is 100
        assert!(!s.satisfies(&req));

        let mut eff = WorldState::new();
        eff.set("location", "Ravenwood");
        eff.set("has_potion", false);

        let s2 = s.apply(&eff);
        assert_eq!(s2.get_str("location"), Some("Ravenwood"));
        assert_eq!(s2.get_bool("has_potion"), Some(false));
        assert_eq!(s2.get_int("mana"), Some(100)); // Retained unchanged
    }

    #[test]
    fn test_goap_planner_finds_linear_quest_plan() {
        let planner = GoapPlanner::default();

        let mut current = WorldState::new();
        current.set("at", "Commons");
        current.set("has_talking_point", false);
        current.set("quest_complete", false);

        let mut goal = WorldState::new();
        goal.set("quest_complete", true);

        let actions = alloc::vec![
            GoapAction::new("TravelToTower", 10.0)
                .require("at", "Commons")
                .effect("at", "HeadmasterTower"),
            GoapAction::new("EnterOffice", 5.0)
                .require("at", "HeadmasterTower")
                .effect("at", "AmbroseOffice"),
            GoapAction::new("TalkToAmbrose", 3.0)
                .require("at", "AmbroseOffice")
                .effect("has_talking_point", true),
            GoapAction::new("HandInQuest", 2.0)
                .require("has_talking_point", true)
                .effect("quest_complete", true),
        ];

        let plan = planner
            .plan(&current, &goal, &actions)
            .expect("Plan must resolve");
        assert_eq!(plan.len(), 4);
        assert_eq!(plan[0].name, "TravelToTower");
        assert_eq!(plan[1].name, "EnterOffice");
        assert_eq!(plan[2].name, "TalkToAmbrose");
        assert_eq!(plan[3].name, "HandInQuest");
    }

    #[test]
    fn test_goap_planner_selects_lowest_cost_branch() {
        let planner = GoapPlanner::default();

        let mut current = WorldState::new();
        current.set("at", "Commons");
        current.set("potion_filled", false);

        let mut goal = WorldState::new();
        goal.set("potion_filled", true);

        // Branch 1: Walk to vendor and buy potion (cost: 50.0 distance + gold)
        // Branch 2: Drink at nearby Commons fountain (cost: 15.0 distance)
        let actions = alloc::vec![
            GoapAction::new("WalkToVendor", 40.0)
                .require("at", "Commons")
                .effect("at", "PotionShop"),
            GoapAction::new("BuyPotion", 10.0)
                .require("at", "PotionShop")
                .effect("potion_filled", true),
            GoapAction::new("WalkToFountain", 10.0)
                .require("at", "Commons")
                .effect("at", "Fountain"),
            GoapAction::new("FillAtFountain", 5.0)
                .require("at", "Fountain")
                .effect("potion_filled", true),
        ];

        let plan = planner
            .plan(&current, &goal, &actions)
            .expect("Plan must resolve");
        assert_eq!(plan.len(), 2);
        // Planner must choose the cheaper fountain route (cost 15.0 vs 50.0)
        assert_eq!(plan[0].name, "WalkToFountain");
        assert_eq!(plan[1].name, "FillAtFountain");
    }

    #[test]
    fn test_goap_planner_is_optimal_when_one_action_satisfies_several_goal_atoms() {
        let planner = GoapPlanner::default();
        let current = WorldState::new();
        let mut goal = WorldState::new();
        goal.set("a", true);
        goal.set("b", true);

        // Two cheap single-effect actions (total 1.0) versus one combined action (1.5).
        // A heuristic that counts each unmet atom as 1.0 overestimates the remaining cost
        // when an action fixes several atoms at once and returns the 1.5 plan.
        let actions = alloc::vec![
            GoapAction::new("Combo", 1.5)
                .effect("a", true)
                .effect("b", true),
            GoapAction::new("SetA", 0.5).effect("a", true),
            GoapAction::new("SetB", 0.5).effect("b", true),
        ];

        let plan = planner
            .plan(&current, &goal, &actions)
            .expect("Plan must resolve");
        let total: f64 = plan.iter().map(|a| a.cost).sum();
        assert!(
            (total - 1.0).abs() < 1e-9,
            "expected the cost-1.0 plan, got cost {total} via {:?}",
            plan.iter().map(|a| a.name.as_str()).collect::<Vec<_>>()
        );
    }

    #[test]
    fn test_goap_planner_rejects_unreachable_goal_early() {
        let planner = GoapPlanner::default();

        let mut current = WorldState::new();
        current.set("at", "Commons");

        let mut goal = WorldState::new();
        goal.set("has_legendary_badge", true); // No action produces this

        let actions = alloc::vec![GoapAction::new("WalkToTower", 10.0)
            .require("at", "Commons")
            .effect("at", "HeadmasterTower"),];

        let plan = planner.plan(&current, &goal, &actions);
        assert!(plan.is_none(), "Unreachable goal must be rejected cleanly");
    }

    #[test]
    fn test_pre_battle_deck_profile_switching() {
        let planner = GoapPlanner::default();

        let mut current = WorldState::new();
        current.set("in_combat", false);
        current.set("safe_from_mobs", true);
        current.set("active_deck_profile", DeckProfileTag::MobAoE);
        current.set("at_sigil", "TowerOfGolem");

        let mut goal = WorldState::new();
        goal.set("in_combat", true);

        let actions = alloc::vec![
            action_ensure_deck_profile(DeckProfileTag::BossFeint),
            action_enter_combat_sigil("TowerOfGolem", DeckProfileTag::BossFeint),
        ];

        let plan = planner
            .plan(&current, &goal, &actions)
            .expect("Planner must synthesize pre-battle deck switch before sigil entry");
        assert_eq!(plan.len(), 2);
        assert_eq!(plan[0].name, "EnsureDeckProfile:boss_feint");
        assert_eq!(plan[1].name, "EnterSigil:TowerOfGolem");
    }

    #[test]
    fn test_pre_battle_deck_switch_requires_safe_from_mobs() {
        let planner = GoapPlanner::default();

        let mut current = WorldState::new();
        current.set("in_combat", false);
        current.set("safe_from_mobs", false); // Roaming mob is too close
        current.set("active_deck_profile", DeckProfileTag::MobAoE);
        current.set("near_target_mob", "SkeletalPirate");

        let mut goal = WorldState::new();
        goal.set("in_combat", true);

        // Cannot switch deck while safe_from_mobs is false unless we retreat first
        let actions = alloc::vec![
            action_retreat_to_safe_zone(),
            action_ensure_deck_profile(DeckProfileTag::BossFeint),
            GoapAction::new("ApproachTargetMob", 6.0)
                .require("safe_from_mobs", true)
                .effect("near_target_mob", "SkeletalPirate"),
            action_pull_patrol_mob("SkeletalPirate", DeckProfileTag::BossFeint),
        ];

        let plan = planner
            .plan(&current, &goal, &actions)
            .expect("Planner must retreat to safe zone before switching deck and pulling mob");
        assert_eq!(plan[0].name, "RetreatToSafeZone");
        assert_eq!(plan[1].name, "EnsureDeckProfile:boss_feint");
        assert_eq!(plan[2].name, "ApproachTargetMob");
        assert_eq!(plan[3].name, "PullMob:SkeletalPirate");
    }
}
