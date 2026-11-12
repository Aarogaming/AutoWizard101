"""Summarize a validated game session without executing recorded actions."""

import argparse
from collections import Counter

from game_session import SessionError, read_session
from game_packs import PackError, load_pack, propose_for_observation


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("manifest", help="Path to session.json")
    parser.add_argument("--pack", help="Optional data-only pack directory or pack.json")
    parser.add_argument(
        "--proposals", action="store_true", help="Print matched simulated proposals"
    )
    args = parser.parse_args()
    try:
        manifest, events = read_session(args.manifest)
        pack = load_pack(args.pack) if args.pack else None
        counts = Counter()
        proposals = []
        for event in events:
            counts[event.kind] += 1
            if pack and event.kind == "observation":
                proposals.extend(
                    (event.sequence, result)
                    for result in propose_for_observation(pack, event.payload)
                )
    except (SessionError, PackError) as exc:
        parser.error(str(exc))
    print(f"Session: {manifest['session_id']}")
    print("Validated recorded events:")
    for kind, count in sorted(counts.items()):
        print(f"  {kind}: {count}")
    print("Replay is read-only; no desktop input was sent.")
    if args.proposals:
        for sequence, result in proposals:
            print(
                f"SIMULATED sequence={sequence} skill={result['skill_id']} proposal={result['proposal']}"
            )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
