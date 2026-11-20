"""Recognize bounded saved Conjuration images and rehearse memory decisions offline."""

import argparse
from dataclasses import asdict
import hashlib
import json
from pathlib import Path

from conjuration_observation_fixtures import (
    _group,
    _image_source,
    _name,
    _number,
    _object,
    replay_fixture,
)
from conjuration_vision import ConjurationVision, VisionProfile, decode_image
from game_data import decode, read_bytes


def replay_images(path):
    path = Path(path)
    raw = read_bytes(path)
    manifest = decode(raw)
    _object(
        manifest,
        (
            "schema_version",
            "session_id",
            "synthetic",
            "board_id",
            "group_size",
            "profile",
            "references",
            "events",
        ),
    )
    if (
        type(manifest["schema_version"]) is not int
        or manifest["schema_version"] != 1
        or type(manifest["synthetic"]) is not bool
    ):
        raise ValueError("Unsupported image replay manifest")
    name = _name(manifest["session_id"], "session_id")
    board_id = _name(manifest["board_id"], "board_id")
    group_size = _group(manifest["group_size"])
    profile = VisionProfile.from_dict(manifest["profile"])
    if (profile.qualification == "synthetic") != manifest["synthetic"]:
        raise ValueError(
            "Reference qualification must match the declared replay provenance"
        )
    references = manifest["references"]
    if not isinstance(references, dict) or set(references) != {
        r.key for r in profile.references
    }:
        raise ValueError("Supply exactly the frozen profile's named reference images")
    events = manifest["events"]
    if not isinstance(events, list) or not 1 <= len(events) <= 256:
        raise ValueError("A focused image rehearsal needs 1 to 256 events")
    images, image_payloads, gallery, reference_pixels = {}, {}, {}, 0

    def checked_source(item):
        _object(item, ("image", "sha256"))
        source = {
            "annotation_origin": "synthetic" if manifest["synthetic"] else "recognizer",
            **item,
        }
        if source["image"] is None:
            raise ValueError("Image replay requires actual saved pixels")
        _image_source(
            source, manifest["synthetic"], path.parent, images, image_payloads
        )
        return source

    try:
        for key, item in references.items():
            source = checked_source(item)
            image = decode_image(image_payloads[source["image"]])
            gallery[key] = image
            reference_pixels += image.width * image.height
            if image.width * image.height > 262144 or reference_pixels > 1048576:
                raise ValueError(
                    "Frozen reference images exceed the decoded pixel budget"
                )
        vision = ConjurationVision(profile, gallery)
    finally:
        for image in gallery.values():
            image.close()
    translated, perception = [], []
    last_time = -1
    for event in events:
        if not isinstance(event, dict):
            raise ValueError("Image event must be an object")
        time_ms = _number(event.get("time_ms"))
        if time_ms < last_time:
            raise ValueError("Image event times must be nondecreasing")
        last_time = time_ms
        kind = event.get("kind")
        if kind == "frame":
            _object(event, ("kind", "time_ms", "revision", "phase", "image", "sha256"))
            if len(perception) >= 128:
                raise ValueError("A focused replay supports at most 128 frames")
            source = checked_source(
                {"image": event["image"], "sha256": event["sha256"]}
            )
            with decode_image(image_payloads[event["image"]]) as image:
                observed = vision.observe(
                    image,
                    board_id=board_id,
                    revision=event["revision"],
                    time_ms=time_ms,
                    phase=event["phase"],
                )
            payload = asdict(observed)
            # The public fixture is JSON-shaped; dataclass tuple members become arrays.
            board = json.loads(json.dumps(payload["board"], allow_nan=False))
            translated.append(
                {
                    "kind": "observation",
                    "time_ms": time_ms,
                    "board": board,
                    "source": source,
                }
            )
            perception.append(payload)
        elif kind == "action_receipt":
            _object(event, ("kind", "time_ms", "proposal_id", "status"))
            translated.append(event)
        elif kind == "reset":
            _object(event, ("kind", "time_ms", "board_id", "group_size"))
            board_id = _name(event["board_id"], "board_id")
            _group(event["group_size"])
            translated.append(event)
        else:
            raise ValueError("Unsupported image rehearsal event")
    derived_fixture = {
        "schema_version": 1,
        "fixture_id": name,
        "clock_origin": "supplied-image-events-ms",
        "synthetic": manifest["synthetic"],
        "board_id": manifest["board_id"],
        "group_size": group_size,
        "events": translated,
    }
    decision_report = replay_fixture(
        derived_fixture, path.parent, image_payloads=image_payloads
    )
    return {
        "schema_version": 1,
        "session_id": name,
        "evidence_mode": (
            "synthetic-image-replay"
            if manifest["synthetic"]
            else "recorded-candidate-image-replay"
        ),
        "sha256_manifest": hashlib.sha256(raw).hexdigest(),
        "profile": manifest["profile"],
        "source_image_hashes": {name: digest for name, (digest, _) in images.items()},
        "desktop_dispatch_count": 0,
        "live_qualified": False,
        "phase_detection_implemented": False,
        "recognition_frame_count": len(perception),
        "perception": perception,
        "decision_report": decision_report,
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("manifest", type=Path)
    parser.add_argument("report", type=Path)
    args = parser.parse_args(argv)
    try:
        report = replay_images(args.manifest)
        text = json.dumps(report, indent=2, allow_nan=False) + "\n"
        if len(text.encode("utf-8")) > 8 * 1024 * 1024:
            raise ValueError("Image rehearsal report exceeds 8 MiB")
        with args.report.open("x", encoding="utf-8") as output:
            output.write(text)
    except (OSError, ValueError, TypeError, KeyError, ImportError) as error:
        print("Image rehearsal failed: " + str(error))
        return 1
    print(
        f"Recognized {report['recognition_frame_count']} saved frames; no desktop inputs sent."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
