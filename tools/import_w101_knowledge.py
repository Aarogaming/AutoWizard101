"""Import the reviewed W101 pet-snack ledger into a new, offline SQLite bundle.

No network, game access, executable imports, merge/update mode or runtime enablement.
The fixed repository schema is trusted code; ledger strings are SQL parameters only.
"""

from __future__ import annotations

import argparse
from contextlib import contextmanager
from datetime import date, datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import sqlite3
import tempfile
from urllib.parse import urlsplit

ROOT = Path(__file__).resolve().parents[1]
SCHEMA = ROOT / "research/game_knowledge_schema.sql"
DEFAULT_LEDGER = ROOT / "research/w101-pet-snacks-20261108.json"
MAPPING = "w101-pet-snacks-v1"
GAME = "maelstrom.game.wizard101"
MAX_LEDGER_BYTES = 262144
SOURCE_IDS = frozenset(
    {
        "official-training-feed",
        "official-energy",
        "official-stats",
        "inventory-item-feed",
        "double-pet-xp-history",
        "preference-guide",
        "happiness-guide",
        "community-training",
    }
)
CLAIM_ENTITIES = {
    "training-context": ("training-snacks", "Training snack feeding"),
    "resources-distinct": ("pet-resources", "Pet training resources"),
    "preference-model": ("snack-preferences", "Pet snack preferences"),
    "preference-observation": ("snack-preferences", "Pet snack preferences"),
    "happiness-context": ("happiness-feeding", "Pet Happiness feeding"),
    "ordinary-item-consumption": (
        "inventory-item-feeding",
        "Ordinary inventory-item feeding (excluded)",
    ),
    "mastery-skip": ("training-mastery-skip", "Pet game mastery skipping"),
    "event-modifiers": ("pet-reward-modifiers", "Pet reward modifiers"),
    "stock-channels": ("snack-supply", "Pet snack supply"),
}
SOURCE_KEYS = frozenset(
    {
        "id",
        "kind",
        "url",
        "published_on",
        "modified_on",
        "retrieved_on",
        "retrieved_timezone",
        "http_status",
        "resolved_url",
        "region",
        "platform",
        "release_scope",
        "limitation",
    }
)
CLAIM_KEYS = frozenset({"id", "statement", "evidence", "sources", "runtime_qualified"})
SOURCE_KINDS = {
    "official-guide": "official-guide",
    "publisher-administrator-announcement": "other",
    "secondary-guide": "community",
}
UNCERTAINTY = frozenset(
    {"unknown", "tentative", "documented", "corroborated", "disputed"}
)


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def canonical(value) -> str:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))


def workspace_path(value: Path, *, existing: bool) -> Path:
    value = Path(value)
    if not value.is_absolute():
        raise ValueError("Use an absolute workspace path")
    resolved = value.resolve(strict=existing)
    if resolved == ROOT or not resolved.is_relative_to(ROOT):
        raise ValueError("Path must remain inside the Maelstrom workspace")
    for part in (value, *value.parents):
        if part.is_symlink() or (hasattr(part, "is_junction") and part.is_junction()):
            raise ValueError("Linked input/output paths are not supported")
        if part == ROOT:
            break
    return resolved


def bounded_bytes(path: Path, maximum: int) -> bytes:
    if not path.is_file() or path.stat().st_size > maximum:
        raise ValueError("Expected a bounded local input file")
    with path.open("rb") as stream:
        data = stream.read(maximum + 1)
    if len(data) > maximum:
        raise ValueError("Input exceeds size limit")
    return data


def _text(value, label: str, limit: int = 4096) -> str:
    if (
        not isinstance(value, str)
        or not value.strip()
        or len(value) > limit
        or "\x00" in value
    ):
        raise ValueError(f"Missing or invalid {label}")
    return value


def _date(value, label: str, *, nullable: bool = False):
    if value is None and nullable:
        return None
    value = _text(value, label, 10)
    if len(value) != 10 or date.fromisoformat(value).isoformat() != value:
        raise ValueError(f"Invalid {label}")
    return value


def _unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate JSON property")
        result[key] = value
    return result


def validate_ledger(data: bytes) -> dict:
    ledger = json.loads(
        data.decode("utf-8-sig"),
        object_pairs_hook=_unique_object,
        parse_constant=lambda _: (_ for _ in ()).throw(
            ValueError("Nonfinite JSON value")
        ),
    )
    if (
        not isinstance(ledger, dict)
        or type(ledger.get("schema_version")) is not int
        or ledger["schema_version"] != 1
    ):
        raise ValueError("Only pet-snack ledger schema version 1 is supported")
    if (ledger.get("kind"), ledger.get("game_id"), ledger.get("subsystem")) != (
        "game-research-ledger",
        "wizard101",
        "pet-snacks",
    ):
        raise ValueError("Only the reviewed Wizard101 pet-snack mapping is supported")
    for key in (
        "executable",
        "automatic_runtime_import",
        "runtime_feeding_enabled",
        "live_qualified",
    ):
        if ledger.get(key) is not False:
            raise ValueError(f"Ledger {key} must remain explicitly false")
    _date(ledger.get("research_date"), "research date")
    _text(ledger.get("research_timezone"), "research timezone", 100)
    _text(ledger.get("scope"), "ledger scope", 4000)
    if (
        not isinstance(ledger.get("current_client"), dict)
        or ledger["current_client"].get("observed_by_this_workstream") is not False
    ):
        raise ValueError(
            "This mapping imports research without current-client observations"
        )
    sources, claims = ledger.get("sources"), ledger.get("claims")
    if not isinstance(sources, list) or len(sources) != len(SOURCE_IDS):
        raise ValueError("This mapping requires the eight reviewed sources")
    if not isinstance(claims, list) or len(claims) != len(CLAIM_ENTITIES):
        raise ValueError("This mapping requires the nine reviewed claims")
    seen = set()
    for source in sources:
        if not isinstance(source, dict) or set(source) != SOURCE_KEYS:
            raise ValueError("Unsupported or missing source provenance fields")
        if (
            not isinstance(source["id"], str)
            or source["id"] not in SOURCE_IDS
            or source["id"] in seen
        ):
            raise ValueError("Unknown or duplicate source ID")
        seen.add(source["id"])
        if not isinstance(source["kind"], str) or source["kind"] not in SOURCE_KINDS:
            raise ValueError("Unknown source kind")
        for key in ("url", "resolved_url"):
            locator = urlsplit(_text(source[key], key))
            if (
                locator.scheme != "https"
                or not locator.hostname
                or locator.username is not None
                or locator.password is not None
            ):
                raise ValueError(
                    "Expected a recorded HTTPS locator, never a fetch request"
                )
        for key in ("published_on", "modified_on"):
            _date(source[key], key, nullable=True)
        _date(source["retrieved_on"], "source retrieval date")
        _text(source["retrieved_timezone"], "retrieval timezone", 100)
        for key in ("region", "platform", "release_scope", "limitation"):
            _text(source[key], key, 2000)
        if type(source["http_status"]) is not int or source["http_status"] != 200:
            raise ValueError("A failed retrieval cannot become mechanics evidence")
    seen = set()
    for claim in claims:
        if not isinstance(claim, dict) or set(claim) != CLAIM_KEYS:
            raise ValueError("Unsupported or missing claim fields")
        if (
            not isinstance(claim["id"], str)
            or claim["id"] not in CLAIM_ENTITIES
            or claim["id"] in seen
        ):
            raise ValueError("Unknown or duplicate claim ID")
        seen.add(claim["id"])
        _text(claim["statement"], "claim statement", 8000)
        evidence, references = claim["evidence"], claim["sources"]
        if (
            not isinstance(evidence, list)
            or len(evidence) != 1
            or not isinstance(evidence[0], str)
            or evidence[0] not in UNCERTAINTY
        ):
            raise ValueError("Expected one explicit claim uncertainty classification")
        if (
            not isinstance(references, list)
            or not references
            or any(not isinstance(v, str) or v not in SOURCE_IDS for v in references)
            or len(set(references)) != len(references)
        ):
            raise ValueError("Claim requires unique existing source provenance")
        if claim["runtime_qualified"] is not False:
            raise ValueError("Research claims cannot become runtime-qualified")
    return ledger


def _populate(db: sqlite3.Connection, ledger: dict, ledger_hash: str) -> dict:
    """All table/column identifiers are fixed here; no SQL comes from the ledger."""
    ids: dict[str, list[str]] = {}

    def insert(table, values, identity):
        db.execute(
            f"INSERT INTO {table} ({', '.join(values)}) VALUES ({', '.join('?' for _ in values)})",
            tuple(values.values()),
        )
        ids.setdefault(table, []).append(identity)

    insert(
        "games",
        dict(
            game_id=GAME,
            title="Wizard101",
            identity_note="Maelstrom local identity, not an official game ID. "
            + ledger["scope"],
        ),
        GAME,
    )
    insert("domains", dict(game_id=GAME, domain_id="pets", title="Pets"), "pets")
    for key, title in sorted(set(CLAIM_ENTITIES.values())):
        entity = "maelstrom.entity.w101." + key
        insert(
            "entities",
            dict(
                entity_id=entity,
                game_id=GAME,
                domain_id="pets",
                kind="mechanic",
                display_name=title,
                identity_note="Local research concept; no verified pet/item identity or executable routine.",
            ),
            entity,
        )
    revisions = {}
    for source in ledger["sources"]:
        sid = "maelstrom.source.w101.pet-snacks." + source["id"]
        revision = (
            "maelstrom.source-revision.w101.pet-snacks."
            + source["id"]
            + ".1."
            + ledger_hash
        )
        revisions[source["id"]] = revision
        insert(
            "sources",
            dict(
                source_id=sid,
                kind=SOURCE_KINDS[source["kind"]],
                locator=source["url"],
                identity_note=canonical(
                    {"ledger_source_id": source["id"], "original_kind": source["kind"]}
                ),
            ),
            sid,
        )
        note = {
            **source,
            "ledger_sha256": ledger_hash,
            "mapping": MAPPING,
            "source_page_bytes_available": False,
        }
        insert(
            "source_revisions",
            dict(
                source_revision_id=revision,
                source_id=sid,
                revision_no=1,
                title="Ledger source: " + source["id"],
                publisher=None,
                published_on=source["published_on"],
                modified_on=source["modified_on"],
                retrieved_on=source["retrieved_on"],
                retrieved_timezone=source["retrieved_timezone"],
                content_sha256=None,
                revision_note=canonical(note),
            ),
            revision,
        )
    for index, claim in enumerate(ledger["claims"]):
        series = "maelstrom.assertion.w101.pet-snacks." + claim["id"]
        cid = "maelstrom.claim.w101.pet-snacks." + claim["id"] + ".1." + ledger_hash
        location = f"ledger-sha256:{ledger_hash}#/claims/{index}"
        insert(
            "claim_series",
            dict(
                series_id=series,
                game_id=GAME,
                entity_id="maelstrom.entity.w101." + CLAIM_ENTITIES[claim["id"]][0],
                predicate="pet-snacks." + claim["id"],
            ),
            series,
        )
        insert(
            "claims",
            dict(
                claim_id=cid,
                series_id=series,
                game_id=GAME,
                revision_no=1,
                statement=claim["statement"],
                value_kind="text",
                value_text=claim["statement"],
                uncertainty=claim["evidence"][0],
                confidence=None,
                game_version=None,
                region=None,
                platform=None,
                realm="unknown",
                effective_from=None,
                effective_until=None,
                primary_source_revision_id=revisions[claim["sources"][0]],
                source_location=location,
                authored_by="import",
            ),
            cid,
        )
        for source in claim["sources"]:
            eid = (
                "maelstrom.evidence.w101.pet-snacks."
                + claim["id"]
                + "."
                + source
                + "."
                + ledger_hash
            )
            insert(
                "claim_evidence",
                dict(
                    evidence_id=eid,
                    claim_id=cid,
                    source_revision_id=revisions[source],
                    stance="supports",
                    source_location=location,
                    note="Ledger-linked evidence only; no current-client qualification or automatic corroboration.",
                ),
                eid,
            )
    return {table: sorted(values) for table, values in ids.items()}


def _write_receipt(path: Path, receipt: dict) -> None:
    with path.open("x", encoding="utf-8", newline="\n") as stream:
        json.dump(receipt, stream, indent=2, ensure_ascii=False)
        stream.write("\n")
        stream.flush()
        os.fsync(stream.fileno())


@contextmanager
def _bundle_lock(path: Path):
    # Do not remove another invocation's lock if exclusive creation fails.
    stream = path.open("xb")
    try:
        with stream:
            yield
    finally:
        path.unlink()


def import_ledger(ledger_path: Path, output_root: Path, name: str) -> Path:
    if not re.fullmatch(r"[a-z0-9][a-z0-9-]{0,63}", name):
        raise ValueError(
            "Use a simple lowercase bundle name (1–64 letters/digits/hyphens)"
        )
    source = workspace_path(ledger_path, existing=True)
    root = workspace_path(output_root, existing=False)
    source_bytes = bounded_bytes(source, MAX_LEDGER_BYTES)
    ledger = validate_ledger(source_bytes)
    schema_bytes = bounded_bytes(SCHEMA, 1048576)
    ledger_hash, schema_hash = digest(source_bytes), digest(schema_bytes)
    root.mkdir(parents=True, exist_ok=True)
    root = workspace_path(root, existing=True)
    destination = root / name
    if destination.exists():
        raise FileExistsError(
            "Import destination already exists; existing databases are never updated"
        )
    lock_path = root / ("." + name + ".import.lock")
    staging = None
    # An exclusive per-bundle lock prevents two importer instances publishing the same name.
    with _bundle_lock(lock_path):
        try:
            if destination.exists():
                raise FileExistsError("Import destination already exists")
            staging = Path(tempfile.mkdtemp(prefix="." + name + ".staging-", dir=root))
            database = staging / "knowledge.sqlite"
            db = sqlite3.connect(database)
            try:
                db.executescript(schema_bytes.decode("utf-8-sig"))
                db.execute("PRAGMA foreign_keys = ON")
                db.execute("BEGIN IMMEDIATE")
                inserted = _populate(db, ledger, ledger_hash)
                if db.execute("PRAGMA foreign_key_check").fetchall():
                    raise ValueError("Imported data failed foreign-key verification")
                if db.execute("PRAGMA integrity_check").fetchone()[0] != "ok":
                    raise ValueError("Imported database failed integrity verification")
                db.commit()
            except Exception:
                db.rollback()
                raise
            finally:
                db.close()
            # Receipt generation happens after commit/close, outside the SQLite write lock.
            receipt = {
                "schema_version": 1,
                "mapping": MAPPING,
                "import_id": "maelstrom.import."
                + digest((MAPPING + ledger_hash + schema_hash).encode()),
                "committed_utc": datetime.now(timezone.utc).isoformat(),
                "source_ledger": source.relative_to(ROOT).as_posix(),
                "source_ledger_sha256": ledger_hash,
                "schema_sha256": schema_hash,
                "database_file": "knowledge.sqlite",
                "database_sha256": digest(database.read_bytes()),
                "research_date": ledger["research_date"],
                "research_timezone": ledger["research_timezone"],
                "ledger_scope": ledger["scope"],
                "inserted_ids": inserted,
                "row_counts": {
                    table: len(values) for table, values in inserted.items()
                },
                "unchanged_ids": [],
                "rejected_records": [],
                "merge_supported": False,
                "current_client_qualified": False,
                "runtime_enabled": False,
                "source_page_content_hashes_available": False,
                "qualification_note": "Historical/source scope is preserved in source revision notes. Unknown version/region/platform/realm/effective dates are not inferred. No observations, qualifications or runtime candidates were imported.",
                "not_imported": [
                    "retrieval_failures",
                    "current_client",
                    "proposed_configuration",
                    "proposed_contract",
                    "milestones",
                    "offline_qualification_cases",
                    "unresolved",
                ],
            }
            _write_receipt(staging / "import-receipt.json", receipt)
            if (
                digest(bounded_bytes(source, MAX_LEDGER_BYTES)) != ledger_hash
                or digest(bounded_bytes(SCHEMA, 1048576)) != schema_hash
            ):
                raise ValueError("Ledger or schema changed during import")
            if destination.exists():
                raise FileExistsError("Import destination appeared during staging")
            # Same-parent rename publishes database+receipt together; on Windows it refuses
            # an existing destination. Never rename/move an unvalidated external path.
            if staging.resolve().parent != root or destination.parent != root:
                raise ValueError("Publication escaped the output root")
            os.rename(staging, destination)
            staging = None
            return destination
        finally:
            if staging is not None and staging.exists():
                resolved_stage = staging.resolve()
                if resolved_stage.parent != root or not resolved_stage.is_relative_to(
                    root
                ):
                    raise ValueError("Refusing cleanup outside the output root")
                shutil.rmtree(resolved_stage)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ledger", type=Path, default=DEFAULT_LEDGER)
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--name", required=True)
    args = parser.parse_args()
    try:
        print(import_ledger(args.ledger, args.output_root, args.name))
    except (OSError, ValueError, sqlite3.Error) as error:
        parser.exit(1, f"Import refused: {error}\n")


if __name__ == "__main__":
    main()
