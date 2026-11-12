"""Local draft pack authoring and persistence. Packs are data, never executable."""

from __future__ import annotations

import json
import base64
import binascii
import math
import re
import tempfile
from pathlib import Path
from typing import Any
from copy import deepcopy

from game_data import decode, read_bytes

PACK_SCHEMA_VERSION = 1
_ID = re.compile(r"^[a-z0-9][a-z0-9._-]{0,63}$")
_RESERVED = {
    "con",
    "prn",
    "aux",
    "nul",
    *(f"com{i}" for i in range(1, 10)),
    *(f"lpt{i}" for i in range(1, 10)),
}


class PackError(ValueError):
    pass


def validate_context(context: Any) -> None:
    """Validate versioned data only; this does not verify the claimed game identity."""
    if (
        not isinstance(context, dict)
        or set(context)
        not in (
            {
                "version",
                "goal",
                "states",
                "action_states",
                "identity",
                "constraints",
                "state_masks",
            },
            {
                "version",
                "goal",
                "states",
                "action_states",
                "identity",
                "constraints",
                "state_masks",
                "avoidance_policy",
            },
        )
        or type(context["version"]) is not int
        or context["version"] != 1
    ):
        raise PackError("Unsupported game context")
    if "avoidance_policy" in context and (
        not isinstance(context["avoidance_policy"], str)
        or not _ID.fullmatch(context["avoidance_policy"])
    ):
        raise PackError("Invalid avoidance policy ID")
    if (
        not isinstance(context["goal"], str)
        or not 1 <= len(context["goal"].strip()) <= 200
    ):
        raise PackError("Context goal must be 1 to 200 characters")
    states, active = context["states"], context["action_states"]
    for values in (states, active):
        if (
            not isinstance(values, list)
            or not 1 <= len(values) <= 32
            or any(not isinstance(v, str) or not _ID.fullmatch(v) for v in values)
            or len(values) != len(set(values))
        ):
            raise PackError("Context states must be unique IDs")
    if not set(active).issubset(states):
        raise PackError("Action states must appear in context states")
    identity = context["identity"]
    if (
        not isinstance(identity, dict)
        or set(identity) != {"minimum_score", "signals"}
        or not isinstance(identity["signals"], dict)
        or set(identity["signals"]) != {"owned_session", "viewport", "visual_state"}
    ):
        raise PackError("Invalid identification rubric")
    weights = list(identity["signals"].values())
    if (
        any(type(v) is not int or not 1 <= v <= 10 for v in weights)
        or type(identity["minimum_score"]) is not int
        or not 1 <= identity["minimum_score"] <= sum(weights)
    ):
        raise PackError("Invalid identification weights")
    constraints = context["constraints"]
    if (
        not isinstance(constraints, dict)
        or not 1 <= len(constraints) <= 32
        or any(
            not isinstance(k, str)
            or not _ID.fullmatch(k)
            or type(v) not in (int, float)
            or not math.isfinite(v)
            or not 0 <= v <= 1_000_000
            for k, v in constraints.items()
        )
    ):
        raise PackError("Invalid context constraints")
    if context.get("avoidance_policy") == "directional-configurable-v30" and (
        type(constraints.get("risk_weight_percent")) is not int
        or not 25 <= constraints["risk_weight_percent"] <= 400
        or type(constraints.get("edge_weight_percent")) is not int
        or not 0 <= constraints["edge_weight_percent"] <= 300
    ):
        raise PackError("Invalid directional planning weights")
    masks = context["state_masks"]
    if (
        not isinstance(masks, dict)
        or set(masks) != {"x", "y", "width", "height", "data"}
        or any(type(masks[k]) is not int for k in ("x", "y", "width", "height"))
    ):
        raise PackError("Invalid state masks")
    x, y, w, h = (masks[k] for k in ("x", "y", "width", "height"))
    if (
        x < 0
        or y < 0
        or w < 1
        or h < 1
        or x + w > 160
        or y + h > 250
        or not isinstance(masks["data"], dict)
        or not set(masks["data"]).issubset(states)
        or len(masks["data"]) > 16
    ):
        raise PackError("State masks exceed their bounds")
    try:
        if any(
            not isinstance(v, str)
            or len(base64.b64decode(v, validate=True)) != (w * h + 7) // 8
            for v in masks["data"].values()
        ):
            raise PackError("Invalid state mask data")
    except (binascii.Error, ValueError) as exc:
        raise PackError("Invalid state mask data") from exc


def validate_pack(pack: Any) -> dict[str, Any]:
    if not isinstance(pack, dict):
        raise PackError("Pack must be a JSON object")
    if "recognition" in pack:
        raise PackError(
            "Recognition assets are not supported by this pack version; "
            "saving would not preserve their templates"
        )
    if "context" in pack:
        validate_context(pack["context"])
    if type(pack.get("schema_version")) is not int or pack["schema_version"] not in (
        1,
        2,
    ):
        raise PackError("Unsupported pack schema_version")
    for field in ("pack_id", "game_id", "version"):
        value = pack.get(field)
        if (
            not isinstance(value, str)
            or not _ID.fullmatch(value)
            or value.endswith(".")
            or value.split(".")[0] in _RESERVED
        ):
            raise PackError(f"{field} must be a lowercase ID (letters, digits, ._-)")
    if not isinstance(pack.get("title"), str) or not pack["title"].strip():
        raise PackError("title is required")
    if pack.get("status", "draft") != "draft":
        raise PackError(
            "Only draft packs are supported; replay reports do not grant qualification"
        )
    for field in ("capabilities", "skills", "fixtures", "dependencies"):
        if not isinstance(pack.get(field), list):
            raise PackError(f"{field} must be an array")
    registry = {}
    if pack["schema_version"] == 1 and "actions" in pack:
        raise PackError("Action registry requires pack schema_version 2")
    if pack["schema_version"] == 2:
        actions = pack.get("actions")
        if not isinstance(actions, list) or len(actions) > 128:
            raise PackError("Actions must be an array of at most 128 entries")
        for action in actions:
            if not isinstance(action, dict) or set(action) != {
                "id",
                "title",
                "key",
                "duration_ms",
            }:
                raise PackError("Each action requires id, title, key and duration_ms")
            action_id = action["id"]
            if (
                not isinstance(action_id, str)
                or not _ID.fullmatch(action_id)
                or action_id in registry
            ):
                raise PackError("Action IDs must be valid and unique")
            if (
                not isinstance(action["title"], str)
                or not action["title"].strip()
                or len(action["title"]) > 120
            ):
                raise PackError("Action title must be 1 to 120 characters")
            if not isinstance(action["key"], str) or not re.fullmatch(
                r"[A-Z0-9_]{1,32}", action["key"]
            ):
                raise PackError("Action key must be an uppercase key name")
            if (
                type(action["duration_ms"]) is not int
                or not 1 <= action["duration_ms"] <= 1000
            ):
                raise PackError("Action duration must be 1 to 1000 milliseconds")
            registry[action_id] = action
    skill_ids = set()
    if len(pack.get("skills", [])) > 128:
        raise PackError("A draft supports at most 128 rules")
    for skill in pack.get("skills", []):
        if (
            not isinstance(skill, dict)
            or not isinstance(skill.get("id"), str)
            or not _ID.fullmatch(skill["id"])
        ):
            raise PackError("Each skill requires an id")
        if skill["id"] in skill_ids:
            raise PackError("Skill IDs must be unique")
        skill_ids.add(skill["id"])
        when, proposal = skill.get("when"), skill.get("propose")
        if (
            not isinstance(when, dict)
            or not isinstance(when.get("state"), str)
            or not when["state"].strip()
        ):
            raise PackError(f"Skill {skill['id']} requires when.state")
        if not isinstance(proposal, dict) or proposal.get("type") not in {
            "key",
            "click",
            "wait",
            *(("action",) if pack["schema_version"] == 2 else ()),
        }:
            raise PackError(f"Skill {skill['id']} has unsupported proposal type")
        params = proposal.get("parameters", {})
        if not isinstance(params, dict):
            raise PackError(f"Skill {skill['id']} parameters must be an object")
        allowed = {
            "key": {"key"},
            "click": {"x", "y"},
            "wait": {"duration_ms"},
            "action": {"action_id"},
        }[proposal["type"]]
        if (
            set(params) != allowed
            or set(proposal) != {"type", "parameters"}
            or set(when) != {"state"}
            or set(skill) != {"id", "when", "propose"}
        ):
            raise PackError(f"Skill {skill['id']} has unsupported or missing fields")
        if proposal["type"] == "wait":
            duration = params["duration_ms"]
            if type(duration) is not int or not 0 <= duration <= 60000:
                raise PackError("Wait duration must be an integer from 0 to 60000 ms")
        if proposal["type"] == "key" and (
            not isinstance(params.get("key"), str) or not params["key"].strip()
        ):
            raise PackError(f"Skill {skill['id']} key proposal requires parameters.key")
        if proposal["type"] == "action" and (
            not isinstance(params.get("action_id"), str)
            or params["action_id"] not in registry
        ):
            raise PackError(f"Skill {skill['id']} refers to an unknown action")
        if proposal["type"] == "click":
            for coordinate in ("x", "y"):
                value = params.get(coordinate)
                if (
                    isinstance(value, bool)
                    or not isinstance(value, (int, float))
                    or not math.isfinite(value)
                    or not 0 <= value <= 1
                ):
                    raise PackError(
                        f"Skill {skill['id']} click {coordinate} must be normalized 0..1"
                    )
    try:
        raw = json.dumps(pack, allow_nan=False)
    except (ValueError, TypeError) as exc:
        raise PackError("Pack must contain finite JSON data") from exc
    if len(raw.encode("utf-8")) > 1_048_576:
        raise PackError("Pack exceeds 1 MiB")
    return pack


def new_pack(pack_id: str, title: str, game_id: str) -> dict[str, Any]:
    return validate_pack(
        {
            "schema_version": 1,
            "pack_id": pack_id,
            "title": title,
            "game_id": game_id,
            "version": "0.1.0",
            "status": "draft",
            "capabilities": [],
            "skills": [],
            "fixtures": [],
            "dependencies": [],
        }
    )


def save_pack(
    pack: Any, library: str | Path, source_directory: str | Path | None = None
) -> Path:
    """Write a new immutable version; reject overwrite and preserve old revisions."""
    data = deepcopy(validate_pack(pack))
    library = Path(library).resolve()
    destination = library / data["pack_id"] / data["version"]
    if not destination.resolve().is_relative_to(library):
        raise PackError("Pack destination escapes the library")
    destination.parent.mkdir(parents=True, exist_ok=True)
    if destination.exists():
        raise PackError(
            "This pack version already exists; increment version to save edits"
        )
    temporary = Path(tempfile.mkdtemp(prefix=".pack-", dir=destination.parent))
    try:
        if data["fixtures"]:
            if source_directory is None:
                raise PackError(
                    "Saving attached examples requires the source pack folder"
                )
            copy_fixtures(data["fixtures"], Path(source_directory), temporary)
        (temporary / "pack.json").write_text(
            json.dumps(data, indent=2, sort_keys=True) + "\n", encoding="utf-8"
        )
        temporary.replace(destination)
    except Exception:
        import shutil

        shutil.rmtree(temporary, ignore_errors=True)
        raise
    return destination


def copy_fixtures(fixtures: list, source: Path, destination: Path) -> None:
    """Preserve bounded session evidence when saving an edited pack version."""
    import hashlib
    from game_session import read_session_snapshot

    source = source.resolve()
    copied = set()
    total = 0
    if len(fixtures) > 16:
        raise PackError("At most 16 attached sessions are supported")

    def copy_file(path):
        nonlocal total
        resolved = path.resolve()
        if not resolved.is_relative_to(source) or path.is_symlink():
            raise PackError("Example file escapes the source pack")
        relative = path.relative_to(source)
        raw = read_bytes(path, 128 * 1024 * 1024)
        if relative not in copied:
            total += len(raw)
            if total > 128 * 1024 * 1024 or len(copied) >= 2048:
                raise PackError("Attached examples exceed the pack limits")
            target = destination / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(raw)
            copied.add(relative)
        return raw

    for fixture in fixtures:
        if (
            not isinstance(fixture, str)
            or not fixture
            or "\\" in fixture
            or ":" in fixture
            or fixture.startswith("/")
            or ".." in Path(fixture).parts
        ):
            raise PackError("Fixture must be a relative session path inside the pack")
        path = source / fixture
        if not path.resolve().is_relative_to(source):
            raise PackError("Fixture escapes the source pack")
        manifest, events, _ = read_session_snapshot(path)
        copy_file(path)
        copy_file(path.parent / manifest["events_file"])
        for event in events:
            image = event.payload.get("image")
            if image is None:
                continue
            if (
                not isinstance(image, str)
                or not image
                or image in {".", ".."}
                or any(c in image for c in "/\\:")
            ):
                raise PackError("Example image must be a local filename")
            raw = copy_file(path.parent / image)
            expected = event.payload.get("sha256")
            if (
                not isinstance(expected, str)
                or hashlib.sha256(raw).hexdigest() != expected.lower()
            ):
                raise PackError("Example image hash does not match")


def load_pack(path: str | Path) -> dict[str, Any]:
    candidate = Path(path)
    if candidate.is_dir():
        candidate /= "pack.json"
    try:
        return validate_pack(decode(read_bytes(candidate)))
    except (OSError, UnicodeError, ValueError) as exc:
        raise PackError(f"Cannot load pack: {exc}") from exc


def propose_for_observation(
    pack: Any, observation: dict[str, Any]
) -> list[dict[str, Any]]:
    """Return data-only proposals matching a state label; never executes them."""
    data = validate_pack(pack)
    state = observation.get("state")
    if not isinstance(state, str):
        return []
    if data["schema_version"] == 2 and (
        state == "unknown" or observation.get("stale") is True
    ):
        return []
    matches = []
    registry = {action["id"]: action for action in data.get("actions", [])}
    for skill in data.get("skills", []):
        if skill["when"]["state"] != state:
            continue
        proposal = deepcopy(skill["propose"])
        match = {"skill_id": skill["id"], "proposal": proposal}
        if proposal["type"] == "action":
            action = registry[proposal["parameters"]["action_id"]]
            match["resolved_action"] = deepcopy(action)
        matches.append(match)
    return matches
