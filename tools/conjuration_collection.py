"""Prepare and check a bounded Conjuration annotation draft; never records or plays."""

import argparse
import hashlib
import json
import math
import os
from pathlib import Path, PurePosixPath
import struct
import tempfile
import zlib

from conjuration_memory import MemoryMatch
from conjuration_observation_fixtures import _name, _object, board_from_dict
from game_data import decode, read_bytes

MAX_RECORDINGS = 8
MAX_FRAMES = 64
MAX_IMAGE_BYTES = 8 * 1024 * 1024
MAX_TOTAL_BYTES = 64 * 1024 * 1024
STAGES = (
    "instructions",
    "preview",
    "hidden",
    "flip",
    "mismatch",
    "match",
    "removed",
    "result",
    "new_board",
)
EVIDENCE_KINDS = {"unknown", "recorded", "synthetic"}
SURFACES = {"unknown", "client", "outer_window"}


def _hash(raw):
    return hashlib.sha256(raw).hexdigest()


def _version(data, kind=None):
    if type(data.get("schema_version")) is not int or data["schema_version"] != 1:
        raise ValueError("Unsupported collection schema")
    if kind is not None and data.get("kind") != kind:
        raise ValueError("Unsupported collection kind")


def _time(value):
    if value is None:
        return None
    try:
        valid = (
            type(value) in (int, float) and math.isfinite(value) and 0 <= value <= 1e15
        )
    except OverflowError:
        valid = False
    if not valid:
        raise ValueError("Timestamp must be unknown or a bounded nonnegative number")
    return value


def _contained(directory, name):
    if (
        not isinstance(name, str)
        or not name
        or len(name) > 512
        or "\\" in name
        or ":" in name
    ):
        raise ValueError("Source must be a bounded relative local path")
    pure = PurePosixPath(name)
    if pure.is_absolute() or ".." in pure.parts or pure.as_posix() != name:
        raise ValueError("Source path must stay within its directory")
    resolved = (directory / name).resolve(strict=True)
    if not resolved.is_relative_to(directory.resolve()) or not resolved.is_file():
        raise ValueError("Source escapes its directory")
    return resolved


def _digest(value):
    if (
        not isinstance(value, str)
        or len(value) != 64
        or any(c not in "0123456789abcdefABCDEF" for c in value)
    ):
        raise ValueError("A complete SHA-256 source digest is required")
    return value.lower()


def png_metadata(raw):
    """Check PNG chunks/CRCs and dimensions without decoding image pixels.

    Alpha presence is metadata, never a claim that all pixels are opaque.
    Compressed pixel data still needs the strict image decoder before recognition.
    """
    if (
        not isinstance(raw, bytes)
        or len(raw) > MAX_IMAGE_BYTES
        or raw[:8] != b"\x89PNG\r\n\x1a\n"
    ):
        raise ValueError("Supply a bounded original PNG")
    offset, header, has_data, transparency, ended = 8, None, False, False, False
    while offset < len(raw):
        if offset + 12 > len(raw):
            raise ValueError("Truncated PNG chunk")
        length = struct.unpack_from(">I", raw, offset)[0]
        end = offset + 12 + length
        if end > len(raw):
            raise ValueError("PNG chunk exceeds the available bytes")
        kind, body = raw[offset + 4 : offset + 8], raw[offset + 8 : offset + 8 + length]
        checksum = struct.unpack_from(">I", raw, offset + 8 + length)[0]
        if zlib.crc32(kind + body) & 0xFFFFFFFF != checksum:
            raise ValueError("PNG chunk checksum mismatch")
        if header is None and kind != b"IHDR":
            raise ValueError("PNG must begin with its image header")
        if kind == b"IHDR":
            if header is not None or length != 13:
                raise ValueError("Invalid PNG image header")
            width, height, depth, color, compression, filtering, interlace = (
                struct.unpack(">IIBBBBB", body)
            )
            valid_depths = {
                0: {1, 2, 4, 8, 16},
                2: {8, 16},
                3: {1, 2, 4, 8},
                4: {8, 16},
                6: {8, 16},
            }
            if (
                not 1 <= width <= 8192
                or not 1 <= height <= 8192
                or width * height > 4_194_304
            ):
                raise ValueError("PNG dimensions exceed the collection budget")
            if (
                color not in valid_depths
                or depth not in valid_depths[color]
                or compression
                or filtering
                or interlace not in (0, 1)
            ):
                raise ValueError("Unsupported PNG header")
            header = {
                "width": width,
                "height": height,
                "bit_depth": depth,
                "color_type": color,
            }
        elif kind == b"IDAT":
            has_data = True
        elif kind == b"tRNS":
            transparency = True
        elif kind == b"acTL":
            raise ValueError(
                "Select explicit still frames; animated PNGs are not supported"
            )
        elif kind == b"IEND":
            if length != 0 or end != len(raw):
                raise ValueError("Invalid PNG end marker or trailing data")
            ended = True
            break
        offset = end
    if header is None or not has_data or not ended:
        raise ValueError("PNG lacks image data or an end marker")
    header["alpha"] = (
        "present-unmeasured"
        if header["color_type"] in (4, 6) or transparency
        else "absent"
    )
    header["pixel_decode_performed"] = False
    return header


def build_draft(recording_paths, *, evidence_kind="unknown", surface_kind="unknown"):
    """Read only explicitly listed Version-1 PNG recordings; return draft and bytes.

    Evidence kind and surface are caller declarations, not independently proven.
    Every annotation begins unknown, even if a recorder supplied a State string.
    """
    if evidence_kind not in EVIDENCE_KINDS or surface_kind not in SURFACES:
        raise ValueError("Declare a supported evidence and surface kind")
    paths = list(recording_paths)
    if not 1 <= len(paths) <= MAX_RECORDINGS:
        raise ValueError("Supply 1 to 8 explicit recording.json paths")
    episodes, payloads, seen, frame_count, image_bytes = [], {}, set(), 0, 0
    for source in paths:
        path = Path(source).resolve(strict=True)
        raw = read_bytes(path)
        recording = decode(raw)
        if (
            not isinstance(recording, dict)
            or type(recording.get("Version")) is not int
            or recording["Version"] != 1
        ):
            raise ValueError(
                "Only existing Version-1 PNG recording manifests are supported"
            )
        episode_id = _name(recording.get("Id"), "recording Id")
        if episode_id in seen:
            raise ValueError("Each recording must have a distinct episode ID")
        seen.add(episode_id)
        if type(recording.get("Complete")) is not bool:
            raise ValueError("Recording completion must be explicitly recorded")
        frames = recording.get("Frames")
        if (
            not isinstance(frames, list)
            or not frames
            or frame_count + len(frames) > MAX_FRAMES
        ):
            raise ValueError("The collection supports 1 to 64 explicitly listed frames")
        recording_name = f"metadata/{len(episodes):02d}-{_hash(raw)}.json"
        payloads[recording_name] = raw
        episode = {
            "episode_id": episode_id,
            "declared_evidence_kind": evidence_kind,
            "split": "unassigned",
            "surface_kind": surface_kind,
            "client_rect": None,
            "ui_scale": None,
            "timestamp_basis": "recording-TimeMs-unqualified",
            "recording": {
                "file": recording_name,
                "sha256": _hash(raw),
                "original_path": str(path),
                "started_utc": recording.get("StartedUtc"),
                "complete": recording["Complete"],
                "source_workspace": recording.get("Workspace"),
            },
            "input_receipts": [],
            "frames": [],
        }
        last_time = -1
        for index, frame in enumerate(frames):
            if not isinstance(frame, dict):
                raise ValueError("Recording frame must be an object")
            actual_path = _contained(path.parent, frame.get("File"))
            image = read_bytes(actual_path, MAX_IMAGE_BYTES)
            image_bytes += len(image)
            if image_bytes > MAX_TOTAL_BYTES:
                raise ValueError(
                    "Collection source reads exceed the 64 MiB image budget"
                )
            digest = _hash(image)
            if digest != _digest(frame.get("Sha256")):
                raise ValueError(
                    "Recording image hash does not match the original bytes"
                )
            metadata = png_metadata(image)
            if (
                type(frame.get("Width")) is not int
                or type(frame.get("Height")) is not int
                or [frame["Width"], frame["Height"]]
                != [metadata["width"], metadata["height"]]
            ):
                raise ValueError(
                    "Recording dimensions disagree with original PNG metadata"
                )
            stamp = _time(frame.get("TimeMs"))
            if stamp is not None:
                if stamp <= last_time:
                    raise ValueError(
                        "Known frame timestamps must strictly increase per episode"
                    )
                last_time = stamp
            name = f"images/{digest}.png"
            payloads[name] = image
            episode["frames"].append(
                {
                    "frame_id": f"e{len(episodes)}-f{index}",
                    "image": name,
                    "sha256": digest,
                    "time_ms": stamp,
                    "width": metadata["width"],
                    "height": metadata["height"],
                    "png": metadata,
                    "original_file": frame["File"],
                    "recording_frame_index": index,
                    "stage": "unknown",
                    "review": "unreviewed",
                    "board": None,
                    "group_size": None,
                    "receipt_ids": [],
                }
            )
        frame_count += len(frames)
        episodes.append(episode)
    draft = {
        "schema_version": 1,
        "kind": "maelstrom-conjuration-card-collection",
        "collection_id": "conjuration-"
        + _hash("|".join(e["recording"]["sha256"] for e in episodes).encode())[:16],
        "rules": {"removed_state": "unknown", "evidence_frame_ids": []},
        "episodes": episodes,
    }
    return draft, payloads


def _source_bytes(directory, name, expected, snapshots, cache, total):
    if name not in cache:
        if snapshots is not None:
            raw = snapshots.get(name)
            if not isinstance(raw, bytes):
                raise ValueError("Missing immutable collection source snapshot")
        else:
            raw = read_bytes(_contained(directory, name), MAX_IMAGE_BYTES)
        if (
            len(raw) > MAX_IMAGE_BYTES
            or total[0] + len(raw) > MAX_TOTAL_BYTES + MAX_RECORDINGS * 1_048_576
        ):
            raise ValueError("Collection snapshot exceeds its byte budget")
        total[0] += len(raw)
        cache[name] = raw
    raw = cache[name]
    if _hash(raw) != _digest(expected):
        raise ValueError("Collection source hash does not match")
    return raw


def _check_board_continuity(entries, gaps):
    current, seen, new_board_phase = None, {}, False
    for entry in entries:
        board = entry["board"]
        if board is None:
            continue
        identity, stage = board["board_id"], entry["stage"]
        reason = None
        if identity != current:
            if identity in seen:
                reason = "retired-board-identity-reused"
            elif current is None and stage == "new_board":
                reason = "new-board-prior-context-missing"
            elif current is not None and stage != "new_board":
                reason = "board-changed-without-new-board-observation"
            current, new_board_phase = identity, stage == "new_board"
        elif stage == "new_board" and not new_board_phase:
            reason = "new-board-must-establish-fresh-identity"
        geometry = tuple(
            sorted((card["slot"], card["x"], card["y"]) for card in board["cards"])
        )
        previous = seen.get(identity)
        if previous and (geometry, entry["group_size"]) != previous[:2]:
            reason = "board-geometry-or-group-size-changed-without-reset"
        elif previous and board["revision"] <= previous[2]:
            reason = "board-revision-not-increasing"
        seen[identity] = (geometry, entry["group_size"], board["revision"])
        if stage != "new_board":
            new_board_phase = False
        if reason:
            gaps.append({"frame_id": entry["frame_id"], "reason": reason})


def _card_annotation(frame):
    board = frame["board"]
    if board is None:
        return None
    # Episode-specific board IDs/revisions are provenance, not pixel labels.
    return (
        board["phase"],
        frame["group_size"],
        tuple(
            sorted(
                (
                    card["slot"],
                    card["x"],
                    card["y"],
                    card["state"],
                    card.get("identity"),
                )
                for card in board["cards"]
            )
        ),
    )


def check_collection(draft, directory=Path("."), *, snapshots=None):
    """Check annotation structure and distinct declared-recorded evidence coverage.

    Coverage is a curation checklist, not verified gameplay or recognition accuracy.
    Synthetic/unreviewed/duplicate/conflicting/leaked frames cannot satisfy it.
    """
    _object(draft, ("schema_version", "kind", "collection_id", "rules", "episodes"))
    _version(draft, "maelstrom-conjuration-card-collection")
    _name(draft["collection_id"], "collection_id")
    rules = _object(draft["rules"], ("removed_state", "evidence_frame_ids"))
    if rules["removed_state"] not in {"unknown", "positive_marker", "not_used"}:
        raise ValueError("Removed-state rule must remain explicit or unknown")
    if not isinstance(rules["evidence_frame_ids"], list):
        raise ValueError("Rule evidence must identify reviewed instructions frames")
    episodes = draft["episodes"]
    if not isinstance(episodes, list) or not 1 <= len(episodes) <= MAX_RECORDINGS:
        raise ValueError("A collection needs 1 to 8 episodes")
    frames, cache, total, episode_ids, gaps = [], {}, [0], set(), []
    image_sizes = {}
    for episode in episodes:
        _object(
            episode,
            (
                "episode_id",
                "declared_evidence_kind",
                "split",
                "surface_kind",
                "client_rect",
                "ui_scale",
                "timestamp_basis",
                "recording",
                "input_receipts",
                "frames",
            ),
        )
        eid = _name(episode["episode_id"], "episode ID")
        if eid in episode_ids:
            raise ValueError("Episode IDs must be unique")
        episode_ids.add(eid)
        if (
            episode["declared_evidence_kind"] not in EVIDENCE_KINDS
            or episode["split"] not in {"unassigned", "calibration", "held_out"}
            or episode["surface_kind"] not in SURFACES
        ):
            raise ValueError("Invalid evidence declaration, episode split or surface")
        if episode["timestamp_basis"] not in {
            "recording-TimeMs-unqualified",
            "source-capture-ms",
            "observer-callback-ms",
            "unknown",
        }:
            raise ValueError("Invalid timestamp provenance")
        if episode["ui_scale"] is not None:
            _name(episode["ui_scale"], "UI scale")
        recording = _object(
            episode["recording"],
            (
                "file",
                "sha256",
                "original_path",
                "started_utc",
                "complete",
                "source_workspace",
            ),
        )
        metadata_raw = _source_bytes(
            Path(directory),
            recording["file"],
            recording["sha256"],
            snapshots,
            cache,
            total,
        )
        if len(metadata_raw) > 1_048_576:
            raise ValueError("Original recording metadata exceeds 1 MiB")
        original = decode(metadata_raw)
        if (
            not isinstance(original, dict)
            or original.get("Id") != eid
            or type(original.get("Version")) is not int
            or original["Version"] != 1
            or type(original.get("Complete")) is not bool
        ):
            raise ValueError("Recording metadata does not identify this episode")
        if any(
            recording[key] != original.get(source)
            for key, source in (
                ("started_utc", "StartedUtc"),
                ("complete", "Complete"),
                ("source_workspace", "Workspace"),
            )
        ):
            raise ValueError(
                "Original recording metadata was altered in the annotation draft"
            )
        entries = episode["frames"]
        if (
            not isinstance(entries, list)
            or not entries
            or len(frames) + len(entries) > MAX_FRAMES
        ):
            raise ValueError("A collection supports at most 64 frames")
        if not isinstance(original.get("Frames"), list) or len(entries) != len(
            original["Frames"]
        ):
            raise ValueError("Preserve every explicitly selected recording frame")
        receipts = episode["input_receipts"]
        if not isinstance(receipts, list) or len(receipts) > 256:
            raise ValueError("Keep at most 256 explicit input receipts per episode")
        receipt_ids = set()
        for receipt in receipts:
            _object(
                receipt,
                ("receipt_id", "time_ms", "proposal_id", "status", "origin", "action"),
            )
            rid = _name(receipt["receipt_id"], "receipt ID")
            if (
                rid in receipt_ids
                or receipt["status"] not in {"dispatched", "rejected", "unknown"}
                or receipt["origin"]
                not in {"manual_observation", "maelstrom_receipt", "unknown"}
            ):
                raise ValueError("Invalid or duplicate supplied input receipt")
            receipt_ids.add(rid)
            _time(receipt["time_ms"])
            _name(receipt["action"], "input action description")
            if receipt["proposal_id"] is not None:
                if (
                    not isinstance(receipt["proposal_id"], str)
                    or not 1 <= len(receipt["proposal_id"]) <= 256
                ):
                    raise ValueError("Invalid original proposal ID")
        last_time = -1
        for index, entry in enumerate(entries):
            _object(
                entry,
                (
                    "frame_id",
                    "image",
                    "sha256",
                    "time_ms",
                    "width",
                    "height",
                    "png",
                    "original_file",
                    "recording_frame_index",
                    "stage",
                    "review",
                    "board",
                    "group_size",
                    "receipt_ids",
                ),
            )
            _name(entry["frame_id"], "frame ID")
            raw = _source_bytes(
                Path(directory),
                entry["image"],
                entry["sha256"],
                snapshots,
                cache,
                total,
            )
            image_sizes[entry["image"]] = len(raw)
            if sum(image_sizes.values()) > MAX_TOTAL_BYTES:
                raise ValueError("Collection image snapshots exceed the 64 MiB budget")
            png = png_metadata(raw)
            if png != entry["png"] or [entry["width"], entry["height"]] != [
                png["width"],
                png["height"],
            ]:
                raise ValueError(
                    "Image geometry/alpha metadata does not match the original"
                )
            source = original["Frames"][index]
            if (
                not isinstance(source, dict)
                or type(entry["recording_frame_index"]) is not int
                or entry["recording_frame_index"] != index
                or entry["original_file"] != source.get("File")
                or entry["sha256"] != _digest(source.get("Sha256"))
                or entry["time_ms"] != source.get("TimeMs")
                or source.get("Width") != png["width"]
                or source.get("Height") != png["height"]
                or type(source.get("Width")) is not int
                or type(source.get("Height")) is not int
            ):
                raise ValueError(
                    "Frame provenance no longer matches original recording metadata"
                )
            stamp = _time(entry["time_ms"])
            if stamp is not None:
                if stamp <= last_time:
                    raise ValueError(
                        "Frame timestamps must strictly increase within an episode"
                    )
                last_time = stamp
            if entry["stage"] not in ("unknown",) + STAGES or entry["review"] not in {
                "unreviewed",
                "manual",
                "recognizer",
            }:
                raise ValueError("Invalid curation stage or review provenance")
            links = entry["receipt_ids"]
            if (
                not isinstance(links, list)
                or len(links) > 16
                or any(link not in receipt_ids for link in links)
                or len(set(links)) != len(links)
            ):
                raise ValueError(
                    "Receipt links must identify distinct supplied receipts in this episode"
                )
            if entry["board"] is not None:
                board = board_from_dict(entry["board"])
                MemoryMatch(board.board_id, entry["group_size"]).observe(board)
                if any(
                    card.x >= png["width"] or card.y >= png["height"]
                    for card in board.cards
                ):
                    raise ValueError(
                        "Annotated card centers must remain inside their source image"
                    )
                states = [card.state for card in board.cards]
                stage = entry["stage"]
                consistent = {
                    "preview": board.phase == "preview"
                    and states.count("face_up") >= 2,
                    "hidden": board.phase == "active"
                    and states.count("hidden") >= 2
                    and "face_up" not in states,
                    "flip": board.phase in {"active", "animation"}
                    and "face_up" in states,
                    "removed": board.phase == "active" and "removed" in states,
                }
                if stage in consistent and not consistent[stage]:
                    raise ValueError(
                        "Curation stage conflicts with its explicit board observation"
                    )
                if "unknown" in states:
                    gaps.append(
                        {
                            "frame_id": entry["frame_id"],
                            "reason": "card-labels-incomplete",
                        }
                    )
            elif entry["group_size"] is not None:
                raise ValueError(
                    "A group size must be scoped to an explicit board observation"
                )
            frames.append((episode, entry))
        _check_board_continuity(entries, gaps)
        rect = episode["client_rect"]
        if rect is not None:
            if (
                not isinstance(rect, list)
                or len(rect) != 4
                or any(type(v) is not int for v in rect)
                or min(rect[:2]) < 0
                or min(rect[2:]) <= 0
            ):
                raise ValueError(
                    "Client rectangle must be an explicit integer source-image ROI"
                )
            if any(
                rect[0] + rect[2] > item["width"] or rect[1] + rect[3] > item["height"]
                for item in entries
            ):
                raise ValueError("Client rectangle extends beyond a source image")
            if any(
                not (
                    rect[0] <= card["x"] < rect[0] + rect[2]
                    and rect[1] <= card["y"] < rect[1] + rect[3]
                )
                for item in entries
                if item["board"] is not None
                for card in item["board"]["cards"]
            ):
                raise ValueError(
                    "Annotated card centers extend beyond the qualified client rectangle"
                )
        for condition, label in (
            (
                episode["declared_evidence_kind"] != "recorded",
                "recorded-provenance-not-declared",
            ),
            (episode["split"] == "unassigned", "episode-split-unassigned"),
            (episode["surface_kind"] == "unknown", "capture-surface-unknown"),
            (
                episode["surface_kind"] == "outer_window" and rect is None,
                "client-rectangle-unknown",
            ),
            (
                episode["timestamp_basis"] != "source-capture-ms",
                "source-timestamp-alignment-unqualified",
            ),
            (episode["ui_scale"] is None, "ui-scale-unknown"),
            (not recording["complete"], "recording-incomplete"),
        ):
            if condition:
                gaps.append({"episode_id": eid, "reason": label})
    ids = [frame["frame_id"] for _, frame in frames]
    if len(ids) != len(set(ids)):
        raise ValueError("Frame IDs must be unique across the collection")
    by_id = {frame["frame_id"]: (episode, frame) for episode, frame in frames}
    for frame_id in rules["evidence_frame_ids"]:
        if (
            frame_id not in by_id
            or by_id[frame_id][1]["stage"] != "instructions"
            or by_id[frame_id][1]["review"] != "manual"
        ):
            raise ValueError("Rule evidence must link manually reviewed instructions")
    if rules["removed_state"] == "unknown" or not rules["evidence_frame_ids"]:
        gaps.append({"reason": "removed-card-representation-rule-unqualified"})
    hashes = {}
    for episode, frame in frames:
        hashes.setdefault(frame["sha256"], []).append((episode, frame))
    excluded = {}
    coverage = {
        split: {stage: 0 for stage in STAGES} for split in ("calibration", "held_out")
    }
    for digest, occurrences in hashes.items():
        reviewed = [
            (e, f)
            for e, f in occurrences
            if e["declared_evidence_kind"] == "recorded" and f["review"] == "manual"
        ]
        assigned_splits = {e["split"] for e, _ in occurrences} & {
            "calibration",
            "held_out",
        }
        splits = {e["split"] for e, _ in reviewed} & {"calibration", "held_out"}
        stages = {f["stage"] for _, f in reviewed} - {"unknown"}
        if any(e["declared_evidence_kind"] == "synthetic" for e, _ in occurrences):
            excluded[digest] = "synthetic-source-cannot-satisfy-recorded-coverage"
            continue
        if len(assigned_splits) > 1 or len(stages) > 1:
            excluded[digest] = (
                "split-leakage"
                if len(assigned_splits) > 1
                else "conflicting-labels-on-identical-pixels"
            )
            continue
        annotations = {
            _card_annotation(frame)
            for _, frame in reviewed
            if frame["board"] is not None
        }
        if len(annotations) > 1:
            excluded[digest] = "conflicting-card-annotations-on-identical-pixels"
            continue
        for split in splits:
            if len(stages) == 1:
                coverage[split][next(iter(stages))] += 1
    required = [
        stage
        for stage in STAGES
        if stage != "removed" or rules["removed_state"] != "not_used"
    ]
    for split in coverage:
        for stage in required:
            if coverage[split][stage] == 0:
                gaps.append(
                    {
                        "split": split,
                        "reason": "missing-distinct-manual-stage",
                        "stage": stage,
                    }
                )
    for episode, frame in frames:
        if (
            frame["stage"] not in {"unknown", "instructions", "result"}
            and frame["board"] is None
        ):
            gaps.append(
                {
                    "frame_id": frame["frame_id"],
                    "reason": "board-and-card-labels-missing",
                }
            )
        if frame["stage"] in {"flip", "match", "mismatch"}:
            linked = [
                r
                for r in episode["input_receipts"]
                if r["receipt_id"] in frame["receipt_ids"]
            ]
            if not linked:
                gaps.append(
                    {"frame_id": frame["frame_id"], "reason": "input-receipts-missing"}
                )
            elif not any(
                r["status"] == "dispatched"
                and r["origin"] != "unknown"
                and r["time_ms"] is not None
                and frame["time_ms"] is not None
                and r["time_ms"] <= frame["time_ms"]
                for r in linked
            ):
                gaps.append(
                    {
                        "frame_id": frame["frame_id"],
                        "reason": "input-dispatch-evidence-incomplete",
                    }
                )
        if frame["png"]["alpha"] != "absent":
            gaps.append(
                {"frame_id": frame["frame_id"], "reason": "alpha-opacity-not-qualified"}
            )
        if frame["time_ms"] is None:
            gaps.append(
                {"frame_id": frame["frame_id"], "reason": "frame-timestamp-unknown"}
            )
    if len(hashes) < 2:
        gaps.append({"reason": "one-unique-image-cannot-demonstrate-card-transitions"})
    return {
        "schema_version": 1,
        "collection_id": draft["collection_id"],
        "episode_count": len(episodes),
        "source_frame_count": len(frames),
        "unique_image_count": len(hashes),
        "duplicate_frame_count": len(frames) - len(hashes),
        "source_snapshot_bytes": total[0],
        "declared_recorded_manual_coverage": coverage,
        "excluded_hashes": excluded,
        "gaps": gaps,
        "collection_checklist_met": not gaps and not excluded,
        "declaration_is_independent_provenance_proof": False,
        "recognition_accuracy_verified": False,
        "gameplay_outcome_verified": False,
        "runtime_enabled": False,
        "desktop_dispatch_count": 0,
        "byte_duplicates_may_miss_visually_identical_reencodings": True,
    }


def prepare_recordings(
    recording_paths, output, *, evidence_kind="unknown", surface_kind="unknown"
):
    """Publish a new local draft only after validating all original snapshots."""
    destination = Path(output).absolute()
    parent = destination.parent.resolve(strict=True)
    destination = parent / destination.name
    if destination.exists() or destination.is_symlink():
        raise FileExistsError("Refusing to overwrite an existing collection workspace")
    draft, payloads = build_draft(
        recording_paths, evidence_kind=evidence_kind, surface_kind=surface_kind
    )
    report = check_collection(draft, snapshots=payloads)
    with tempfile.TemporaryDirectory(
        prefix=".conjuration-collection-", dir=parent
    ) as staging:
        staged = Path(staging).resolve() / "draft"
        if not staged.is_relative_to(parent):
            raise ValueError("Draft staging escaped its selected parent")
        staged.mkdir()
        for name, raw in payloads.items():
            target = staged / name
            target.parent.mkdir(exist_ok=True)
            target.write_bytes(raw)
        for name, value in (("annotations.json", draft), ("completeness.json", report)):
            (staged / name).write_text(
                json.dumps(value, indent=2, allow_nan=False) + "\n", encoding="utf-8"
            )
        if destination.exists() or destination.is_symlink():
            raise FileExistsError("Collection destination appeared during preparation")
        os.rename(staged, destination)
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    prepare = commands.add_parser("prepare")
    prepare.add_argument("--recording", action="append", required=True, type=Path)
    prepare.add_argument("--output", required=True, type=Path)
    prepare.add_argument(
        "--evidence-kind", choices=sorted(EVIDENCE_KINDS), default="unknown"
    )
    prepare.add_argument("--surface-kind", choices=sorted(SURFACES), default="unknown")
    check = commands.add_parser("check")
    check.add_argument("annotations", type=Path)
    try:
        args = parser.parse_args(argv)
        if args.command == "prepare":
            report = prepare_recordings(
                args.recording,
                args.output,
                evidence_kind=args.evidence_kind,
                surface_kind=args.surface_kind,
            )
        else:
            report = check_collection(
                decode(read_bytes(args.annotations)), args.annotations.parent
            )
        print(json.dumps(report, indent=2, allow_nan=False))
        return 0
    except (OSError, ValueError, TypeError, KeyError) as error:
        print("Collection preparation/check failed: " + str(error))
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
