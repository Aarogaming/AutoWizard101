"""Create a starter data-only game pack in a local library."""

import argparse

from game_packs import PackError, new_pack, save_pack


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("pack_id")
    parser.add_argument("title")
    parser.add_argument("game_id")
    parser.add_argument("--library", default=".local/game-packs")
    args = parser.parse_args()
    try:
        path = save_pack(new_pack(args.pack_id, args.title, args.game_id), args.library)
    except PackError as exc:
        parser.error(str(exc))
    print(f"Draft saved: {path}")
    print("Pack contains data only; it has no input or executable skills yet.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
