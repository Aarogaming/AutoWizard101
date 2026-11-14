import copy
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import w101_stats_profile as profile


def fixture():
    identity = dict(
        handle=123,
        processId=456,
        startedUtcTicks=100,
        processName="WizardGraphicalClient",
        imagePath="C:/game/client.exe",
        imageSha256="a" * 64,
    )
    observed = dict(
        Target=identity,
        Status="observed",
        Loading=False,
        CleanupConfirmed=True,
        HookRestored=True,
        ResourceCandidate=dict(
            status="candidate",
            observedUtcTicks=30_000_000,
            uiObservedUtcTicks=29_000_000,
            namedFields=[dict(name="m_baseHitpoints", value=100, quality="candidate")],
            uiGlobes=dict(health=dict(current=100, maximum=200)),
        ),
    )
    reference = dict(
        schemaVersion=1,
        source="manual",
        target=identity.copy(),
        observedUtcTicks=30_000_000,
        fields={
            "m_baseHitpoints": dict(value=100, confidence=1.0),
            "health.maximum": dict(value=200, confidence=0.99),
        },
    )
    return observed, reference


class StatsProfileTests(unittest.TestCase):
    def subject_fixture(self):
        observed, reference = fixture()
        identity = dict(
            profileId="profile-death",
            name="Luke Skywalker",
            school="Death",
            generation=2,
        )
        observed["Subject"] = identity.copy()
        observed["StatesObservedUtcTicks"] = 29_000_000
        reference["subject"] = identity.copy()
        return observed, reference

    def test_subject_matches_without_promoting_and_level_is_not_identity(self):
        observed, reference = self.subject_fixture()
        observed["Subject"]["level"] = 10
        reference["subject"]["level"] = 11
        result = profile.compare_documents(observed, reference)
        self.assertTrue(result["subjectVerified"])
        self.assertNotIn("level", result["subject"])
        self.assertEqual(2, result["counts"]["agreement"])
        self.assertFalse(
            result["liveQualified"] or result["canPromote"] or result["executable"]
        )

    def test_same_name_other_school_and_same_process_new_generation_rejected(self):
        for key, value in (
            ("school", "Fire"),
            ("generation", 3),
            ("profileId", "other-profile"),
            ("name", "Other Name"),
            ("verifiedCharacterId", "character-2"),
        ):
            with self.subTest(key=key):
                observed, reference = self.subject_fixture()
                reference["subject"][key] = value
                result = profile.compare_documents(observed, reference)
                self.assertEqual("rejected", result["status"])
                self.assertFalse(result["subjectVerified"])
                self.assertIn("subject-mismatch", result["reasons"])
                self.assertEqual(0, result["counts"]["agreement"])

    def test_subject_missing_counterpart_rejected(self):
        for side in ("observed", "reference"):
            observed, reference = self.subject_fixture()
            if side == "observed":
                del observed["Subject"]
            else:
                del reference["subject"]
            result = profile.compare_documents(observed, reference)
            self.assertEqual("rejected", result["status"])
            self.assertIn("subject-counterpart-missing", result["reasons"])
            self.assertFalse(result["subjectVerified"])

    def test_subject_invalid_generation_or_identity_rejected(self):
        for key, value in (
            ("generation", 0),
            ("generation", True),
            ("profileId", ""),
            ("name", "\n"),
            ("school", None),
            ("verifiedCharacterId", ""),
        ):
            observed, reference = self.subject_fixture()
            observed["Subject"][key] = value
            reference["subject"][key] = value
            result = profile.compare_documents(observed, reference)
            self.assertIn("subject-invalid", result["reasons"])
            self.assertFalse(result["subjectVerified"])

    def test_subject_time_must_match_envelopes_and_field(self):
        observed, reference = self.subject_fixture()
        observed["Subject"]["observedUtcTicks"] = 1
        result = profile.compare_documents(observed, reference)
        self.assertIn("subject-time-mismatch", result["reasons"])
        observed, reference = self.subject_fixture()
        observed["Subject"]["observedUtcTicks"] = 30_000_000
        observed["StatesObservedUtcTicks"] = 1
        self.assertIn(
            "subject-time-mismatch",
            profile.compare_documents(observed, reference)["reasons"],
        )
        observed, reference = self.subject_fixture()
        observed["StatesObservedUtcTicks"] = 10_000_000
        observed["ResourceCandidate"]["observedUtcTicks"] = 40_000_000
        reference["fields"] = {"m_baseHitpoints": dict(value=100, confidence=1)}
        result = profile.compare_documents(observed, reference)
        self.assertEqual(
            "candidate-subject-time-mismatch", result["fields"][0]["reason"]
        )

    def test_legacy_subjectless_comparison_is_never_subject_verified(self):
        observed, reference = fixture()
        result = profile.compare_documents(observed, reference)
        self.assertFalse(result["subjectVerified"])
        self.assertIsNone(result["subject"])
        self.assertFalse(result["canPromote"])
        self.assertEqual(2, result["counts"]["agreement"])

    def test_fractional_potions_use_ui_evidence_without_promotion(self):
        observed, reference = fixture()
        observed["ResourceCandidate"]["uiPotions"] = dict(current=3.5, maximum=4)
        observed["ResourceCandidate"]["potions"] = dict(current=1, maximum=2)
        reference["fields"] = {
            "potions.current": dict(value=3.5, confidence=1),
            "potions.maximum": dict(value=4, confidence=1),
        }
        result = profile.compare_documents(observed, reference)
        self.assertEqual(2, result["counts"]["agreement"])
        self.assertTrue(
            all(row["candidateSource"] == "uiPotions" for row in result["fields"])
        )
        self.assertTrue(
            all(row["observedUtcTicks"] == 29_000_000 for row in result["fields"])
        )
        self.assertFalse(
            result["liveQualified"] or result["canPromote"] or result["executable"]
        )
        del observed["ResourceCandidate"]["uiPotions"]
        self.assertEqual(
            2, profile.compare_documents(observed, reference)["counts"]["unknown"]
        )

    def test_potion_ui_age_is_checked_independently_of_stats(self):
        observed, reference = fixture()
        observed["ResourceCandidate"]["uiPotions"] = dict(current=3.5, maximum=4)
        observed["ResourceCandidate"]["uiObservedUtcTicks"] = 1
        reference["fields"] = {"potions.current": dict(value=3.5, confidence=1)}
        result = profile.compare_documents(observed, reference)
        self.assertEqual(
            "candidate-reference-time-mismatch", result["fields"][0]["reason"]
        )

    def test_native_envelope_and_globes_are_compared_without_promotion(self):
        observed, reference = fixture()
        before = copy.deepcopy(observed)
        result = profile.compare_documents(observed, reference)
        self.assertEqual(2, result["counts"]["agreement"])
        self.assertFalse(result["executable"])
        self.assertFalse(result["liveQualified"])
        self.assertFalse(result["canPromote"])
        self.assertEqual(before, observed)
        self.assertNotIn("resources", result)
        self.assertNotIn("currentValues", result)

    def test_all_target_parts_must_match(self):
        for key, changed in (
            ("handle", 124),
            ("processId", 457),
            ("startedUtcTicks", 101),
            ("imageSha256", "b" * 64),
            ("imagePath", "D:/other.exe"),
            ("processName", "Other"),
        ):
            with self.subTest(key=key):
                observed, reference = fixture()
                reference["target"][key] = changed
                result = profile.compare_documents(observed, reference)
                self.assertEqual("rejected", result["status"])
                self.assertEqual(0, result["counts"]["agreement"])

    def test_stale_and_future_relative_evidence_unknown(self):
        for stamp in (8_000_000, 51_000_000):
            observed, reference = fixture()
            reference["observedUtcTicks"] = stamp
            result = profile.compare_documents(observed, reference)
            self.assertEqual(2, result["counts"]["unknown"])

    def test_different_field_acquisition_times_are_respected(self):
        observed, reference = fixture()
        observed["ResourceCandidate"]["uiObservedUtcTicks"] = 1
        result = profile.compare_documents(observed, reference)
        self.assertEqual(1, result["counts"]["agreement"])
        self.assertEqual(1, result["counts"]["unknown"])

    def test_low_confidence_ambiguity_and_invalid_numbers_unknown(self):
        for replacement in (
            dict(value=100, confidence=0.94),
            dict(value=100, confidence=1.1),
            dict(value=100, confidence=True),
            dict(value=100, confidence=1, ambiguous=True),
            dict(value=float("nan"), confidence=1),
            dict(value=float("inf"), confidence=1),
            dict(value=10**1000, confidence=1),
            dict(value=True, confidence=1),
            dict(value="100", confidence=1),
        ):
            with self.subTest(replacement=replacement):
                observed, reference = fixture()
                reference["fields"] = {"m_baseHitpoints": replacement}
                self.assertEqual(
                    1,
                    profile.compare_documents(observed, reference)["counts"]["unknown"],
                )

    def test_disagreement_and_missing_fields_preserved(self):
        observed, reference = fixture()
        reference["fields"]["m_baseHitpoints"]["value"] = 101
        reference["fields"]["m_missing"] = dict(value=1, confidence=1)
        result = profile.compare_documents(observed, reference)
        self.assertEqual(dict(agreement=1, disagreement=1, unknown=1), result["counts"])

    def test_duplicate_or_unqualified_named_fields_unknown(self):
        observed, reference = fixture()
        observed["ResourceCandidate"]["namedFields"] *= 2
        result = profile.compare_documents(observed, reference)
        self.assertEqual("candidate-field-ambiguous", result["fields"][0]["reason"])
        observed, reference = fixture()
        observed["ResourceCandidate"]["namedFields"][0]["quality"] = "unknown"
        self.assertEqual(
            "unknown",
            profile.compare_documents(observed, reference)["fields"][0]["outcome"],
        )

    def test_loading_and_cleanup_failure_reject(self):
        for key, value in (
            ("Loading", True),
            ("Loading", None),
            ("CleanupConfirmed", False),
            ("HookRestored", False),
            ("Status", "cancelled"),
        ):
            observed, reference = fixture()
            observed[key] = value
            self.assertEqual(
                "rejected", profile.compare_documents(observed, reference)["status"]
            )

    def test_field_budget_and_ambiguous_member_casing_rejected(self):
        observed, reference = fixture()
        reference["fields"] = {
            f"field{i}": dict(value=0, confidence=1) for i in range(129)
        }
        with self.assertRaises(ValueError):
            profile.compare_documents(observed, reference)
        observed, reference = fixture()
        observed["target"] = observed["Target"]
        with self.assertRaises(ValueError):
            profile.compare_documents(observed, reference)

    def test_files_have_hashes_and_optional_verified_image(self):
        observed, reference = fixture()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            image = b"synthetic-fixture-no-capture"
            (root / "evidence.png").write_bytes(image)
            reference["evidence"] = dict(
                imagePath="evidence.png", imageSha256=hashlib.sha256(image).hexdigest()
            )
            raw = json.dumps(observed).encode()
            (root / "observed.json").write_bytes(raw)
            (root / "reference.json").write_text(json.dumps(reference))
            result = profile.compare_files(
                root / "observed.json", root / "reference.json"
            )
            self.assertEqual(
                hashlib.sha256(raw).hexdigest(), result["inputs"]["observationSha256"]
            )
            self.assertTrue(result["imageEvidenceVerified"])
            (root / "evidence.png").write_bytes(b"changed")
            with self.assertRaisesRegex(ValueError, "hash mismatch"):
                profile.compare_files(root / "observed.json", root / "reference.json")

    def test_cli_saves_new_report_and_never_overwrites_evidence(self):
        observed, reference = fixture()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "observed.json").write_text(json.dumps(observed))
            (root / "reference.json").write_text(json.dumps(reference))
            args = [
                "--observation",
                str(root / "observed.json"),
                "--reference",
                str(root / "reference.json"),
                "--output",
                str(root / "report.json"),
            ]
            self.assertEqual(0, profile.main(args))
            result = json.loads((root / "report.json").read_text())
            self.assertFalse(result["canPromote"])
            with self.assertRaises(SystemExit):
                profile.main(args)

    def test_duplicate_json_and_oversized_input_rejected(self):
        with self.assertRaises(ValueError):
            profile.unique_object([("value", 1), ("value", 2)])
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "large"
            path.write_bytes(b"12345")
            with self.assertRaises(ValueError):
                profile.bounded_read(path, 4)


if __name__ == "__main__":
    unittest.main()
