"""Offline destination-name planning over extracted W101 topology.

Produces candidate zone legs for local NavigationAtlas qualification, never
movement commands. Coordinates from the macro graph are deliberately ignored.
"""

import argparse
from collections import deque
import hashlib
import json
from pathlib import Path

ALIASES = {
    "commons": "WizardCity/WC_Hub",
    "pet pavilion": "WizardCity/WC_Streets/Interiors/WC_PET_Park",
    "pet dance": "WizardCity/WC_Streets/Interiors/WC_PET_Park",
}


def plan(graph, start, destination, bindings=(), connector_request=None):
    nodes = graph["nodes"]
    if len(nodes) > 65536 or len(graph["edges"]) > 1000000:
        raise ValueError("graph budget")
    by_id = {n["id"]: n["zone"] for n in nodes}
    if len(by_id) != len(nodes):
        raise ValueError("duplicate node ID")

    def resolve(value):
        value = ALIASES.get(value.strip().casefold(), value.strip())
        matches = [
            i for i, name in by_id.items() if name.casefold() == value.casefold()
        ]
        if len(matches) != 1:
            raise ValueError("unknown or ambiguous destination")
        return matches[0]

    origin, goal = resolve(start), resolve(destination)
    adjacency = {i: [] for i in by_id}
    for a, b in graph["edges"]:
        if a not in by_id or b not in by_id:
            raise ValueError("unknown edge endpoint")
        adjacency[a].append(b)
    previous = {origin: None}
    queue = deque([origin])
    while queue and goal not in previous:
        current = queue.popleft()
        for nxt in sorted(adjacency[current]):
            if nxt not in previous:
                previous[nxt] = current
                queue.append(nxt)
    if goal not in previous:
        return dict(found=False, reason="no-directed-zone-path", runtimeEnabled=False)
    chain = []
    current = goal
    while current is not None:
        chain.append(current)
        current = previous[current]
    chain.reverse()
    legs = []
    for a, b in zip(chain, chain[1:]):
        matches = [
            x for x in bindings if x["fromZone"] == by_id[a] and x["toZone"] == by_id[b]
        ]
        legs.append(
            dict(
                fromZone=by_id[a],
                toZone=by_id[b],
                anchor=matches[0] if len(matches) == 1 else None,
                qualification="candidate-requires-local-atlas",
                anchorStatus=(
                    "candidate" if len(matches) == 1 else "missing-or-ambiguous"
                ),
            )
        )
    result = dict(
        found=True,
        runtimeEnabled=False,
        destination=destination,
        destinationZone=by_id[goal],
        zoneSequence=[by_id[i] for i in chain],
        legs=legs,
        finalInteractionVerified=False,
        topologySha256=graph.get("memberSha256"),
    )
    if connector_request is not None:
        from w101_connector import select

        request = dict(connector_request)
        if request.pop("zone") != by_id[origin]:
            raise ValueError("connector zone mismatch")
        result["entryConnector"] = select(**request)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("graph", type=Path)
    parser.add_argument("start")
    parser.add_argument("destination")
    parser.add_argument("output", type=Path)
    parser.add_argument("--commons-binding", type=Path)
    parser.add_argument("--pavilion-binding", type=Path)
    args = parser.parse_args()

    def load(path):
        if path.stat().st_size > 8 * 1024 * 1024:
            raise ValueError("input budget")
        data = path.read_bytes()
        return json.loads(data), hashlib.sha256(data).hexdigest()

    graph, _ = load(args.graph)
    bindings = []
    if args.commons_binding and args.pavilion_binding:
        commons, ch = load(args.commons_binding)
        pavilion, ph = load(args.pavilion_binding)
        bindings.append(
            dict(
                fromZone=ALIASES["commons"],
                toZone=ALIASES["pet pavilion"],
                approach=commons["Goal"],
                arrival=pavilion["Start"],
                evidenceSha256=[ch, ph],
                meaning="saved approach/post-load candidates, not exact trigger boundary",
                reverseQualified=False,
            )
        )
    result = plan(graph, args.start, args.destination, bindings)
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result))


if __name__ == "__main__":
    main()
