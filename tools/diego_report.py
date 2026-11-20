"""Produce an offline Diego report without importing the desktop review UI."""

import argparse
from pathlib import Path
import tempfile

from PIL import Image

from diego_vision import Examples, demo, report
from offline_report_store import save_report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pack", type=Path)
    parser.add_argument("--frame", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--demo", action="store_true")
    args = parser.parse_args()
    if args.demo:
        with tempfile.TemporaryDirectory(prefix="diego-report-") as folder:
            pack, frames = demo(folder)
            result = report(frames[0], pack)
    else:
        if args.pack is None or args.frame is None:
            parser.error("Supply --pack and --frame, or use --demo.")
        pack = Examples(args.pack)
        with Image.open(args.frame) as image:
            result = report(image, pack)
    result["syntheticDemo"] = args.demo
    save_report(args.output, result)


if __name__ == "__main__":
    main()
