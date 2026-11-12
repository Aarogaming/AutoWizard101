"""Strict, bounded JSON input for the offline Workshop."""

import json
import math
from pathlib import Path


def read_bytes(path, limit=1_048_576):
    with Path(path).open("rb") as stream:
        raw = stream.read(limit + 1)
    if len(raw) > limit:
        raise ValueError(f"{Path(path).name} exceeds the {limit}-byte limit")
    return raw


def _pairs(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"Duplicate JSON field: {key}")
        result[key] = value
    return result


def _constant(value):
    raise ValueError(f"Nonfinite JSON number: {value}")


def decode(raw):
    def finite(value):
        number = float(value)
        if not math.isfinite(number):
            raise ValueError("JSON number is out of range")
        return number

    return json.loads(
        raw, object_pairs_hook=_pairs, parse_constant=_constant, parse_float=finite
    )
