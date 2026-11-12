"""Create a small immutable set of data-only Dodge policy candidates."""

from __future__ import annotations

import argparse
from copy import deepcopy
from itertools import product
from pathlib import Path

from game_packs import load_pack, save_pack

POLICY_PREFIXES = {
    "center-configurable-v20": "0.3.0",
    "directional-v26": "0.4.0",
    "directional-guard-v27": "0.5.0",
    "directional-carry-v28": "0.6.0",
    "directional-smooth-v29": "0.7.0",
    "directional-configurable-v30": "0.8.0",
    "directional-two-phase-v31": "0.9.0",
    "directional-pulse-v32": "0.10.0",
}


def generate(
    source,
    library,
    horizons,
    radii,
    policy="center-configurable-v20",
    risk_weights=None,
    edge_weights=None,
    margins=None,
):
    risk_weights = [100] if risk_weights is None else risk_weights
    edge_weights = [100] if edge_weights is None else edge_weights
    margins = [None] if margins is None else margins
    if (
        not horizons
        or not radii
        or not risk_weights
        or not edge_weights
        or not margins
        or len(horizons)
        * len(radii)
        * len(risk_weights)
        * len(edge_weights)
        * len(margins)
        > 16
        or len(set(horizons)) != len(horizons)
        or len(set(radii)) != len(radii)
        or len(set(risk_weights)) != len(risk_weights)
        or len(set(edge_weights)) != len(edge_weights)
        or len(set(margins)) != len(margins)
        or any(type(h) is not int or not 100 <= h <= 2000 for h in horizons)
        or any(type(r) is not int or not 1 <= r <= 100 for r in radii)
        or any(
            type(value) is not int or not 25 <= value <= 400 for value in risk_weights
        )
        or any(
            type(value) is not int or not 0 <= value <= 300 for value in edge_weights
        )
        or any(
            value is not None and (type(value) is not int or not 1 <= value <= 100)
            for value in margins
        )
        or (None in margins and margins != [None])
        or policy not in POLICY_PREFIXES
        or (
            policy != "directional-configurable-v30"
            and (risk_weights != [100] or edge_weights != [100])
        )
    ):
        raise ValueError(
            "Candidates must use unique, bounded horizons and radii (maximum 16)."
        )
    pack = load_pack(source)
    if pack.get("game_id") != "godot-dodge-the-creeps" or "context" not in pack:
        raise ValueError("Choose a context-aware Dodge source pack.")
    source_directory = Path(source) if Path(source).is_dir() else Path(source).parent
    prefix = POLICY_PREFIXES[policy]
    combinations = list(product(horizons, radii, risk_weights, edge_weights, margins))

    def version(horizon, radius, risk, edge, margin):
        base = f"{prefix}-h{horizon}-r{radius}"
        if policy == "directional-configurable-v30":
            base += f"-q{risk}-e{edge}"
        return f"{base}-m{margin}" if margin is not None else base

    destinations = [
        Path(library) / pack["pack_id"] / version(*values) for values in combinations
    ]
    if any(path.exists() for path in destinations):
        raise ValueError(
            "A candidate version already exists; no versions were written."
        )
    candidates = []
    for horizon, radius, risk, edge, margin in combinations:
        candidate = deepcopy(pack)
        candidate["version"] = version(horizon, radius, risk, edge, margin)
        candidate["title"] = f"Dodge {policy}: {horizon} ms horizon, {radius} px radius"
        candidate["context"]["avoidance_policy"] = policy
        candidate["context"]["constraints"]["prediction_horizon_ms"] = horizon
        candidate["context"]["constraints"]["threat_radius_px"] = radius
        if margin is not None:
            candidate["context"]["constraints"]["edge_margin_px"] = margin
        if policy == "directional-configurable-v30":
            candidate["context"]["constraints"]["risk_weight_percent"] = risk
            candidate["context"]["constraints"]["edge_weight_percent"] = edge
        else:
            candidate["context"]["constraints"].pop("risk_weight_percent", None)
            candidate["context"]["constraints"].pop("edge_weight_percent", None)
        candidates.append(
            save_pack(candidate, library, source_directory=source_directory)
        )
    return candidates


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("library", type=Path)
    parser.add_argument("--horizons", type=int, nargs="+", required=True)
    parser.add_argument("--radii", type=int, nargs="+", required=True)
    parser.add_argument("--risk-weights", type=int, nargs="+", default=[100])
    parser.add_argument("--edge-weights", type=int, nargs="+", default=[100])
    parser.add_argument("--margins", type=int, nargs="+")
    parser.add_argument(
        "--policy",
        choices=tuple(POLICY_PREFIXES),
        default="center-configurable-v20",
    )
    args = parser.parse_args()
    for candidate in generate(
        args.source,
        args.library,
        args.horizons,
        args.radii,
        args.policy,
        args.risk_weights,
        args.edge_weights,
        args.margins,
    ):
        print(candidate)


if __name__ == "__main__":
    main()
