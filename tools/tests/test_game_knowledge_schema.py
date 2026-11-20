"""Small in-memory integrity checks for the proposed local knowledge store."""

from pathlib import Path
import sqlite3
import unittest

SCHEMA = Path(__file__).resolve().parents[2] / "research/game_knowledge_schema.sql"
GAME = "maelstrom.game.wizard101"
ENTITY = "maelstrom.entity.w101.pet-dance"
SERIES = "maelstrom.assertion.w101.dance-length"
SOURCE = "maelstrom.source.w101.pet-guide"
SOURCE_REVISION = "maelstrom.source-revision.w101.pet-guide.1"
CLAIM = "maelstrom.claim.w101.dance-length.1"


class GameKnowledgeSchemaTests(unittest.TestCase):
    def setUp(self):
        self.db = sqlite3.connect(":memory:")
        self.addCleanup(self.db.close)
        self.db.executescript(SCHEMA.read_text(encoding="utf-8"))
        self.insert("games", game_id=GAME, title="Wizard101")
        self.insert("domains", game_id=GAME, domain_id="minigames", title="Minigames")
        self.insert(
            "entities",
            entity_id=ENTITY,
            game_id=GAME,
            domain_id="minigames",
            kind="minigame",
            display_name="Pet Dance",
        )
        self.insert(
            "sources",
            source_id=SOURCE,
            kind="official-guide",
            locator="https://example.invalid/pet-guide",
        )
        self.insert(
            "source_revisions",
            source_revision_id=SOURCE_REVISION,
            source_id=SOURCE,
            revision_no=1,
            title="Example guide",
            retrieved_on="2026-11-08",
            retrieved_timezone="America/New_York",
        )
        self.insert(
            "claim_series",
            series_id=SERIES,
            game_id=GAME,
            entity_id=ENTITY,
            predicate="round.initial-cue-count",
        )

    def insert(self, table, **values):
        # Table/column names are fixed by this test module, never external input.
        names = ", ".join(values)
        placeholders = ", ".join("?" for _ in values)
        self.db.execute(
            f"INSERT INTO {table} ({names}) VALUES ({placeholders})",
            tuple(values.values()),
        )

    def claim(self, **changes):
        values = dict(
            claim_id=CLAIM,
            series_id=SERIES,
            game_id=GAME,
            revision_no=1,
            statement="Example claim, not imported W101 knowledge.",
            value_kind="number",
            value_number=3,
            uncertainty="documented",
            primary_source_revision_id=SOURCE_REVISION,
            authored_by="assistant-review",
        )
        values.update(changes)
        self.insert("claims", **values)

    def observation(self, **changes):
        values = dict(
            observation_id="maelstrom.observation.w101.example",
            game_id=GAME,
            entity_id=ENTITY,
            environment_kind="live-client",
            inputs_by="human",
            observed_on="2026-11-08",
            observed_timezone="America/New_York",
            game_version="example-build",
            region="example-region",
            platform="Windows",
            realm="live",
            artifact_locator="fixture://example-frame",
            artifact_sha256="A" * 64,
            fact="Example observation, not a gameplay claim.",
            recorded_by="assistant-review",
        )
        values.update(changes)
        self.insert("observations", **values)

    def qualification(self, **changes):
        values = dict(
            qualification_id="maelstrom.qualification.w101.example",
            game_id=GAME,
            claim_id=CLAIM,
            observation_id="maelstrom.observation.w101.example",
            level="current-client",
            result="supports",
            note="Only the explicit observed scope.",
        )
        values.update(changes)
        self.insert("qualifications", **values)

    def test_foreign_keys_and_schema_version_are_active(self):
        self.assertEqual(self.db.execute("PRAGMA foreign_keys").fetchone()[0], 1)
        self.assertEqual(
            self.db.execute("SELECT version FROM knowledge_schema").fetchone()[0], 1
        )
        self.assertEqual(self.db.execute("PRAGMA foreign_key_check").fetchall(), [])
        self.assertEqual(self.db.execute("PRAGMA integrity_check").fetchone()[0], "ok")

    def test_claim_requires_an_existing_source_revision(self):
        with self.assertRaises(sqlite3.IntegrityError):
            self.claim(primary_source_revision_id=None)
        with self.assertRaises(sqlite3.IntegrityError):
            self.claim(primary_source_revision_id="maelstrom.source-revision.missing")
        self.claim()
        self.assertEqual(
            self.db.execute("SELECT count(*) FROM claims").fetchone()[0], 1
        )

    def test_publication_date_is_not_retrieval_date_and_invalid_dates_fail(self):
        self.assertEqual(
            self.db.execute(
                "SELECT published_on, retrieved_on FROM source_revisions"
            ).fetchone(),
            (None, "2026-11-08"),
        )
        with self.assertRaises(sqlite3.IntegrityError):
            self.insert(
                "source_revisions",
                source_revision_id="maelstrom.source-revision.invalid",
                source_id=SOURCE,
                revision_no=2,
                previous_revision_id=SOURCE_REVISION,
                title="Invalid date",
                retrieved_on="2026-02-30",
                retrieved_timezone="UTC",
            )

    def test_source_revision_keeps_old_bibliography_and_rejects_cross_source_parent(
        self,
    ):
        self.insert(
            "source_revisions",
            source_revision_id="maelstrom.source-revision.w101.pet-guide.2",
            source_id=SOURCE,
            revision_no=2,
            previous_revision_id=SOURCE_REVISION,
            title="Updated guide",
            published_on="2026-10-01",
            retrieved_on="2026-11-08",
            retrieved_timezone="America/New_York",
        )
        self.assertEqual(
            self.db.execute("SELECT count(*) FROM source_revisions").fetchone()[0], 2
        )
        self.insert(
            "sources",
            source_id="maelstrom.source.other",
            kind="wiki",
            locator="https://example.invalid/other",
        )
        with self.assertRaises(sqlite3.IntegrityError):
            self.insert(
                "source_revisions",
                source_revision_id="maelstrom.source-revision.other.2",
                source_id="maelstrom.source.other",
                revision_no=2,
                previous_revision_id=SOURCE_REVISION,
                title="Wrong parent",
                retrieved_on="2026-11-08",
                retrieved_timezone="UTC",
            )

    def test_revision_lineage_and_retraction_preserve_history(self):
        self.claim()
        second = "maelstrom.claim.w101.dance-length.2"
        self.claim(
            claim_id=second,
            revision_no=2,
            supersedes_claim_id=CLAIM,
            disposition="corrected",
            value_number=4,
        )
        self.assertEqual(
            self.db.execute(
                "SELECT value_number FROM claims ORDER BY revision_no"
            ).fetchall(),
            [(3.0,), (4.0,)],
        )
        self.assertEqual(
            self.db.execute("SELECT claim_id FROM claim_leaves").fetchall(), [(second,)]
        )
        with self.assertRaises(sqlite3.IntegrityError):
            self.claim(
                claim_id="maelstrom.claim.bad-jump",
                revision_no=4,
                supersedes_claim_id=second,
            )
        self.claim(
            claim_id="maelstrom.claim.w101.dance-length.3",
            revision_no=3,
            supersedes_claim_id=second,
            disposition="retracted",
            value_kind="unknown",
            value_number=None,
        )
        self.assertEqual(
            self.db.execute("SELECT disposition FROM claim_leaves").fetchone()[0],
            "retracted",
        )
        for statement in (
            "UPDATE claims SET value_number=9 WHERE claim_id=?",
            "DELETE FROM claims WHERE claim_id=?",
        ):
            with self.assertRaisesRegex(sqlite3.IntegrityError, "append-only"):
                self.db.execute(statement, (CLAIM,))

    def test_replace_cannot_erase_history_even_without_recursive_triggers(self):
        self.db.execute("PRAGMA recursive_triggers=OFF")
        self.claim()
        with self.assertRaisesRegex(sqlite3.IntegrityError, "cannot be replaced"):
            self.db.execute(
                "INSERT OR REPLACE INTO claims SELECT * FROM claims WHERE claim_id=?",
                (CLAIM,),
            )
        with self.assertRaisesRegex(sqlite3.IntegrityError, "cannot be replaced"):
            self.db.execute(
                "INSERT OR REPLACE INTO games(game_id,title) VALUES (?,?)",
                (GAME, "Replacement"),
            )
        self.assertEqual(
            self.db.execute(
                "SELECT title FROM games WHERE game_id=?", (GAME,)
            ).fetchone()[0],
            "Wizard101",
        )

    def test_contradictory_assertions_coexist_without_a_winner(self):
        self.claim()
        other_series = "maelstrom.assertion.w101.competing-length"
        other_claim = "maelstrom.claim.w101.competing-length.1"
        self.insert(
            "claim_series",
            series_id=other_series,
            game_id=GAME,
            entity_id=ENTITY,
            predicate="round.initial-cue-count",
        )
        self.claim(
            claim_id=other_claim,
            series_id=other_series,
            value_number=4,
            uncertainty="disputed",
        )
        self.insert(
            "claim_links",
            link_id="maelstrom.claim-link.length-conflict",
            left_claim_id=CLAIM,
            right_claim_id=other_claim,
            relation="contradicts",
            reason="Example unresolved contradiction.",
        )
        self.assertEqual(
            self.db.execute("SELECT count(*) FROM claim_leaves").fetchone()[0], 2
        )
        self.assertEqual(
            self.db.execute("SELECT count(*) FROM qualifications").fetchone()[0], 0
        )

    def test_typed_values_reject_inconsistent_or_nonfinite_payloads(self):
        for bad in (
            dict(value_kind="boolean", value_number=2),
            dict(value_kind="text", value_text="three", value_number=3),
            dict(value_kind="unknown", value_number=3),
            dict(value_number=float("inf")),
            dict(value_number=float("nan")),
        ):
            with self.subTest(bad=bad), self.assertRaises(sqlite3.IntegrityError):
                self.claim(**bad)

    def test_cross_game_relationship_is_rejected(self):
        other_game = "maelstrom.game.example"
        self.insert("games", game_id=other_game, title="Example")
        self.insert("domains", game_id=other_game, domain_id="areas", title="Areas")
        self.insert(
            "entities",
            entity_id="maelstrom.entity.other.area",
            game_id=other_game,
            domain_id="areas",
            kind="area",
            display_name="Other area",
        )
        with self.assertRaises(sqlite3.IntegrityError):
            self.insert(
                "relationships",
                relationship_id="maelstrom.relationship.invalid",
                game_id=GAME,
                subject_entity_id=ENTITY,
                type_id="located-in",
                object_entity_id="maelstrom.entity.other.area",
            )

    def test_current_client_requires_actual_scoped_observation_and_preserves_human_actor(
        self,
    ):
        self.claim()
        self.observation(environment_kind="synthetic")
        with self.assertRaisesRegex(
            sqlite3.IntegrityError, "explicit observation scope"
        ):
            self.qualification()
        self.observation(observation_id="maelstrom.observation.real", game_version=None)
        with self.assertRaisesRegex(
            sqlite3.IntegrityError, "explicit observation scope"
        ):
            self.qualification(observation_id="maelstrom.observation.real")
        self.observation(observation_id="maelstrom.observation.scoped")
        self.qualification(observation_id="maelstrom.observation.scoped")
        self.assertEqual(
            self.db.execute(
                "SELECT inputs_by FROM observations WHERE observation_id='maelstrom.observation.scoped'"
            ).fetchone()[0],
            "human",
        )

    def test_realm_and_version_changes_are_not_silent_corroboration_or_refutation(self):
        self.claim(game_version="older-build", realm="test")
        self.observation()
        for result in ("supports", "contradicts"):
            with self.subTest(result=result), self.assertRaisesRegex(
                sqlite3.IntegrityError, "scopes differ"
            ):
                self.qualification(result=result)
        self.qualification(result="inconclusive")
        self.assertEqual(
            self.db.execute("SELECT realm FROM claims").fetchone()[0], "test"
        )

    def test_runtime_references_are_disabled_and_share_claim_game(self):
        self.claim()
        values = dict(
            candidate_id="maelstrom.candidate.w101.dance",
            game_id=GAME,
            entity_id=ENTITY,
            kind="routine",
            registry_ref="w101.pet-dance-review",
            pack_id="wizard101-pet-dance-teacher",
            pack_version="draft-example",
        )
        with self.assertRaises(sqlite3.IntegrityError):
            self.insert("runtime_candidates", **values, enabled=1)
        self.insert("runtime_candidates", **values)
        self.insert(
            "candidate_claims",
            candidate_id=values["candidate_id"],
            claim_id=CLAIM,
            game_id=GAME,
        )
        self.assertEqual(
            self.db.execute("SELECT enabled,status FROM runtime_candidates").fetchone(),
            (0, "disabled-candidate"),
        )
        with self.assertRaises(sqlite3.IntegrityError):
            self.insert(
                "candidate_claims",
                candidate_id=values["candidate_id"],
                claim_id="maelstrom.claim.missing",
                game_id=GAME,
            )
        self.assertEqual(self.db.execute("PRAGMA foreign_key_check").fetchall(), [])


if __name__ == "__main__":
    unittest.main()
