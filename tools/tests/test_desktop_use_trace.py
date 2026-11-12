"""Lightweight synthetic checks for the desktop-use evidence companion."""

import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
SKILL = Path(
    os.environ.get(
        "DESKTOP_USE_SKILL_DIR", ROOT / ".agents/skills/desktop-use-evidence"
    )
)
SCRIPT = SKILL / "scripts/validate_trace.py"
SPEC = importlib.util.spec_from_file_location("desktop_trace", SCRIPT)
validator = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(validator)


def trace(mode="desktop_test", source="visual"):
    return {
        "schema_version": 1,
        "mode": mode,
        "target_id": "test-window",
        "target_identity_verified": True,
        "max_actions": 2,
        "max_duration_ms": 1000,
        "max_action_duration_ms": 100,
        "max_observation_age_ms": 100,
        "events": [
            {
                "id": "o1",
                "kind": "observation",
                "time_ms": 0,
                "target_id": "test-window",
                "source": source,
                "state": "known",
            },
            {
                "id": "f1",
                "kind": "focus",
                "time_ms": 1,
                "target_id": "test-window",
                "focused": True,
            },
        ],
    }


def dispatch(mechanism="desktop", held=False, timestamp=10):
    return {
        "id": "a1",
        "kind": "dispatch",
        "time_ms": timestamp,
        "target_id": "test-window",
        "observation_id": "o1",
        "mechanism": mechanism,
        "duration_ms": 50,
        "requires_release": held,
    }


def receipt():
    return {
        "id": "r1",
        "kind": "receipt",
        "time_ms": 11,
        "action_id": "a1",
        "status": "accepted",
    }


class TraceChecks(unittest.TestCase):
    def codes(self, value):
        return {issue["code"] for issue in validator.validate_trace(value)}

    def test_bounded_desktop_action_and_observed_outcome(self):
        value = trace()
        value["events"] += [
            dispatch(held=True),
            receipt(),
            {"id": "release", "kind": "release", "time_ms": 12, "action_id": "a1"},
            {
                "id": "o2",
                "kind": "observation",
                "time_ms": 13,
                "target_id": "test-window",
                "source": "visual",
                "state": "known",
            },
            {
                "id": "out",
                "kind": "outcome",
                "time_ms": 14,
                "action_id": "a1",
                "observation_id": "o2",
                "status": "observed",
            },
            {
                "id": "claim",
                "kind": "claim",
                "time_ms": 15,
                "claim": "movement_observed",
                "evidence_id": "out",
            },
        ]
        self.assertEqual(self.codes(value), set())

    def test_observe_mode_and_native_visual_observation(self):
        self.assertEqual(self.codes(trace("observe")), set())
        value = trace("native_test")
        value["events"] += [dispatch("native"), receipt()]
        self.assertEqual(self.codes(value), set())

    def test_rejected_scenarios(self):
        cases = []
        value = trace("observe")
        value["events"] += [dispatch(), receipt()]
        cases.append((value, "mode_violation"))
        value = trace()
        value["target_identity_verified"] = False
        value["events"] += [dispatch(), receipt()]
        cases.append((value, "target_unverified"))
        value = trace()
        value["events"][1]["focused"] = False
        value["events"] += [dispatch(), receipt()]
        cases.append((value, "focus_missing"))
        value = trace()
        value["events"][0]["target_id"] = "other"
        value["events"] += [dispatch(), receipt()]
        cases.append((value, "wrong_target"))
        value = trace()
        value["events"][0]["state"] = "unknown"
        value["events"] += [dispatch(), receipt()]
        cases.append((value, "unknown_observation"))
        value = trace()
        value["events"] += [dispatch(timestamp=200)]
        cases.append((value, "stale_observation"))
        value = trace()
        value["max_actions"] = 0
        value["events"] += [dispatch(), receipt()]
        cases.append((value, "budget_exceeded"))
        value = trace()
        value["events"] += [dict(dispatch(), duration_ms=101), receipt()]
        cases.append((value, "invalid_action"))
        value = trace()
        value["events"] += [
            {"id": "stop", "kind": "stop", "time_ms": 2, "reason": "user stopped"},
            dispatch(),
            receipt(),
        ]
        cases.append((value, "action_after_stop"))
        value = trace()
        value["events"] += [dispatch()]
        cases.append((value, "missing_receipt"))
        value = trace()
        value["events"] += [dispatch(held=True), receipt()]
        cases.append((value, "missing_release"))
        value = trace()
        value["events"] += [
            dispatch(),
            receipt(),
            {
                "id": "claim",
                "kind": "claim",
                "time_ms": 12,
                "claim": "movement_observed",
                "evidence_id": "r1",
            },
        ]
        cases.append((value, "unsupported_claim"))
        value = trace()
        value["events"] += [
            dispatch(),
            receipt(),
            {
                "id": "out",
                "kind": "outcome",
                "time_ms": 12,
                "action_id": "a1",
                "observation_id": "o1",
                "status": "observed",
            },
        ]
        cases.append((value, "outcome_without_evidence"))
        value = trace(source="engine")
        value["events"] += [
            {
                "id": "v",
                "kind": "verification",
                "time_ms": 2,
                "observation_id": "o1",
                "scope": "visual",
                "status": "passed",
                "artifact": "report",
            }
        ]
        cases.append((value, "wrong_observation_source"))
        for value, expected in cases:
            with self.subTest(expected=expected):
                self.assertIn(expected, self.codes(value))

    def test_cleanup_after_stop_is_allowed(self):
        value = trace()
        value["events"] += [
            dispatch(held=True),
            receipt(),
            {"id": "s", "kind": "stop", "time_ms": 12, "reason": "user"},
            {"id": "release", "kind": "release", "time_ms": 13, "action_id": "a1"},
        ]
        self.assertEqual(self.codes(value), set())

    def test_held_input_duration_is_checked(self):
        value = trace()
        value["events"] += [
            dispatch(held=True),
            receipt(),
            {"id": "release", "kind": "release", "time_ms": 111, "action_id": "a1"},
        ]
        self.assertIn("hold_too_long", self.codes(value))

    def test_unhashable_references_do_not_crash(self):
        value = trace()
        value["events"] += [
            dict(dispatch(), observation_id=[]),
            {
                "id": "r",
                "kind": "receipt",
                "time_ms": 11,
                "action_id": {},
                "status": "accepted",
            },
            {"id": "rel", "kind": "release", "time_ms": 12, "action_id": []},
            {
                "id": "out",
                "kind": "outcome",
                "time_ms": 13,
                "action_id": [],
                "observation_id": {},
                "status": "observed",
            },
            {
                "id": "c",
                "kind": "claim",
                "time_ms": 14,
                "claim": "movement_observed",
                "evidence_id": [],
            },
        ]
        self.assertIn("invalid_receipt", self.codes(value))

    def test_cli_reports_structure_only_and_rejects_duplicate_fields(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "trace.json"
            path.write_text(json.dumps(trace("observe")), encoding="utf-8")
            result = subprocess.run(
                [sys.executable, str(SCRIPT), str(path)],
                capture_output=True,
                text=True,
                check=False,
            )
            self.assertEqual(result.returncode, 0)
            self.assertFalse(json.loads(result.stdout)["verifies_truth"])
            path.write_text(
                '{"schema_version": 1, "schema_version": 1}', encoding="utf-8"
            )
            result = subprocess.run(
                [sys.executable, str(SCRIPT), str(path)],
                capture_output=True,
                text=True,
                check=False,
            )
            self.assertEqual(result.returncode, 1)
            self.assertEqual(
                json.loads(result.stdout)["issues"][0]["code"], "invalid_trace"
            )


if __name__ == "__main__":
    unittest.main()
