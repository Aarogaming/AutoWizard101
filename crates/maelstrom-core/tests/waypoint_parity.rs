//! Test the staged module without modifying the concurrently owned crate root.
#[path = "../src/waypoint_feedback.rs"]
mod waypoint_feedback;

use std::collections::HashSet;
use waypoint_feedback::{WaypointFeedback, WaypointPose};

const CORPUS: &str = include_str!("../../../ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/RustParity/waypoint-observations-v1.tsv");

fn number(value: &str) -> f64 {
    value.parse().expect("fixture number")
}

fn close(actual: f64, expected: &str, location: &str) {
    let expected = number(expected);
    assert!(
        if expected.is_nan() {
            actual.is_nan()
        } else if expected.is_infinite() {
            actual == expected
        } else {
            actual.is_finite() && (actual - expected).abs() <= 1e-9
        },
        "{location}: actual={actual}, expected={expected}"
    );
}

#[test]
fn shared_waypoint_observations_match_staged_rust_port() {
    assert_eq!(
        CORPUS.lines().next(),
        Some("# maelstrom-waypoint-observations-v1")
    );
    let mut ids = HashSet::new();
    let mut controller = None;
    let mut case_id = "";
    let mut count = 0;
    let mut case_count = 0;
    for (line_index, line) in CORPUS.lines().enumerate() {
        if line.is_empty() || line.starts_with('#') {
            continue;
        }
        let row: Vec<_> = line.split('\t').collect();
        match row[0] {
            "case" => {
                assert_eq!(row.len(), 6);
                assert!(controller.is_none() || case_count > 0);
                case_id = row[1];
                assert!(ids.insert(case_id), "duplicate case: {case_id}");
                controller = Some(WaypointFeedback::new(
                    WaypointPose {
                        x: number(row[2]),
                        y: number(row[3]),
                        z: number(row[4]),
                        heading: 0.0,
                        timestamp: 0,
                    },
                    row[5].parse().expect("maximum pulse"),
                ));
                case_count = 0;
            }
            "obs" => {
                assert_eq!(row.len(), 11);
                let actual = controller
                    .as_mut()
                    .expect("observation before case")
                    .observe(WaypointPose {
                        x: number(row[1]),
                        y: number(row[2]),
                        z: number(row[3]),
                        heading: number(row[4]),
                        timestamp: row[5].parse().expect("timestamp"),
                    });
                let location = format!("{case_id}, line {}", line_index + 1);
                assert_eq!(actual.state, row[6], "{location}");
                assert_eq!(actual.reason, row[7], "{location}");
                assert_eq!(
                    actual.pulse_ms,
                    row[8].parse::<i32>().expect("pulse"),
                    "{location}"
                );
                close(actual.distance, row[9], &format!("{location}: distance"));
                close(
                    actual.heading_error,
                    row[10],
                    &format!("{location}: heading"),
                );
                count += 1;
                case_count += 1;
            }
            operation => panic!("unknown fixture operation: {operation}"),
        }
    }
    assert!(case_count > 0);
    assert_eq!(ids.len(), 27);
    assert_eq!(count, 59);
}
