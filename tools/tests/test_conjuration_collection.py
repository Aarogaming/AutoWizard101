"""Small stdlib tests with generated PNGs; no real gameplay is performed."""

from copy import deepcopy
import hashlib
import json
from pathlib import Path
import struct
import sys
import tempfile
import unittest
from unittest.mock import patch
import zlib

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from conjuration_collection import (
    STAGES,
    build_draft,
    check_collection,
    png_metadata,
    prepare_recordings,
)

ROOT = Path(__file__).resolve().parents[2]


def png(seed=0, alpha=False):
    def chunk(kind, data):
        return (
            struct.pack(">I", len(data))
            + kind
            + data
            + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)
        )

    width, height = 64, 48
    rows = []
    for y in range(height):
        row = bytearray(b"\0")
        for x in range(width):
            row.extend(
                ((seed + x * 2) % 256, (seed + y * 3) % 256, (x + y + seed) % 256)
            )
            if alpha:
                row.append(128 if x == 0 else 255)
        rows.append(row)
    return (
        b"\x89PNG\r\n\x1a\n"
        + chunk(
            b"IHDR",
            struct.pack(">IIBBBBB", width, height, 8, 6 if alpha else 2, 0, 0, 0),
        )
        + chunk(b"IDAT", zlib.compress(b"".join(rows)))
        + chunk(b"IEND", b"")
    )


def make_recording(parent, name="episode-a", count=3, same=False, alpha=False, seed=0):
    directory = Path(parent) / name
    directory.mkdir()
    frames = []
    for index in range(count):
        raw = png(seed if same else seed + index, alpha)
        file = f"frame-{index:04d}.png"
        (directory / file).write_bytes(raw)
        frames.append(
            {
                "File": file,
                "TimeMs": index * 60,
                "Width": 64,
                "Height": 48,
                "Sha256": hashlib.sha256(raw).hexdigest().upper(),
                "State": "untrusted-old-label",
                "Region": None,
            }
        )
    data = {
        "Version": 1,
        "Id": name,
        "Workspace": "wizard101-conjuration",
        "StartedUtc": "2026-11-09T12:00:00Z",
        "Complete": True,
        "Frames": frames,
    }
    path = directory / "recording.json"
    path.write_text(json.dumps(data), encoding="utf-8")
    return path


def qualified_metadata(episode, split="calibration"):
    # Explicit mocked declarations, not a claim these synthetic test pixels are real.
    episode.update(
        split=split,
        surface_kind="client",
        ui_scale="test-only",
        timestamp_basis="source-capture-ms",
    )


def complete_mock_draft(directory):
    paths = [
        make_recording(directory, "cal", len(STAGES), seed=0),
        make_recording(directory, "held", len(STAGES), seed=100),
    ]
    draft, sources = build_draft(paths, evidence_kind="recorded")
    for episode, split in zip(draft["episodes"], ("calibration", "held_out")):
        qualified_metadata(episode, split)
        for frame, stage in zip(episode["frames"], STAGES):
            frame.update(stage=stage, review="manual")
            if stage not in {"instructions", "result"}:
                states = {
                    "hidden": ("hidden", "hidden"),
                    "flip": ("face_up", "hidden"),
                    "removed": ("removed", "removed"),
                    "new_board": ("hidden", "hidden"),
                }.get(stage, ("face_up", "face_up"))
                cards = [
                    {
                        "slot": str(i),
                        "x": 20 + 24 * i,
                        "y": 24,
                        "state": state,
                        "identity": (
                            ("B" if i and stage == "mismatch" else "A")
                            if state == "face_up"
                            else None
                        ),
                    }
                    for i, state in enumerate(states)
                ]
                frame["board"] = {
                    "board_id": episode["episode_id"]
                    + ("-new" if stage == "new_board" else "-board"),
                    "revision": frame["recording_frame_index"],
                    "phase": "preview" if stage == "preview" else "active",
                    "cards": cards,
                }
                frame["group_size"] = 2
            if stage in {"flip", "match", "mismatch"}:
                rid = "r-" + frame["frame_id"]
                episode["input_receipts"].append(
                    {
                        "receipt_id": rid,
                        "time_ms": frame["time_ms"] - 10,
                        "proposal_id": None,
                        "status": "dispatched",
                        "origin": "manual_observation",
                        "action": "left click",
                    }
                )
                frame["receipt_ids"] = [rid]
    draft["rules"] = {
        "removed_state": "positive_marker",
        "evidence_frame_ids": [draft["episodes"][0]["frames"][0]["frame_id"]],
    }
    return draft, sources


def clone_calibration_episode(draft, sources):
    clone = deepcopy(draft["episodes"][0])
    clone["episode_id"] = "cal-copy"
    raw = json.loads(sources[clone["recording"]["file"]])
    raw["Id"] = clone["episode_id"]
    payload = json.dumps(raw).encode()
    clone["recording"].update(
        file="metadata/cal-copy.json", sha256=hashlib.sha256(payload).hexdigest()
    )
    sources["metadata/cal-copy.json"] = payload
    for frame in clone["frames"]:
        frame["frame_id"] = "copy-" + frame["frame_id"]
        if frame["board"] is not None:
            frame["board"]["board_id"] = "copy-" + frame["board"]["board_id"]
            frame["board"]["revision"] += 100
    draft["episodes"].append(clone)
    return clone


class ConjurationCollectionTests(unittest.TestCase):
    def test_prepare_keeps_originals_and_starts_an_unknown_unassigned_draft(self):
        with tempfile.TemporaryDirectory() as directory:
            path = make_recording(directory)
            original = path.read_bytes()
            output = Path(directory) / "draft"
            report = prepare_recordings([path], output)
            annotations = json.loads((output / "annotations.json").read_text())
            episode = annotations["episodes"][0]
            self.assertEqual(path.read_bytes(), original)
            self.assertEqual(
                (output / episode["recording"]["file"]).read_bytes(), original
            )
            self.assertEqual(episode["declared_evidence_kind"], "unknown")
            self.assertEqual(episode["split"], "unassigned")
            self.assertEqual(episode["input_receipts"], [])
            self.assertTrue(
                all(
                    frame["stage"] == "unknown"
                    and frame["board"] is None
                    and frame["group_size"] is None
                    for frame in episode["frames"]
                )
            )
            self.assertEqual(report, check_collection(annotations, output))
            self.assertFalse(report["collection_checklist_met"])
            self.assertFalse(report["runtime_enabled"])
            self.assertEqual(report["desktop_dispatch_count"], 0)

    def test_duplicates_across_recordings_count_once_and_never_supply_new_states(self):
        with tempfile.TemporaryDirectory() as directory:
            a = make_recording(directory, "a", 2, same=True)
            b = make_recording(directory, "b", 3, same=True)
            draft, sources = build_draft([a, b], evidence_kind="recorded")
            for episode in draft["episodes"]:
                qualified_metadata(episode)
                for frame in episode["frames"]:
                    frame.update(stage="instructions", review="manual")
            report = check_collection(draft, snapshots=sources)
            self.assertEqual(
                (
                    report["source_frame_count"],
                    report["unique_image_count"],
                    report["duplicate_frame_count"],
                ),
                (5, 1, 4),
            )
            self.assertEqual(
                report["declared_recorded_manual_coverage"]["calibration"][
                    "instructions"
                ],
                1,
            )
            self.assertTrue(
                any(
                    gap["reason"]
                    == "one-unique-image-cannot-demonstrate-card-transitions"
                    for gap in report["gaps"]
                )
            )
            self.assertFalse(report["collection_checklist_met"])

    def test_split_leakage_is_excluded_even_if_held_out_copy_is_unreviewed(self):
        with tempfile.TemporaryDirectory() as directory:
            a = make_recording(directory, "a", 1)
            b = make_recording(directory, "b", 1)
            draft, sources = build_draft([a, b], evidence_kind="recorded")
            qualified_metadata(draft["episodes"][0], "calibration")
            qualified_metadata(draft["episodes"][1], "held_out")
            draft["episodes"][0]["frames"][0].update(
                stage="instructions", review="manual"
            )
            report = check_collection(draft, snapshots=sources)
            self.assertIn("split-leakage", report["excluded_hashes"].values())
            self.assertEqual(
                report["declared_recorded_manual_coverage"]["calibration"][
                    "instructions"
                ],
                0,
            )

    def test_synthetic_or_recognizer_labels_cannot_satisfy_manual_recorded_coverage(
        self,
    ):
        with tempfile.TemporaryDirectory() as directory:
            path = make_recording(directory)
            for kind, review in (
                ("synthetic", "manual"),
                ("recorded", "recognizer"),
                ("unknown", "manual"),
            ):
                with self.subTest(kind=kind, review=review):
                    draft, sources = build_draft([path], evidence_kind=kind)
                    qualified_metadata(draft["episodes"][0])
                    for frame in draft["episodes"][0]["frames"]:
                        frame.update(stage="instructions", review=review)
                    report = check_collection(draft, snapshots=sources)
                    self.assertEqual(
                        sum(
                            report["declared_recorded_manual_coverage"][
                                "calibration"
                            ].values()
                        ),
                        0,
                    )
                    self.assertFalse(
                        report["declaration_is_independent_provenance_proof"]
                    )

    def test_known_synthetic_hash_cannot_be_redeclared_as_recorded_elsewhere(self):
        with tempfile.TemporaryDirectory() as directory:
            a = make_recording(directory, "a", 1)
            b = make_recording(directory, "b", 1)
            draft, sources = build_draft([a, b], evidence_kind="recorded")
            for episode in draft["episodes"]:
                qualified_metadata(episode)
                episode["frames"][0].update(stage="instructions", review="manual")
            draft["episodes"][1]["declared_evidence_kind"] = "synthetic"
            report = check_collection(draft, snapshots=sources)
            self.assertIn(
                "synthetic-source-cannot-satisfy-recorded-coverage",
                report["excluded_hashes"].values(),
            )
            self.assertEqual(
                report["declared_recorded_manual_coverage"]["calibration"][
                    "instructions"
                ],
                0,
            )

    def test_conflicting_stage_labels_on_identical_pixels_are_excluded(self):
        with tempfile.TemporaryDirectory() as directory:
            path = make_recording(directory, count=2, same=True)
            draft, sources = build_draft([path], evidence_kind="recorded")
            episode = draft["episodes"][0]
            qualified_metadata(episode)
            for frame, stage in zip(episode["frames"], ("instructions", "preview")):
                frame.update(stage=stage, review="manual")
            report = check_collection(draft, snapshots=sources)
            self.assertIn(
                "conflicting-labels-on-identical-pixels",
                report["excluded_hashes"].values(),
            )
            self.assertEqual(
                sum(
                    report["declared_recorded_manual_coverage"]["calibration"].values()
                ),
                0,
            )

    def test_alpha_and_clock_origin_are_not_inferred_from_available_metadata(self):
        with tempfile.TemporaryDirectory() as directory:
            path = make_recording(directory, alpha=True)
            draft, sources = build_draft(
                [path], evidence_kind="recorded", surface_kind="outer_window"
            )
            episode = draft["episodes"][0]
            self.assertEqual(episode["timestamp_basis"], "recording-TimeMs-unqualified")
            self.assertEqual(episode["frames"][0]["png"]["alpha"], "present-unmeasured")
            self.assertFalse(episode["frames"][0]["png"]["pixel_decode_performed"])
            report = check_collection(draft, snapshots=sources)
            reasons = {item["reason"] for item in report["gaps"]}
            self.assertTrue(
                {
                    "alpha-opacity-not-qualified",
                    "source-timestamp-alignment-unqualified",
                    "client-rectangle-unknown",
                }
                <= reasons
            )

    def test_original_hash_dimension_timestamp_and_traversal_errors_fail_before_output(
        self,
    ):
        with tempfile.TemporaryDirectory() as directory:
            path = make_recording(directory)
            original = json.loads(path.read_text())
            for mutation in ("hash", "dimensions", "timestamp", "traversal"):
                with self.subTest(mutation=mutation):
                    data = deepcopy(original)
                    if mutation == "hash":
                        data["Frames"][0]["Sha256"] = "0" * 64
                    elif mutation == "dimensions":
                        data["Frames"][0]["Width"] += 1
                    elif mutation == "timestamp":
                        data["Frames"][1]["TimeMs"] = data["Frames"][0]["TimeMs"]
                    else:
                        data["Frames"][0]["File"] = "../outside.png"
                    path.write_text(json.dumps(data))
                    output = Path(directory) / "invalid-output"
                    with self.assertRaises(ValueError):
                        prepare_recordings([path], output)
                    self.assertFalse(output.exists())

    def test_existing_output_is_never_overwritten(self):
        with tempfile.TemporaryDirectory() as directory:
            path = make_recording(directory)
            output = Path(directory) / "existing"
            output.mkdir()
            (output / "keep.txt").write_text("keep")
            with self.assertRaises(FileExistsError):
                prepare_recordings([path], output)
            self.assertEqual((output / "keep.txt").read_text(), "keep")

    def test_frame_recording_and_byte_caps_are_enforced(self):
        with tempfile.TemporaryDirectory() as directory:
            path = make_recording(directory)
            with self.assertRaises(ValueError):
                build_draft([path] * 9)
            with patch("conjuration_collection.MAX_FRAMES", 2):
                with self.assertRaises(ValueError):
                    build_draft([path])
            with patch("conjuration_collection.MAX_TOTAL_BYTES", 10):
                with self.assertRaises(ValueError):
                    build_draft([path])

    def test_png_chunk_corruption_or_truncation_is_rejected_without_pixel_decode(self):
        raw = png()
        for broken in (
            b"not PNG",
            raw[:-1],
            raw[:40],
            raw[:-5] + b"abcde",
            raw + b"extra",
        ):
            with self.assertRaises(ValueError):
                png_metadata(broken)

    def test_receipts_must_be_explicit_linked_and_not_claim_outcomes(self):
        with tempfile.TemporaryDirectory() as directory:
            path = make_recording(directory)
            draft, sources = build_draft([path], evidence_kind="recorded")
            episode, frame = draft["episodes"][0], draft["episodes"][0]["frames"][1]
            frame.update(stage="flip", review="manual")
            report = check_collection(draft, snapshots=sources)
            self.assertIn(
                "input-receipts-missing", {g["reason"] for g in report["gaps"]}
            )
            episode["input_receipts"] = [
                {
                    "receipt_id": "r1",
                    "time_ms": 10,
                    "proposal_id": None,
                    "status": "unknown",
                    "origin": "manual_observation",
                    "action": "left click",
                }
            ]
            frame["receipt_ids"] = ["r1"]
            report = check_collection(draft, snapshots=sources)
            self.assertIn(
                "input-dispatch-evidence-incomplete",
                {g["reason"] for g in report["gaps"]},
            )
            self.assertFalse(report["gameplay_outcome_verified"])
            frame["receipt_ids"] = ["missing"]
            with self.assertRaises(ValueError):
                check_collection(draft, snapshots=sources)

    def test_optional_removed_coverage_needs_actual_reviewed_rule_evidence(self):
        with tempfile.TemporaryDirectory() as directory:
            path = make_recording(directory)
            draft, sources = build_draft([path], evidence_kind="recorded")
            draft["rules"]["removed_state"] = "not_used"
            report = check_collection(draft, snapshots=sources)
            self.assertIn(
                "removed-card-representation-rule-unqualified",
                {g["reason"] for g in report["gaps"]},
            )
            draft["rules"]["evidence_frame_ids"] = ["does-not-exist"]
            with self.assertRaises(ValueError):
                check_collection(draft, snapshots=sources)

    def test_annotation_changes_cannot_mutate_original_clock_or_geometry(self):
        with tempfile.TemporaryDirectory() as directory:
            path = make_recording(directory)
            draft, sources = build_draft([path])
            for field, value in (
                ("time_ms", 500),
                ("width", 100),
                ("recording_frame_index", 1),
            ):
                changed = deepcopy(draft)
                changed["episodes"][0]["frames"][0][field] = value
                with self.assertRaises(ValueError):
                    check_collection(changed, snapshots=sources)

    def test_documented_plan_is_nonexecutable_and_explicit_about_unknown_rules(self):
        plan = json.loads(
            (ROOT / "tools/fixtures/conjuration_collection/plan-v1.json").read_text(
                encoding="utf-8"
            )
        )
        self.assertFalse(plan["executable"])
        self.assertFalse(plan["starts_recorder"])
        self.assertFalse(plan["sends_game_input"])
        self.assertEqual({s["stage"] for s in plan["steps"]}, set(STAGES))
        self.assertTrue(plan["unknown_rules"])
        self.assertEqual(plan["existing_evidence"]["unique_png_hashes"], 1)

    def test_complete_mock_curation_is_only_a_checklist_and_never_runtime_qualification(
        self,
    ):
        with tempfile.TemporaryDirectory() as directory:
            draft, sources = complete_mock_draft(directory)
            report = check_collection(draft, snapshots=sources)
            self.assertTrue(report["collection_checklist_met"], report["gaps"])
            self.assertFalse(report["recognition_accuracy_verified"])
            self.assertFalse(report["gameplay_outcome_verified"])
            self.assertFalse(report["runtime_enabled"])
            wrong = deepcopy(draft)
            wrong["episodes"][0]["client_rect"] = [0, 0, 32, 48]
            with self.assertRaisesRegex(ValueError, "qualified client rectangle"):
                check_collection(wrong, snapshots=sources)
            wrong = deepcopy(draft)
            wrong["episodes"][0]["frames"][2]["stage"] = "preview"
            with self.assertRaisesRegex(ValueError, "stage conflicts"):
                check_collection(wrong, snapshots=sources)

    def test_image_budget_is_separate_from_the_metadata_allowance(self):
        with tempfile.TemporaryDirectory() as directory:
            path = make_recording(directory)
            draft, sources = build_draft([path])
            actual_image_bytes = sum(
                len(raw) for name, raw in sources.items() if name.endswith(".png")
            )
            with patch(
                "conjuration_collection.MAX_TOTAL_BYTES", actual_image_bytes - 1
            ):
                with self.assertRaisesRegex(ValueError, "image snapshots"):
                    check_collection(draft, snapshots=sources)

    def test_new_board_cannot_reuse_current_or_retired_identity(self):
        with tempfile.TemporaryDirectory() as directory:
            draft, sources = complete_mock_draft(directory)
            self.assertTrue(
                check_collection(draft, snapshots=sources)["collection_checklist_met"]
            )
            wrong = deepcopy(draft)
            frames = wrong["episodes"][0]["frames"]
            frames[-1]["board"]["board_id"] = frames[1]["board"]["board_id"]
            report = check_collection(wrong, snapshots=sources)
            self.assertFalse(report["collection_checklist_met"])
            self.assertIn(
                "new-board-must-establish-fresh-identity",
                {g["reason"] for g in report["gaps"]},
            )
            wrong = deepcopy(draft)
            frames = wrong["episodes"][0]["frames"]
            frames[2]["stage"] = "new_board"
            frames[2]["board"]["board_id"] = "intermediate-board"
            report = check_collection(wrong, snapshots=sources)
            self.assertFalse(report["collection_checklist_met"])
            self.assertIn(
                "retired-board-identity-reused", {g["reason"] for g in report["gaps"]}
            )

    def test_same_pixels_require_consistent_card_annotations_not_matching_episode_ids(
        self,
    ):
        with tempfile.TemporaryDirectory() as directory:
            draft, sources = complete_mock_draft(directory)
            clone_calibration_episode(draft, sources)
            report = check_collection(draft, snapshots=sources)
            self.assertTrue(report["collection_checklist_met"], report["gaps"])
            self.assertEqual(report["excluded_hashes"], {})
            for change in ("identity", "state", "geometry", "group_size"):
                with self.subTest(change=change):
                    wrong = deepcopy(draft)
                    clone = wrong["episodes"][-1]
                    frame = clone["frames"][5 if change == "state" else 1]
                    if change == "identity":
                        frame["board"]["cards"][0]["identity"] = "B"
                    elif change == "state":
                        frame["board"]["cards"][0].update(state="hidden", identity=None)
                    elif change == "geometry":
                        frame["board"]["cards"][0]["x"] += 1
                    else:
                        frame["board"]["cards"].append(
                            {
                                "slot": "2",
                                "x": 8,
                                "y": 24,
                                "state": "face_up",
                                "identity": "A",
                            }
                        )
                        frame["group_size"] = 3
                    report = check_collection(wrong, snapshots=sources)
                    self.assertFalse(report["collection_checklist_met"])
                    self.assertEqual(
                        report["excluded_hashes"][frame["sha256"]],
                        "conflicting-card-annotations-on-identical-pixels",
                    )

    def test_existing_seventeen_frames_remain_one_insufficient_instruction_image(self):
        paths = [
            ROOT / ".local/conjuration-samples/recordings" / name / "recording.json"
            for name in ("20261108-175042-fd787863", "20261108-175243-79c9bad5")
        ]
        if not all(path.is_file() for path in paths):
            self.skipTest("Local historical recordings are not distributed")
        draft, sources = build_draft(
            paths, evidence_kind="recorded", surface_kind="outer_window"
        )
        for episode in draft["episodes"]:
            episode["split"] = "calibration"
            for frame in episode["frames"]:
                frame.update(stage="instructions", review="manual")
        report = check_collection(draft, snapshots=sources)
        self.assertEqual(
            (
                report["source_frame_count"],
                report["unique_image_count"],
                report["duplicate_frame_count"],
            ),
            (17, 1, 16),
        )
        self.assertEqual(
            report["declared_recorded_manual_coverage"]["calibration"]["instructions"],
            1,
        )
        self.assertFalse(report["collection_checklist_met"])
        self.assertFalse(report["runtime_enabled"])


if __name__ == "__main__":
    unittest.main()
