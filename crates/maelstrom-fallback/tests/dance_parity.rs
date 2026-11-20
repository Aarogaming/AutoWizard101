//! Synthetic labeled-observation contract shared with the native C# teacher.
use maelstrom_fallback::pet_dance::{DanceCue, DancePhase, PetDanceSequence};
use std::collections::HashSet;

const CORPUS: &str = include_str!("../../../ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/RustParity/dance-observations-v1.tsv");

fn cue(value: &str) -> DanceCue {
    match value {
        "unknown" => DanceCue::Unknown,
        "neutral" => DanceCue::Neutral,
        "up" => DanceCue::Up,
        "down" => DanceCue::Down,
        "left" => DanceCue::Left,
        "right" => DanceCue::Right,
        "fading" => DanceCue::Fading,
        _ => panic!("unknown fixture cue: {value}"),
    }
}

fn list(value: &str) -> Vec<&str> {
    if value == "-" {
        Vec::new()
    } else {
        value.split(',').collect()
    }
}

#[test]
fn shared_dance_observations_match_rust_after_every_frame() {
    assert_eq!(
        CORPUS.lines().next(),
        Some("# maelstrom-dance-observations-v1")
    );
    let mut ids = HashSet::new();
    let mut sequence = None;
    let mut case_id = "";
    let mut observations = 0;
    let mut case_observations = 0;
    for (line_index, line) in CORPUS.lines().enumerate() {
        if line.is_empty() || line.starts_with('#') {
            continue;
        }
        let row: Vec<_> = line.split('\t').collect();
        match row[0] {
            "case" => {
                assert_eq!(row.len(), 3);
                assert!(
                    sequence.is_none() || case_observations > 0,
                    "empty case: {case_id}"
                );
                case_id = row[1];
                assert!(ids.insert(case_id), "duplicate fixture case: {case_id}");
                sequence =
                    Some(PetDanceSequence::new(row[2].parse().expect("expected length")).unwrap());
                case_observations = 0;
            }
            "obs" => {
                assert_eq!(row.len(), 11, "line {}", line_index + 1);
                let actual = sequence.as_mut().expect("observation before case").observe(
                    row[1].parse().expect("timestamp"),
                    (
                        row[2].parse().expect("width"),
                        row[3].parse().expect("height"),
                    ),
                    DancePhase::parse(Some(row[4])),
                    cue(row[5]),
                    cue(row[6]),
                    row[7].parse().expect("opacity"),
                );
                let location = format!("{case_id}, line {}: {}", line_index + 1, actual.reason);
                assert_eq!(actual.cues, list(row[8]), "cues: {location}");
                assert_eq!(actual.proposals, list(row[9]), "proposals: {location}");
                assert_eq!(
                    actual.rejected,
                    row[10].parse::<bool>().expect("rejected"),
                    "rejected: {location}"
                );
                observations += 1;
                case_observations += 1;
            }
            operation => panic!("unknown fixture operation: {operation}"),
        }
    }
    assert!(case_observations > 0);
    assert_eq!(ids.len(), 15);
    assert_eq!(observations, 390);
}
