"""Bounded, provenance-preserving Conjuration observation replay; no desktop I/O."""

import argparse
from dataclasses import asdict
import hashlib
import json
import math
from pathlib import Path, PurePosixPath

from conjuration_memory import Board, Card, DispatchReceipt, MemoryMatch
from game_data import decode, read_bytes

MAX_EVENTS = 2000
MAX_IMAGE_BYTES = 8 * 1024 * 1024
MAX_TOTAL_IMAGE_BYTES = 64 * 1024 * 1024


def _object(value, required, optional=()):
    if not isinstance(value, dict) or not set(required) <= value.keys():
        raise ValueError("Missing fixture object fields")
    if value.keys() - set(required) - set(optional):
        raise ValueError("Unrecognized fixture fields")
    return value


def _name(value, label):
    if not isinstance(value, str) or not value.strip() or len(value) > 128:
        raise ValueError(label + " must be a nonempty string of at most 128 characters")
    return value


def _number(value):
    try:
        valid = type(value) in (int, float) and math.isfinite(value) and value >= 0
    except OverflowError:
        valid = False
    if not valid:
        raise ValueError("Event time must be finite and nonnegative")
    return value


def _group(value):
    if type(value) is not int or not 2 <= value <= 8:
        raise ValueError("Explicitly qualify a group size between 2 and 8")
    return value


def board_from_dict(value):
    _object(value, ("board_id", "revision", "phase", "cards"))
    if not isinstance(value["cards"], list) or not 2 <= len(value["cards"]) <= 64:
        raise ValueError("Provide a complete board with 2 to 64 card slots")
    cards = []
    for card in value["cards"]:
        _object(card, ("slot", "x", "y", "state"), ("identity",))
        cards.append(Card(**card))
    return Board(value["board_id"], value["revision"], value["phase"], tuple(cards))


def _image_source(source, synthetic, directory, image_cache, image_payloads=None):
    _object(source, ("annotation_origin", "image", "sha256"))
    if synthetic:
        if source["annotation_origin"] != "synthetic":
            raise ValueError(
                "Synthetic observations cannot claim recorded image evidence"
            )
        if source["image"] is None and source["sha256"] is None:
            return
    elif source["annotation_origin"] not in ("manual", "recognizer"):
        raise ValueError("Recorded observations need manual or recognizer provenance")
    name, digest = source["image"], source["sha256"]
    if not isinstance(name, str) or not name or "\\" in name or ":" in name:
        raise ValueError("Image must be a local relative path")
    pure = PurePosixPath(name)
    if pure.is_absolute() or ".." in pure.parts or pure.as_posix() != name:
        raise ValueError("Image must stay within its fixture directory")
    if (
        not isinstance(digest, str)
        or len(digest) != 64
        or any(c not in "0123456789abcdef" for c in digest)
    ):
        raise ValueError(
            "Image SHA-256 must contain 64 lowercase hexadecimal characters"
        )
    resolved = (directory / name).resolve(strict=True)
    if not resolved.is_relative_to(directory.resolve()) or not resolved.is_file():
        raise ValueError("Image escapes its fixture directory")
    if name not in image_cache:
        if len(image_cache) >= 64:
            raise ValueError("A focused fixture may reference at most 64 images")
        raw = image_payloads.get(name) if image_payloads is not None else None
        if raw is None:
            raw = read_bytes(resolved, MAX_IMAGE_BYTES)
        if not isinstance(raw, bytes) or len(raw) > MAX_IMAGE_BYTES:
            raise ValueError("Invalid or oversized image byte snapshot")
        if (
            sum(item[1] for item in image_cache.values()) + len(raw)
            > MAX_TOTAL_IMAGE_BYTES
        ):
            raise ValueError("Fixture image evidence exceeds the 64 MiB budget")
        image_cache[name] = (hashlib.sha256(raw).hexdigest(), len(raw))
        if image_payloads is not None:
            image_payloads[name] = raw
    if image_cache[name][0] != digest:
        raise ValueError("Image evidence hash does not match")


def replay_fixture(fixture, directory=Path("."), *, image_payloads=None):
    """Validate the complete stream before returning a report. Receipts are supplied,
    never invented; recorded recognizer labels remain candidates, not ground truth.
    Image bytes are hashed, not interpreted by this label-only replay adapter.
    """
    _object(
        fixture,
        (
            "schema_version",
            "fixture_id",
            "clock_origin",
            "synthetic",
            "board_id",
            "group_size",
            "events",
        ),
        ("notes",),
    )
    if type(fixture["schema_version"]) is not int or fixture["schema_version"] != 1:
        raise ValueError("Unsupported observation fixture schema")
    _name(fixture["fixture_id"], "fixture_id")
    _name(fixture["clock_origin"], "clock_origin")
    if "notes" in fixture and not isinstance(fixture["notes"], str):
        raise ValueError("Optional fixture notes must be text")
    if type(fixture["synthetic"]) is not bool:
        raise ValueError("Synthetic provenance must be explicit")
    solver = MemoryMatch(
        _name(fixture["board_id"], "board_id"), _group(fixture["group_size"])
    )
    events = fixture["events"]
    if not isinstance(events, list) or not 1 <= len(events) <= MAX_EVENTS:
        raise ValueError("A fixture needs 1 to 2000 events")
    trace, images, seen_boards = [], {}, {fixture["board_id"]}
    last_time = -1
    proposal_count = receipt_count = matched_groups = 0
    board_cleared = False
    for sequence, event in enumerate(events, 1):
        if not isinstance(event, dict):
            raise ValueError("Event must be an object")
        now = _number(event.get("time_ms"))
        if now < last_time:
            raise ValueError("Event times must be nondecreasing")
        last_time = now
        kind = event.get("kind")
        if kind == "observation":
            _object(event, ("kind", "time_ms", "board", "source"), ("expected",))
            _image_source(
                event["source"],
                fixture["synthetic"],
                Path(directory),
                images,
                image_payloads,
            )
            board = board_from_dict(event["board"])
            outcome = solver.observe(board)
            proposal = solver.propose()
            proposal_count += proposal is not None
            matched_groups += outcome is not None and outcome.kind == "match_observed"
            board_cleared = solver.reason == "board-cleared-observed"
            if "expected" in event:
                expected = _object(event["expected"], (), ("proposal_slot", "outcome"))
                actual = {
                    "proposal_slot": proposal.slot if proposal else None,
                    "outcome": outcome.kind if outcome else None,
                }
                if any(actual[key] != value for key, value in expected.items()):
                    raise ValueError(f"Fixture expectation failed at event {sequence}")
            trace.append(
                {
                    "sequence": sequence,
                    "time_ms": now,
                    "board_id": board.board_id,
                    "reason": solver.reason,
                    "proposal": asdict(proposal) if proposal else None,
                    "outcome": asdict(outcome) if outcome else None,
                    "source": event["source"],
                }
            )
        elif kind == "action_receipt":
            _object(event, ("kind", "time_ms", "proposal_id", "status"))
            solver.record_dispatch(
                DispatchReceipt(event["proposal_id"], event["status"])
            )
            receipt_count += 1
            trace.append(
                {
                    "sequence": sequence,
                    "time_ms": now,
                    "receipt": event,
                    "reason": solver.reason,
                }
            )
        elif kind == "reset":
            _object(event, ("kind", "time_ms", "board_id", "group_size"))
            name = _name(event["board_id"], "board_id")
            if name in seen_boards:
                raise ValueError("Reset cannot reuse a previous board identity")
            solver.reset(name, _group(event["group_size"]))
            seen_boards.add(name)
            board_cleared = False
            trace.append({"sequence": sequence, "time_ms": now, "reset": name})
        else:
            raise ValueError("Unsupported fixture event kind")
    return {
        "schema_version": 1,
        "fixture_id": fixture["fixture_id"],
        "evidence_mode": (
            "synthetic-label-replay"
            if fixture["synthetic"]
            else "recorded-label-replay"
        ),
        "desktop_dispatch_count": 0,
        "live_qualified": False,
        "proposal_count": proposal_count,
        "supplied_receipt_count": receipt_count,
        "matched_groups_observed": matched_groups,
        "last_board_cleared": board_cleared,
        "game_win_verified": False,
        "final_reason": solver.reason,
        "images": {name: digest for name, (digest, _) in images.items()},
        "trace": trace,
    }


def load_and_replay(path):
    path = Path(path)
    raw = read_bytes(path)
    report = replay_fixture(decode(raw), path.parent)
    report["sha256_fixture"] = hashlib.sha256(raw).hexdigest()
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("fixture", type=Path)
    parser.add_argument("report", type=Path)
    args = parser.parse_args(argv)
    try:
        report = load_and_replay(args.fixture)
        with args.report.open("x", encoding="utf-8") as output:
            json.dump(report, output, indent=2, allow_nan=False)
            output.write("\n")
    except (OSError, ValueError, TypeError, KeyError) as error:
        print("Conjuration replay failed: " + str(error))
        return 1
    print(f"Replayed {len(report['trace'])} events; no desktop inputs sent.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
