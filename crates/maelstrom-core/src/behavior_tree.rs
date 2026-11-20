//! `behavior_tree`: Deterministic, zero-dependency Hierarchical Behavior Tree engine.
//! Provides priority preemption, composite selectors, sequences, decorators, and
//! typed blackboard state evaluation for autonomous routine execution.
//! Adheres strictly to `Facts ≠ Authority ≠ Actions`.

use alloc::boxed::Box;
use alloc::string::String;
use alloc::vec::Vec;

/// Discrete execution status returned by every Behavior Tree node.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum BtStatus {
    /// The node successfully achieved its goal or passed its precondition.
    Success,
    /// The node failed its precondition or could not complete its action.
    Failure,
    /// The node has dispatched an asynchronous operation and remains in-flight.
    Running,
}

/// Core interface implemented by all composite, decorator, condition, and action nodes.
pub trait BtNode<B> {
    /// Ticks the node against the provided blackboard facts.
    fn tick(&mut self, blackboard: &mut B) -> BtStatus;

    /// Resets the internal execution state when preempted by a higher-priority branch.
    fn reset(&mut self);

    /// Name identifier of the node for tracing and inspection.
    fn name(&self) -> &str;
}

/// Priority Selector (Fallback) composite.
/// Evaluates children strictly in priority order (0 to N-1).
/// Returns `Success` or `Running` on the first non-failure.
/// If a previously running lower-priority child is preempted by a higher-priority child,
/// the lower-priority child is automatically reset.
pub struct Selector<B> {
    pub name: String,
    pub children: Vec<Box<dyn BtNode<B>>>,
    last_running_child: Option<usize>,
}

impl<B> Selector<B> {
    pub fn new(name: impl Into<String>) -> Self {
        Self {
            name: name.into(),
            children: Vec::new(),
            last_running_child: None,
        }
    }

    pub fn with_child(mut self, child: impl BtNode<B> + 'static) -> Self {
        self.children.push(Box::new(child));
        self
    }

    pub fn add_child(&mut self, child: impl BtNode<B> + 'static) {
        self.children.push(Box::new(child));
    }
}

impl<B> BtNode<B> for Selector<B> {
    fn tick(&mut self, blackboard: &mut B) -> BtStatus {
        for i in 0..self.children.len() {
            let status = self.children[i].tick(blackboard);

            match status {
                BtStatus::Running => {
                    // Preemption handling: if a different child was running previously, reset it
                    if let Some(prev) = self.last_running_child {
                        if prev != i && prev < self.children.len() {
                            self.children[prev].reset();
                        }
                    }
                    self.last_running_child = Some(i);
                    return BtStatus::Running;
                }
                BtStatus::Success => {
                    // Preemption handling: reset any lower-priority child that was previously running
                    if let Some(prev) = self.last_running_child {
                        if prev != i && prev < self.children.len() {
                            self.children[prev].reset();
                        }
                    }
                    self.last_running_child = None;
                    return BtStatus::Success;
                }
                BtStatus::Failure => {
                    // If this child was previously running and now failed, clear the running latch
                    if self.last_running_child == Some(i) {
                        self.last_running_child = None;
                    }
                    // Continue to next priority child
                }
            }
        }

        // All children failed
        self.last_running_child = None;
        BtStatus::Failure
    }

    fn reset(&mut self) {
        for child in &mut self.children {
            child.reset();
        }
        self.last_running_child = None;
    }

    fn name(&self) -> &str {
        &self.name
    }
}

/// Reactive Sequence composite.
/// Re-evaluates children in order from index 0 on every tick.
/// This guarantees that if a precondition (e.g. `in_combat`, `has_goal`) changes while an action is `Running`,
/// the sequence immediately aborts, resets the running action, and returns `Failure`.
/// Returns `Running` when the active child is running.
/// Returns `Success` only if all children succeed.
pub struct Sequence<B> {
    pub name: String,
    pub children: Vec<Box<dyn BtNode<B>>>,
    running_child: Option<usize>,
}

impl<B> Sequence<B> {
    pub fn new(name: impl Into<String>) -> Self {
        Self {
            name: name.into(),
            children: Vec::new(),
            running_child: None,
        }
    }

    pub fn with_child(mut self, child: impl BtNode<B> + 'static) -> Self {
        self.children.push(Box::new(child));
        self
    }

    pub fn add_child(&mut self, child: impl BtNode<B> + 'static) {
        self.children.push(Box::new(child));
    }
}

impl<B> BtNode<B> for Sequence<B> {
    fn tick(&mut self, blackboard: &mut B) -> BtStatus {
        let mut idx = 0;
        while idx < self.children.len() {
            let status = self.children[idx].tick(blackboard);

            match status {
                BtStatus::Running => {
                    // If a different child was running previously, reset it
                    if let Some(prev) = self.running_child {
                        if prev != idx && prev < self.children.len() {
                            self.children[prev].reset();
                        }
                    }
                    self.running_child = Some(idx);
                    return BtStatus::Running;
                }
                BtStatus::Failure => {
                    // Precondition or action failed: reset all children
                    self.reset();
                    return BtStatus::Failure;
                }
                BtStatus::Success => {
                    idx += 1;
                }
            }
        }

        // All children completed successfully
        self.reset();
        BtStatus::Success
    }

    fn reset(&mut self) {
        for child in &mut self.children {
            child.reset();
        }
        self.running_child = None;
    }

    fn name(&self) -> &str {
        &self.name
    }
}

/// Policy for Parallel composite evaluation.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ParallelPolicy {
    /// Requires all children to succeed. Fails immediately if any child fails.
    RequireAll,
    /// Requires at least one child to succeed. Fails only if all children fail.
    RequireOne,
}

/// Parallel composite.
/// Ticks every child, in order, on every tick (not concurrently, and a child that already succeeded is ticked
/// again). Two policies only, `RequireAll` and `RequireOne`; there are no numeric thresholds.
pub struct Parallel<B> {
    pub name: String,
    pub children: Vec<Box<dyn BtNode<B>>>,
    pub policy: ParallelPolicy,
}

impl<B> Parallel<B> {
    pub fn new(name: impl Into<String>, policy: ParallelPolicy) -> Self {
        Self {
            name: name.into(),
            children: Vec::new(),
            policy,
        }
    }

    pub fn with_child(mut self, child: impl BtNode<B> + 'static) -> Self {
        self.children.push(Box::new(child));
        self
    }

    pub fn add_child(&mut self, child: impl BtNode<B> + 'static) {
        self.children.push(Box::new(child));
    }
}

impl<B> BtNode<B> for Parallel<B> {
    fn tick(&mut self, blackboard: &mut B) -> BtStatus {
        let mut success_count = 0;
        let mut failure_count = 0;
        let mut running_count = 0;

        for child in &mut self.children {
            match child.tick(blackboard) {
                BtStatus::Success => success_count += 1,
                BtStatus::Failure => failure_count += 1,
                BtStatus::Running => running_count += 1,
            }
        }

        match self.policy {
            ParallelPolicy::RequireAll => {
                if failure_count > 0 {
                    self.reset();
                    BtStatus::Failure
                } else if running_count > 0 {
                    BtStatus::Running
                } else {
                    self.reset();
                    BtStatus::Success
                }
            }
            ParallelPolicy::RequireOne => {
                if success_count > 0 {
                    self.reset();
                    BtStatus::Success
                } else if running_count > 0 {
                    BtStatus::Running
                } else {
                    self.reset();
                    BtStatus::Failure
                }
            }
        }
    }

    fn reset(&mut self) {
        for child in &mut self.children {
            child.reset();
        }
    }

    fn name(&self) -> &str {
        &self.name
    }
}

/// Inverter decorator.
/// Flips `Success` to `Failure`, and `Failure` to `Success`. Passes `Running` through.
pub struct Inverter<B> {
    pub name: String,
    pub child: Box<dyn BtNode<B>>,
}

impl<B> Inverter<B> {
    pub fn new(name: impl Into<String>, child: impl BtNode<B> + 'static) -> Self {
        Self {
            name: name.into(),
            child: Box::new(child),
        }
    }
}

impl<B> BtNode<B> for Inverter<B> {
    fn tick(&mut self, blackboard: &mut B) -> BtStatus {
        match self.child.tick(blackboard) {
            BtStatus::Success => BtStatus::Failure,
            BtStatus::Failure => BtStatus::Success,
            BtStatus::Running => BtStatus::Running,
        }
    }

    fn reset(&mut self) {
        self.child.reset();
    }

    fn name(&self) -> &str {
        &self.name
    }
}

/// Retry decorator.
/// Re-executes the inner child up to `max_attempts` times on `Failure` before propagating `Failure`.
/// Between failed attempts it returns `Running` (the child is reset), so a parent `Selector` sees the child as
/// in flight until the last attempt fails.
pub struct Retry<B> {
    pub name: String,
    pub child: Box<dyn BtNode<B>>,
    pub max_attempts: usize,
    pub attempts: usize,
}

impl<B> Retry<B> {
    pub fn new(
        name: impl Into<String>,
        max_attempts: usize,
        child: impl BtNode<B> + 'static,
    ) -> Self {
        Self {
            name: name.into(),
            child: Box::new(child),
            max_attempts,
            attempts: 0,
        }
    }
}

impl<B> BtNode<B> for Retry<B> {
    fn tick(&mut self, blackboard: &mut B) -> BtStatus {
        let status = self.child.tick(blackboard);

        match status {
            BtStatus::Success => {
                self.attempts = 0;
                BtStatus::Success
            }
            BtStatus::Running => BtStatus::Running,
            BtStatus::Failure => {
                self.attempts += 1;
                if self.attempts >= self.max_attempts {
                    self.attempts = 0;
                    self.child.reset();
                    BtStatus::Failure
                } else {
                    self.child.reset();
                    BtStatus::Running
                }
            }
        }
    }

    fn reset(&mut self) {
        self.attempts = 0;
        self.child.reset();
    }

    fn name(&self) -> &str {
        &self.name
    }
}

/// ForceSuccess decorator.
/// Converts `Failure` into `Success`. Passes `Running` through.
pub struct ForceSuccess<B> {
    pub name: String,
    pub child: Box<dyn BtNode<B>>,
}

impl<B> ForceSuccess<B> {
    pub fn new(name: impl Into<String>, child: impl BtNode<B> + 'static) -> Self {
        Self {
            name: name.into(),
            child: Box::new(child),
        }
    }
}

impl<B> BtNode<B> for ForceSuccess<B> {
    fn tick(&mut self, blackboard: &mut B) -> BtStatus {
        match self.child.tick(blackboard) {
            BtStatus::Failure => BtStatus::Success,
            other => other,
        }
    }

    fn reset(&mut self) {
        self.child.reset();
    }

    fn name(&self) -> &str {
        &self.name
    }
}

/// Condition leaf node.
/// Evaluates a read-only predicate against blackboard facts.
/// Returns `Success` if true, `Failure` if false.
pub struct ConditionNode<B> {
    pub name: String,
    predicate: Box<dyn Fn(&B) -> bool + Send + Sync>,
}

impl<B> ConditionNode<B> {
    pub fn new<F>(name: impl Into<String>, predicate: F) -> Self
    where
        F: Fn(&B) -> bool + Send + Sync + 'static,
    {
        Self {
            name: name.into(),
            predicate: Box::new(predicate),
        }
    }
}

impl<B> BtNode<B> for ConditionNode<B> {
    fn tick(&mut self, blackboard: &mut B) -> BtStatus {
        if (self.predicate)(blackboard) {
            BtStatus::Success
        } else {
            BtStatus::Failure
        }
    }

    fn reset(&mut self) {}

    fn name(&self) -> &str {
        &self.name
    }
}

/// Action leaf node.
/// Executes state modification or emits an action proposal.
pub struct ActionNode<B> {
    pub name: String,
    action: Box<dyn FnMut(&mut B) -> BtStatus + Send + Sync>,
}

impl<B> ActionNode<B> {
    pub fn new<F>(name: impl Into<String>, action: F) -> Self
    where
        F: FnMut(&mut B) -> BtStatus + Send + Sync + 'static,
    {
        Self {
            name: name.into(),
            action: Box::new(action),
        }
    }
}

impl<B> BtNode<B> for ActionNode<B> {
    fn tick(&mut self, blackboard: &mut B) -> BtStatus {
        (self.action)(blackboard)
    }

    fn reset(&mut self) {}

    fn name(&self) -> &str {
        &self.name
    }
}

/// Domain typed blackboard for Wizard101 autonomous orchestration.
/// Contains verified observation facts and emitted action proposals.
/// Adheres to `Facts ≠ Authority ≠ Actions`.
#[derive(Debug, Clone, PartialEq)]
pub struct W101Blackboard {
    pub in_combat: bool,
    pub mana_pct: f64,
    pub health_pct: f64,
    pub has_potion: bool,
    pub is_stuck: bool,
    pub current_zone: String,
    pub active_goal: Option<String>,
    pub active_waypoints: Vec<(f64, f64, f64)>,
    pub current_pose: (f64, f64, f64),
    pub proposal_channel: Option<String>,
    pub combat_turn_action: Option<String>,
    pub active_detour: Option<crate::costmap::DetourCandidate>,
    pub detour_budget: f64,
    /// Appended to on every navigation and combat tick. Nothing in the tree or in `MaelstromAutonomyBridge`
    /// clears it, and the bridge clones all of it into every `AutonomyCycleResult`.
    pub status_log: Vec<String>,
}

impl W101Blackboard {
    pub fn new() -> Self {
        Self {
            in_combat: false,
            mana_pct: 1.0,
            health_pct: 1.0,
            has_potion: true,
            is_stuck: false,
            current_zone: String::from("WizardCity_Commons"),
            active_goal: None,
            active_waypoints: Vec::new(),
            current_pose: (0.0, 0.0, 0.0),
            proposal_channel: None,
            combat_turn_action: None,
            active_detour: None,
            detour_budget: 35.0,
            status_log: Vec::new(),
        }
    }
}

impl Default for W101Blackboard {
    fn default() -> Self {
        Self::new()
    }
}

/// Assembles the canonical W101 Recovery & Questing Priority Behavior Tree.
/// Priority order:
/// 1. Combat Interruption (Highest)
/// 2. Vital Recovery (Potion)
/// 3. Kinematic Stuck Recovery
/// 4. Opportunistic Detour
/// 5. Quest Navigation
/// 6. Idle Fallback (Lowest)
///
/// Several leaves only set `proposal_channel`: the branch-1 action never finishes, the potion action assumes it
/// worked (it writes the restored values itself), nothing outside tests sets `is_stuck`, and navigation runs no
/// path search (waypoints come from the caller).
pub fn build_w101_canonical_tree() -> Selector<W101Blackboard> {
    let mut root = Selector::new("W101RootSelector");

    // Branch 1: Combat Interruption (Priority 1)
    let mut combat_branch = Sequence::new("CombatBranch");
    combat_branch.add_child(ConditionNode::new(
        "CheckInCombat",
        |bb: &W101Blackboard| bb.in_combat,
    ));
    combat_branch.add_child(ActionNode::new(
        "ExecuteCombatTurn",
        |bb: &mut W101Blackboard| {
            if let Some(ref action) = bb.combat_turn_action {
                bb.proposal_channel = Some(alloc::format!("PROPOSAL:CombatTurn:{}", action));
            } else {
                bb.proposal_channel = Some(String::from("PROPOSAL:CombatTurn"));
            }
            bb.status_log.push(String::from("Combat turn proposed"));
            BtStatus::Running
        },
    ));
    root.add_child(combat_branch);

    // Branch 2: Vital Recovery (Priority 2)
    let mut potion_branch = Sequence::new("PotionRecoveryBranch");
    potion_branch.add_child(ConditionNode::new(
        "CheckNeedsPotion",
        |bb: &W101Blackboard| bb.mana_pct < 0.20 || bb.health_pct < 0.25,
    ));
    potion_branch.add_child(ConditionNode::new(
        "CheckHasPotion",
        |bb: &W101Blackboard| bb.has_potion,
    ));
    potion_branch.add_child(ActionNode::new(
        "DrinkPotionAction",
        |bb: &mut W101Blackboard| {
            bb.proposal_channel = Some(String::from("PROPOSAL:DrinkPotion"));
            bb.has_potion = false;
            bb.mana_pct = 1.0;
            bb.health_pct = 1.0;
            bb.status_log.push(String::from("Potion consumed"));
            BtStatus::Success
        },
    ));
    root.add_child(potion_branch);

    // Branch 3: Kinematic Stuck Recovery (Priority 3)
    let mut stuck_branch = Sequence::new("StuckRecoveryBranch");
    stuck_branch.add_child(ConditionNode::new("CheckIsStuck", |bb: &W101Blackboard| {
        bb.is_stuck
    }));
    stuck_branch.add_child(ActionNode::new(
        "ExecuteUnstuckPulse",
        |bb: &mut W101Blackboard| {
            bb.proposal_channel = Some(String::from("PROPOSAL:UnstuckPulse"));
            bb.is_stuck = false;
            bb.status_log.push(String::from("Unstuck pulse executed"));
            BtStatus::Success
        },
    ));
    root.add_child(stuck_branch);

    // Branch 4: Opportunistic Detour (Priority 4)
    let mut detour_branch = Sequence::new("OpportunisticDetourBranch");
    detour_branch.add_child(ConditionNode::new(
        "CheckHasApprovedDetour",
        |bb: &W101Blackboard| bb.active_detour.as_ref().is_some_and(|d| d.approved),
    ));
    detour_branch.add_child(ActionNode::new(
        "ExecuteDetourHarvest",
        |bb: &mut W101Blackboard| {
            if let Some(ref detour) = bb.active_detour {
                let dx = bb.current_pose.0 - detour.position.0;
                let dy = bb.current_pose.1 - detour.position.1;
                let dz = bb.current_pose.2 - detour.position.2;
                let dist = (dx * dx + dy * dy + dz * dz).sqrt();

                if dist <= 3.5 {
                    bb.proposal_channel = Some(alloc::format!(
                        "PROPOSAL:Interact:Detour:{}",
                        detour.entity_id
                    ));
                    bb.status_log
                        .push(alloc::format!("Detour node {} harvested", detour.entity_id));
                    bb.active_detour = None;
                    BtStatus::Success
                } else {
                    bb.proposal_channel = Some(alloc::format!(
                        "PROPOSAL:SteerDetour:{:.1},{:.1}",
                        detour.position.0,
                        detour.position.1
                    ));
                    BtStatus::Running
                }
            } else {
                BtStatus::Failure
            }
        },
    ));
    root.add_child(detour_branch);

    // Branch 5: Quest Navigation (Priority 5)
    let mut nav_branch = Sequence::new("QuestNavigationBranch");
    nav_branch.add_child(ConditionNode::new("CheckHasGoal", |bb: &W101Blackboard| {
        bb.active_goal.is_some()
    }));
    nav_branch.add_child(ActionNode::new(
        "FollowNavMeshPath",
        |bb: &mut W101Blackboard| {
            if bb.active_waypoints.is_empty() {
                bb.active_goal = None;
                bb.proposal_channel = Some(String::from("PROPOSAL:Arrival"));
                BtStatus::Success
            } else {
                bb.proposal_channel = Some(String::from("PROPOSAL:StepWaypoint"));
                bb.status_log.push(String::from("Stepping waypoint"));
                BtStatus::Running
            }
        },
    ));
    root.add_child(nav_branch);

    // Branch 6: Idle Fallback (Priority 6)
    root.add_child(ActionNode::new(
        "YieldIdleAction",
        |bb: &mut W101Blackboard| {
            bb.proposal_channel = Some(String::from("PROPOSAL:YieldIdle"));
            BtStatus::Success
        },
    ));

    root
}

#[cfg(test)]
mod tests {
    use super::*;

    #[derive(Default)]
    struct SimpleBb {
        pub counter: usize,
    }

    #[test]
    fn test_selector_fallback_and_first_success() {
        let mut selector = Selector::new("TestSelector")
            .with_child(ConditionNode::new("Fail1", |_bb: &SimpleBb| false))
            .with_child(ConditionNode::new("Success2", |_bb: &SimpleBb| true))
            .with_child(ConditionNode::new("Fail3", |_bb: &SimpleBb| false));

        let mut bb = SimpleBb::default();
        assert_eq!(selector.tick(&mut bb), BtStatus::Success);
    }

    #[test]
    fn test_sequence_short_circuit_on_failure() {
        let mut sequence = Sequence::new("TestSequence")
            .with_child(ConditionNode::new("Pass1", |_bb: &SimpleBb| true))
            .with_child(ConditionNode::new("Fail2", |_bb: &SimpleBb| false))
            .with_child(ActionNode::new("NeverRun", |bb: &mut SimpleBb| {
                bb.counter += 100;
                BtStatus::Success
            }));

        let mut bb = SimpleBb::default();
        assert_eq!(sequence.tick(&mut bb), BtStatus::Failure);
        assert_eq!(
            bb.counter, 0,
            "Action must not execute after sequence failure"
        );
    }

    #[test]
    fn test_inverter_decorator() {
        let mut inv_true =
            Inverter::new("InvTrue", ConditionNode::new("True", |_bb: &SimpleBb| true));
        let mut inv_false = Inverter::new(
            "InvFalse",
            ConditionNode::new("False", |_bb: &SimpleBb| false),
        );

        let mut bb = SimpleBb::default();
        assert_eq!(inv_true.tick(&mut bb), BtStatus::Failure);
        assert_eq!(inv_false.tick(&mut bb), BtStatus::Success);
    }

    #[test]
    fn test_retry_decorator() {
        let mut retry = Retry::new(
            "TestRetry",
            3,
            ActionNode::new("CountAttempts", |bb: &mut SimpleBb| {
                bb.counter += 1;
                if bb.counter >= 3 {
                    BtStatus::Success
                } else {
                    BtStatus::Failure
                }
            }),
        );

        let mut bb = SimpleBb::default();
        // First tick: attempt 1 fails, returns Running
        assert_eq!(retry.tick(&mut bb), BtStatus::Running);
        assert_eq!(bb.counter, 1);

        // Second tick: attempt 2 fails, returns Running
        assert_eq!(retry.tick(&mut bb), BtStatus::Running);
        assert_eq!(bb.counter, 2);

        // Third tick: attempt 3 succeeds, returns Success
        assert_eq!(retry.tick(&mut bb), BtStatus::Success);
        assert_eq!(bb.counter, 3);
    }

    #[test]
    fn test_parallel_require_all_and_require_one() {
        let mut par_all = Parallel::new("ParAll", ParallelPolicy::RequireAll)
            .with_child(ConditionNode::new("Pass1", |_bb: &SimpleBb| true))
            .with_child(ConditionNode::new("Pass2", |_bb: &SimpleBb| true));

        let mut par_one = Parallel::new("ParOne", ParallelPolicy::RequireOne)
            .with_child(ConditionNode::new("Fail1", |_bb: &SimpleBb| false))
            .with_child(ConditionNode::new("Pass2", |_bb: &SimpleBb| true));

        let mut bb = SimpleBb::default();
        assert_eq!(par_all.tick(&mut bb), BtStatus::Success);
        assert_eq!(par_one.tick(&mut bb), BtStatus::Success);
    }

    #[test]
    fn test_priority_preemption_combat_aborts_navigation() {
        let mut tree = build_w101_canonical_tree();
        let mut bb = W101Blackboard::new();

        // 1. Initial state: Has active quest goal with waypoints -> Navigation runs
        bb.active_goal = Some(String::from("Quest_OldeTown"));
        bb.active_waypoints = alloc::vec![(10.0, 20.0, 0.0), (30.0, 40.0, 0.0)];

        let status1 = tree.tick(&mut bb);
        assert_eq!(status1, BtStatus::Running);
        assert_eq!(
            bb.proposal_channel.as_deref(),
            Some("PROPOSAL:StepWaypoint")
        );

        // 2. Interruption: Player pulled into combat!
        bb.in_combat = true;

        // On next tick, Priority Selector prioritizes Combat over Navigation immediately!
        let status2 = tree.tick(&mut bb);
        assert_eq!(status2, BtStatus::Running);
        assert_eq!(bb.proposal_channel.as_deref(), Some("PROPOSAL:CombatTurn"));

        // 3. Combat finishes: in_combat flips back to false
        bb.in_combat = false;

        // Next tick resumes Navigation branch
        let status3 = tree.tick(&mut bb);
        assert_eq!(status3, BtStatus::Running);
        assert_eq!(
            bb.proposal_channel.as_deref(),
            Some("PROPOSAL:StepWaypoint")
        );
    }
}
