import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from analyze_dodge_outcomes import summarize_reports


class OutcomeAnalysisTests(unittest.TestCase):
    def test_quiet_decisions_are_not_counted_as_failed_clearance(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "report.json"
            quiet = {
                "advice": "Hold",
                "outcome_diagnostics": {
                    "Advice": "Hold",
                    "BeforeClearanceAdvice": "Hold",
                    "ClearanceOverride": False,
                    "SafeOptions": 0,
                    "SelectedClearance": None,
                },
            }
            fallback = {
                "advice": "Left",
                "outcome_diagnostics": {
                    "Advice": "Left",
                    "BeforeClearanceAdvice": "Right",
                    "ClearanceOverride": True,
                    "SafeOptions": 0,
                    "SelectedClearance": -3.0,
                    "Fallback": "two-phase",
                },
            }
            path.write_text(
                json.dumps(
                    {
                        "policy": "directional-outcome-v34",
                        "stop_reason": "game-over",
                        "decisions": [quiet, fallback],
                    }
                )
            )
            result = summarize_reports([path])
            self.assertEqual(result["quiet_decisions"], 1)
            self.assertEqual(result["no_safe_options"], 1)
            self.assertEqual(result["clearance_overrides"], 1)
            self.assertEqual(result["two_phase_fallbacks"], 1)
            fallback["outcome_diagnostics"]["ClearanceOverride"] = False
            path.write_text(
                json.dumps(
                    {
                        "policy": "directional-outcome-v34",
                        "stop_reason": "game-over",
                        "decisions": [fallback],
                    }
                )
            )
            with self.assertRaisesRegex(ValueError, "Inconsistent"):
                summarize_reports([path])

    def test_reference_choice_is_counted_and_checked(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "report.json"
            info = {
                "Advice": "Left",
                "BeforeClearanceAdvice": "Right",
                "ClearanceOverride": True,
                "SafeOptions": 2,
                "SelectedClearance": 20.0,
                "ReferenceAdvice": "Left",
                "ReferenceRetained": True,
            }
            report = {
                "policy": "directional-outcome-reference-v37",
                "stop_reason": "game-over",
                "decisions": [{"advice": "Left", "outcome_diagnostics": info}],
            }
            path.write_text(json.dumps(report))
            self.assertEqual(summarize_reports([path])["reference_retained"], 1)
            info["ReferenceRetained"] = False
            info["ReferenceAdvice"] = "Up"
            path.write_text(json.dumps(report))
            self.assertEqual(
                summarize_reports([path])["model_selected_after_reference_rejected"], 1
            )
            info["ReferenceRetained"] = True
            info["ReferenceAdvice"] = "Up"
            path.write_text(json.dumps(report))
            with self.assertRaisesRegex(ValueError, "reference-policy"):
                summarize_reports([path])
            report["policy"] = "directional-outcome-escape-v38"
            info["ReferenceRetained"] = False
            info["Fallback"] = "two-phase-early"
            path.write_text(json.dumps(report))
            result = summarize_reports([path])
            self.assertEqual(result["two_phase_early"], 1)
            self.assertEqual(result["model_selected_after_reference_rejected"], 0)
            info["SafeOptions"] = 4
            path.write_text(json.dumps(report))
            with self.assertRaisesRegex(ValueError, "early escape"):
                summarize_reports([path])


if __name__ == "__main__":
    unittest.main()
