import copy
import json
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from check_w101_integration_map import DEFAULT_PLAN, ROOT, PlanError, validate


class IntegrationMapTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.baseline = json.loads(DEFAULT_PLAN.read_text(encoding="utf-8"))
        cls.catalog = json.loads(
            (ROOT / cls.baseline["catalog"]).read_text(encoding="utf-8")
        )

    def setUp(self):
        self.plan = copy.deepcopy(self.baseline)

    def test_real_plan_covers_archives_and_orders_all_prerequisites(self):
        order = validate(self.plan, self.catalog)
        self.assertEqual(len(order), len(self.plan["work_packages"]))
        for row in self.plan["work_packages"]:
            for dependency in row["depends_on"]:
                self.assertLess(order.index(dependency), order.index(row["id"]))

    def test_missing_or_twice_mapped_archive_is_rejected(self):
        self.plan["sources"].pop(0)
        with self.assertRaisesRegex(PlanError, "exactly one"):
            validate(self.plan, self.catalog)
        self.plan = copy.deepcopy(self.baseline)
        extra = copy.deepcopy(self.plan["sources"][0])
        extra["id"] = "duplicate-archive"
        self.plan["sources"].append(extra)
        with self.assertRaisesRegex(PlanError, "exactly one"):
            validate(self.plan, self.catalog)

    def test_additional_source_needs_local_evidence(self):
        source = next(s for s in self.plan["sources"] if s["catalog_id"] is None)
        source["evidence"] = []
        with self.assertRaisesRegex(PlanError, "needs evidence"):
            validate(self.plan, self.catalog)

    def test_cyclic_and_unknown_dependencies_are_rejected(self):
        for dependency, message in (("w04", "cycle"), ("missing", "unknown")):
            with self.subTest(dependency=dependency):
                self.plan["work_packages"][0]["depends_on"] = [dependency]
                with self.assertRaisesRegex(PlanError, message):
                    validate(self.plan, self.catalog)

    def test_live_operation_cannot_skip_capture_and_idle_gates(self):
        operation = next(o for o in self.plan["operations"] if o["writes_game"])
        operation["gate"] = "w01"
        with self.assertRaisesRegex(PlanError, "live gate"):
            validate(self.plan, self.catalog)

    def test_unknown_provider_and_duplicate_operation_are_rejected(self):
        self.plan["operations"][0]["providers"] = ["unreviewed"]
        with self.assertRaisesRegex(PlanError, "unknown providers"):
            validate(self.plan, self.catalog)
        self.plan = copy.deepcopy(self.baseline)
        self.plan["operations"].append(copy.deepcopy(self.plan["operations"][0]))
        with self.assertRaisesRegex(PlanError, "Duplicate operations"):
            validate(self.plan, self.catalog)

    def test_plan_cannot_claim_runtime_readiness(self):
        self.plan["executable"] = True
        with self.assertRaisesRegex(PlanError, "non-executable"):
            validate(self.plan, self.catalog)
        self.plan["executable"] = False
        self.plan["operations"][0]["scheduler_ready"] = True
        with self.assertRaisesRegex(PlanError, "scheduler readiness"):
            validate(self.plan, self.catalog)

    def test_evidence_path_cannot_escape_checkout(self):
        for path in ("../external.txt", "C:/external.txt", "/external.txt"):
            with self.subTest(path=path):
                self.plan["capabilities"][0]["existing"] = [path]
                with self.assertRaisesRegex(PlanError, "repository-relative"):
                    validate(self.plan, self.catalog)

    def test_lightweight_and_single_owner_defaults_are_required(self):
        for field, value in (
            ("live_input_owners", 2),
            ("queue_limit_proposed", 0),
            ("queue_limit_proposed", True),
            ("unbounded_repeats", True),
            ("training_during_live_gameplay", True),
        ):
            with self.subTest(field=field, value=value):
                self.plan = copy.deepcopy(self.baseline)
                self.plan["defaults"][field] = value
                with self.assertRaises(PlanError):
                    validate(self.plan, self.catalog)


if __name__ == "__main__":
    unittest.main()
