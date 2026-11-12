"""Local offline fixture checks, not an adopted Aaroneous admission contract."""

import argparse
import json
import math
from pathlib import Path

MAX_BYTES = 256 * 1024


def _fail(message):
    raise ValueError(message)


def _pairs(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            _fail("Duplicate JSON key: " + key)
        result[key] = value
    return result


def _integer(value, minimum=0):
    return type(value) is int and value >= minimum


def _finite(value, minimum, maximum):
    return (
        type(value) in (int, float)
        and math.isfinite(value)
        and minimum <= value <= maximum
    )


def _box(value, bounds):
    if (
        not isinstance(value, list)
        or len(value) != 4
        or not all(_integer(v) for v in value)
    ):
        _fail("Invalid integer rectangle.")
    x, y, width, height = value
    bx, by, bw, bh = bounds
    if (
        width <= 0
        or height <= 0
        or x < bx
        or y < by
        or x + width > bx + bw
        or y + height > by + bh
    ):
        _fail("Rectangle is empty or outside its coordinate bounds.")


def validate_report(data):
    if not isinstance(data, dict):
        _fail("Report must be an object.")
    required = {
        "mode",
        "inputSent",
        "sourceSize",
        "playArea",
        "exampleCount",
        "similarityThreshold",
        "detections",
        "tracks",
        "suggestions",
        "spawnSequenceAssumed",
        "limitations",
    }
    if required - data.keys():
        _fail("Missing report fields: " + ", ".join(sorted(required - data.keys())))
    if (
        data["mode"] != "offline-observer"
        or data["inputSent"] is not False
        or data["spawnSequenceAssumed"] is not False
    ):
        _fail(
            "Only offline reports without input or assumed spawn sequence are accepted by this fixture checker."
        )
    size = data["sourceSize"]
    if (
        not isinstance(size, list)
        or len(size) != 2
        or not all(_integer(v, 1) and v <= 16384 for v in size)
    ):
        _fail("Invalid source dimensions.")
    _box(data["playArea"], [0, 0, *size])
    if not _integer(data["exampleCount"]) or not _finite(
        data["similarityThreshold"], 0.8, 0.999
    ):
        _fail("Invalid example count or matching threshold.")
    for field in ("detections", "tracks"):
        if not isinstance(data[field], list) or len(data[field]) > 1024:
            _fail("Invalid or excessive " + field)
        for item in data[field]:
            if (
                not isinstance(item, dict)
                or not isinstance(item.get("label"), str)
                or not 1 <= len(item["label"]) <= 256
            ):
                _fail("Object label must be a nonempty bounded string.")
            _box(item.get("box"), data["playArea"])
            if field == "detections" and not _finite(item.get("similarity"), 0, 1):
                _fail("Similarity must be finite and between zero and one.")
            if field == "tracks" and (
                not _integer(item.get("consecutiveFrames"), 1)
                or item.get("relativeMotion")
                not in ("unknown", "approaching", "receding", "steady")
            ):
                _fail("Invalid track evidence.")
    if (
        not isinstance(data["suggestions"], list)
        or len(data["suggestions"]) > 64
        or any(not isinstance(v, str) or len(v) > 4096 for v in data["suggestions"])
    ):
        _fail("Invalid suggestion strings.")
    if (
        not isinstance(data["limitations"], str)
        or not 1 <= len(data["limitations"]) <= 8192
    ):
        _fail("Explicit report limitations are required.")
    if "syntheticDemo" in data and type(data["syntheticDemo"]) is not bool:
        _fail("syntheticDemo must be a boolean when present.")
    # Additional fields are retained unchanged. This does not grant them execution meaning.
    return data


def load_report(path):
    with Path(path).open("rb") as stream:
        raw = stream.read(MAX_BYTES + 1)
    if len(raw) > MAX_BYTES:
        _fail("Report exceeds the local 256 KiB fixture limit.")
    data = json.loads(
        raw.decode("utf-8"),
        object_pairs_hook=_pairs,
        parse_constant=lambda value: _fail("Non-finite JSON constant: " + value),
    )
    return validate_report(data)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("report", type=Path)
    args = parser.parse_args()
    data = load_report(args.report)
    print(
        json.dumps(
            {
                "validOfflineReport": True,
                "mode": data["mode"],
                "inputSent": data["inputSent"],
            }
        )
    )


if __name__ == "__main__":
    main()
