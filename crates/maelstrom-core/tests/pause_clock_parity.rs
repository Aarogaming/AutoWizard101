#[path = "../src/gameplay_pause_clock.rs"]
mod gameplay_pause_clock;

use gameplay_pause_clock::{ClockError, GameplayPauseClock};
use std::collections::HashSet;

const CORPUS: &str = include_str!(
    "../../../ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/RustParity/pause-clock-v1.tsv"
);

fn error_name(error: Option<ClockError>) -> &'static str {
    match error {
        None => "-",
        Some(ClockError::InvalidPauseBudget) => "pause-budget",
        Some(ClockError::InvalidGameplayBudget) => "gameplay-budget",
        Some(ClockError::InvalidWallTime) => "wall-time",
        Some(ClockError::ContradictoryScene) => "scene",
    }
}

fn flag(value: &str) -> bool {
    value.parse().expect("fixture boolean")
}
fn number(value: &str) -> f64 {
    value.parse().expect("fixture number")
}

#[test]
fn shared_pause_clock_contract_matches_staged_rust() {
    assert_eq!(CORPUS.lines().next(), Some("# maelstrom-pause-clock-v1"));
    let mut ids = HashSet::new();
    let mut clock = None;
    let mut id = "";
    let mut count = 0;
    let mut case_count = 0;
    for (line_index, line) in CORPUS.lines().enumerate() {
        if line.is_empty() || line.starts_with('#') {
            continue;
        }
        let row: Vec<_> = line.split('\t').collect();
        match row[0] {
            "case" => {
                assert_eq!(row.len(), 5);
                assert!(clock.is_none() || case_count > 0, "empty valid case: {id}");
                id = row[1];
                assert!(ids.insert(id), "duplicate case: {id}");
                let created = GameplayPauseClock::new(number(row[2]), number(row[3]));
                assert_eq!(error_name(created.as_ref().err().copied()), row[4], "{id}");
                clock = created.ok();
                case_count = 0;
            }
            "obs" => {
                assert_eq!(row.len(), 11);
                let clock = clock.as_mut().expect("observation without valid clock");
                let before = clock.state();
                let result = clock.advance(number(row[1]), flag(row[2]), flag(row[3]));
                let location = format!("{id}, line {}", line_index + 1);
                assert_eq!(error_name(result.err()), row[4], "{location}");
                let actual = clock.state();
                if row[4] != "-" {
                    assert_eq!(
                        actual, before,
                        "invalid observation mutated state: {location}"
                    );
                }
                assert_eq!(actual.started, flag(row[5]), "{location}");
                assert_eq!(actual.is_loading, flag(row[6]), "{location}");
                assert_eq!(actual.active_ms, number(row[7]), "{location}");
                assert_eq!(actual.paused_ms, number(row[8]), "{location}");
                assert_eq!(actual.stop_reason.unwrap_or("-"), row[9], "{location}");
                assert_eq!(actual.gameplay_allowed, flag(row[10]), "{location}");
                count += 1;
                case_count += 1;
            }
            operation => panic!("unknown fixture operation: {operation}"),
        }
    }
    assert!(clock.is_none() || case_count > 0);
    assert_eq!(ids.len(), 25);
    assert_eq!(count, 51);
    assert_eq!(
        GameplayPauseClock::default().state(),
        GameplayPauseClock::new(45000.0, 75000.0).unwrap().state()
    );
}
