//! Shared, synthetic native/Rust conformance corpus. No input or game access.
#![cfg(feature = "deck_codec")]

use maelstrom_core::combat_geometry::{CombatCardGeometry, CombatGeometry, Point};
use maelstrom_core::deck_encoder::DeckEncoder;
use std::collections::{HashMap, HashSet};

const CORPUS: &str = include_str!(
    "../../../ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/RustParity/component-parity-v1.tsv"
);

fn integer(value: &str) -> i32 {
    value.parse().expect("fixture integer")
}

fn index(value: &str) -> Result<usize, String> {
    // Native takes signed indices; Rust's public API takes usize. Reject negative
    // fixture inputs at the representation boundary rather than wrapping them.
    usize::try_from(integer(value)).map_err(|_| "negative index".to_owned())
}

fn entries(value: &str) -> HashMap<String, i32> {
    if value == "-" {
        return HashMap::new();
    }
    value
        .split('|')
        .map(|entry| {
            let (name, count) = entry.split_once('=').expect("fixture map entry");
            (name.to_owned(), integer(count))
        })
        .collect()
}

#[test]
fn native_component_corpus_matches_rust() {
    assert_eq!(
        CORPUS.lines().next(),
        Some("# maelstrom-component-parity-v1")
    );
    let mut ids = HashSet::new();
    for line in CORPUS
        .lines()
        .filter(|line| !line.is_empty() && !line.starts_with('#'))
    {
        let row: Vec<_> = line.split('\t').collect();
        assert!(row.len() >= 3, "bad fixture row: {line}");
        let id = row[1];
        assert!(ids.insert(id), "duplicate fixture ID: {id}");
        let columns = match row[0] {
            "target" | "pedestal" => 5,
            "hand" | "codec" => 6,
            "enchant" => 8,
            "reject-target" | "reject-pedestal" => 3,
            "reject-hand" => 4,
            operation => panic!("unknown fixture operation: {operation}"),
        };
        assert_eq!(row.len(), columns, "{id}");
        match row[0] {
            "target" | "pedestal" => {
                let input = index(row[2]).unwrap();
                let actual = if row[0] == "target" {
                    CombatGeometry::calculate_target_coordinates(input)
                } else {
                    CombatGeometry::get_enemy_pedestal_coordinates(input)
                };
                assert_eq!(
                    actual.unwrap(),
                    Point::new(integer(row[3]), integer(row[4])),
                    "{id}"
                );
            }
            "hand" => assert_eq!(
                CombatGeometry::get_hand_card_coordinates(
                    index(row[2]).unwrap(),
                    index(row[3]).unwrap()
                )
                .unwrap(),
                Point::new(integer(row[4]), integer(row[5])),
                "{id}"
            ),
            "enchant" => {
                let enchant = CombatCardGeometry::new("Epic", integer(row[2]), integer(row[3]), 52);
                let spell = CombatCardGeometry::new(
                    "Tempest",
                    integer(row[4]),
                    integer(row[5]),
                    integer(row[6]),
                );
                let (first, second, card) =
                    CombatGeometry::calculate_enchant_geometry(&enchant, &spell);
                assert_eq!(first, Point::new(enchant.spell_x, enchant.spell_y), "{id}");
                assert_eq!(second, Point::new(spell.spell_x, spell.spell_y), "{id}");
                assert_eq!(
                    card,
                    CombatCardGeometry::new(
                        "Epic-Tempest",
                        integer(row[7]),
                        spell.spell_y,
                        spell.card_width
                    ),
                    "{id}"
                );
            }
            "reject-target" => assert!(
                index(row[2])
                    .and_then(CombatGeometry::calculate_target_coordinates)
                    .is_err(),
                "{id}"
            ),
            "reject-pedestal" => assert!(
                index(row[2])
                    .and_then(CombatGeometry::get_enemy_pedestal_coordinates)
                    .is_err(),
                "{id}"
            ),
            "reject-hand" => assert!(
                index(row[2])
                    .and_then(|hand| index(row[3])
                        .and_then(|total| CombatGeometry::get_hand_card_coordinates(hand, total)))
                    .is_err(),
                "{id}"
            ),
            "codec" => {
                let actual = DeckEncoder::decode(row[2]).unwrap();
                assert_eq!(actual.normal, entries(row[3]), "{id}: normal");
                assert_eq!(actual.tc, entries(row[4]), "{id}: tc");
                assert_eq!(actual.item, entries(row[5]), "{id}: item");
            }
            _ => unreachable!(),
        }
    }
    // Prevent an empty/truncated corpus from silently passing either consumer.
    assert_eq!(ids.len(), 56);
}
