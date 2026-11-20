use maelstrom_core::behavior_tree::{
    build_w101_canonical_tree, ActionNode, BtNode, BtStatus, ConditionNode, ForceSuccess, Inverter,
    Parallel, ParallelPolicy, Retry, Selector, Sequence, W101Blackboard,
};

#[test]
fn test_integration_complex_questing_and_nested_recovery_pipeline() {
    let mut tree = build_w101_canonical_tree();
    let mut bb = W101Blackboard::new();

    // Setup initial state: Full health, full mana, 1 potion, quest goal with 2 waypoints
    bb.active_goal = Some(String::from("TurnIn_HeadmasterAmbrose"));
    bb.active_waypoints = vec![(100.0, 200.0, 0.0), (300.0, 400.0, 0.0)];

    // 1. Tick: Normal Quest Navigation starts
    let st1 = tree.tick(&mut bb);
    assert_eq!(st1, BtStatus::Running);
    assert_eq!(
        bb.proposal_channel.as_deref(),
        Some("PROPOSAL:StepWaypoint")
    );

    // 2. Incident: Character snagged on decorative world geometry (Odometry watchdog triggers is_stuck)
    bb.is_stuck = true;

    // Next tick: Stuck Recovery branch preempts Navigation
    let st2 = tree.tick(&mut bb);
    assert_eq!(st2, BtStatus::Success);
    assert_eq!(
        bb.proposal_channel.as_deref(),
        Some("PROPOSAL:UnstuckPulse")
    );
    assert!(!bb.is_stuck, "Unstuck maneuver must clear stuck flag");

    // 3. Resumed navigation after unstuck
    let st3 = tree.tick(&mut bb);
    assert_eq!(st3, BtStatus::Running);
    assert_eq!(
        bb.proposal_channel.as_deref(),
        Some("PROPOSAL:StepWaypoint")
    );

    // 4. Incident: Mana drops below threshold (0.15 < 0.20)
    bb.mana_pct = 0.15;

    // Next tick: Vital Recovery (Potion) branch preempts Navigation
    let st4 = tree.tick(&mut bb);
    assert_eq!(st4, BtStatus::Success);
    assert_eq!(bb.proposal_channel.as_deref(), Some("PROPOSAL:DrinkPotion"));
    assert_eq!(bb.mana_pct, 1.0, "Potion must restore mana to full");
    assert!(!bb.has_potion, "Potion reserve must be decremented");

    // 5. Incident: Ambushed by street mob (Combat initiates)
    bb.in_combat = true;

    // Next tick: Combat Interruption branch immediately takes highest priority
    let st5 = tree.tick(&mut bb);
    assert_eq!(st5, BtStatus::Running);
    assert_eq!(bb.proposal_channel.as_deref(), Some("PROPOSAL:CombatTurn"));

    // 6. Combat concludes: in_combat flips back to false
    bb.in_combat = false;

    // 7. Complete all waypoints and reach destination
    bb.active_waypoints.clear();
    let st6 = tree.tick(&mut bb);
    assert_eq!(st6, BtStatus::Success);
    assert_eq!(bb.proposal_channel.as_deref(), Some("PROPOSAL:Arrival"));
    assert_eq!(bb.active_goal, None, "Goal must clear upon arrival");

    // 8. With no active goals or emergencies, tree rests in safe Idle
    let st7 = tree.tick(&mut bb);
    assert_eq!(st7, BtStatus::Success);
    assert_eq!(bb.proposal_channel.as_deref(), Some("PROPOSAL:YieldIdle"));
}

#[test]
fn test_integration_reactive_preemption_resets_running_subtrees() {
    #[derive(Default)]
    struct TestState {
        pub count: usize,
        pub interrupt: bool,
    }

    let mut root = Selector::new("RootPriority");

    // Branch 1: Emergency Interrupt
    let mut emergency = Sequence::new("EmergencyBranch");
    emergency.add_child(ConditionNode::new("CheckInterrupt", |s: &TestState| {
        s.interrupt
    }));
    emergency.add_child(ActionNode::new("HandleEmergency", |s: &mut TestState| {
        s.count += 1000;
        BtStatus::Success
    }));
    root.add_child(emergency);

    // Branch 2: Normal Long-Running Task
    let mut normal = Sequence::new("NormalBranch");
    normal.add_child(ConditionNode::new("AlwaysReady", |_s: &TestState| true));
    normal.add_child(ActionNode::new("LongRunningStep", |s: &mut TestState| {
        s.count += 1;
        BtStatus::Running
    }));
    root.add_child(normal);

    let mut state = TestState::default();

    // Ticks 1 to 3: Normal task runs
    assert_eq!(root.tick(&mut state), BtStatus::Running);
    assert_eq!(state.count, 1);
    assert_eq!(root.tick(&mut state), BtStatus::Running);
    assert_eq!(state.count, 2);
    assert_eq!(root.tick(&mut state), BtStatus::Running);
    assert_eq!(state.count, 3);

    // Interrupt occurs!
    state.interrupt = true;

    // Tick 4: Emergency branch preempts normal task immediately
    assert_eq!(root.tick(&mut state), BtStatus::Success);
    assert_eq!(state.count, 1003);

    // Interrupt cleared
    state.interrupt = false;

    // Tick 5: Normal branch resumes and increments count
    assert_eq!(root.tick(&mut state), BtStatus::Running);
    assert_eq!(state.count, 1004);
}

#[test]
fn test_integration_decorators_and_parallel_policies() {
    #[derive(Default)]
    struct DecoratorState {
        pub attempts: usize,
        pub parallel_flag: bool,
    }

    let mut state = DecoratorState::default();

    // Test Retry + Inverter combination
    let mut retry_inv = Retry::new(
        "RetryInverter",
        3,
        Inverter::new(
            "Inv",
            ActionNode::new("AlwaysSuccess", |s: &mut DecoratorState| {
                s.attempts += 1;
                BtStatus::Success // Inverter flips to Failure, causing Retry to retry
            }),
        ),
    );

    // First tick: attempt 1 -> inverted to Failure -> Retry returns Running
    assert_eq!(retry_inv.tick(&mut state), BtStatus::Running);
    assert_eq!(state.attempts, 1);

    // Second tick: attempt 2 -> inverted to Failure -> Retry returns Running
    assert_eq!(retry_inv.tick(&mut state), BtStatus::Running);
    assert_eq!(state.attempts, 2);

    // Third tick: attempt 3 -> reaches max_attempts (3) -> Retry returns Failure
    assert_eq!(retry_inv.tick(&mut state), BtStatus::Failure);
    assert_eq!(state.attempts, 3);

    // Wrap in ForceSuccess: Failure converted to Success
    let mut force_success = ForceSuccess::new("Force", retry_inv);
    assert_eq!(force_success.tick(&mut state), BtStatus::Running); // restarts retry cycle

    // Test Parallel composite
    let mut parallel = Parallel::new("ParRequireAll", ParallelPolicy::RequireAll);
    parallel.add_child(ConditionNode::new("Check1", |_s: &DecoratorState| true));
    parallel.add_child(ActionNode::new("Action2", |s: &mut DecoratorState| {
        s.parallel_flag = true;
        BtStatus::Success
    }));

    assert_eq!(parallel.tick(&mut state), BtStatus::Success);
    assert!(state.parallel_flag);
}

#[test]
fn test_integration_opportunistic_detour_execution_and_preemption() {
    use maelstrom_core::costmap::DetourCandidate;
    use maelstrom_core::spatial_index::EntityKind;

    let mut tree = build_w101_canonical_tree();
    let mut bb = W101Blackboard::new();

    bb.active_goal = Some(String::from("MainlineQuest"));
    bb.active_waypoints = vec![(200.0, 0.0, 0.0)];
    bb.current_pose = (0.0, 0.0, 0.0);

    // 1. Approved detour discovered at (15.0, 10.0, 0.0)
    bb.active_detour = Some(DetourCandidate {
        entity_id: 42,
        entity_kind: EntityKind::Reagent,
        position: (15.0, 10.0, 0.0),
        delta_cost: 3.5,
        approved: true,
    });

    // Detour branch preempts mainline quest navigation
    let st1 = tree.tick(&mut bb);
    assert_eq!(st1, BtStatus::Running);
    assert_eq!(
        bb.proposal_channel.as_deref(),
        Some("PROPOSAL:SteerDetour:15.0,10.0")
    );

    // 2. Player approaches within interact radius (d <= 3.5)
    bb.current_pose = (14.0, 10.0, 0.0); // distance = 1.0 <= 3.5
    let st2 = tree.tick(&mut bb);
    assert_eq!(st2, BtStatus::Success);
    assert_eq!(
        bb.proposal_channel.as_deref(),
        Some("PROPOSAL:Interact:Detour:42")
    );
    assert!(
        bb.active_detour.is_none(),
        "Detour must be cleared after interaction"
    );

    // 3. Next tick resumes mainline quest navigation
    let st3 = tree.tick(&mut bb);
    assert_eq!(st3, BtStatus::Running);
    assert_eq!(
        bb.proposal_channel.as_deref(),
        Some("PROPOSAL:StepWaypoint")
    );

    // 4. Test preemption: while detouring, mob ambushes player into combat
    bb.active_detour = Some(DetourCandidate {
        entity_id: 43,
        entity_kind: EntityKind::SilverChest,
        position: (50.0, 10.0, 0.0),
        delta_cost: 4.0,
        approved: true,
    });
    bb.in_combat = true;

    // Combat branch takes precedence over detour branch immediately
    let st4 = tree.tick(&mut bb);
    assert_eq!(st4, BtStatus::Running);
    assert_eq!(bb.proposal_channel.as_deref(), Some("PROPOSAL:CombatTurn"));
}
