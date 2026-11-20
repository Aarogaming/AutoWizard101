"""Offline source-review probe. No device APIs or application dependencies added."""

import argparse
import json
from pathlib import Path
import subprocess
import sys
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("report", type=Path)
    args = parser.parse_args()
    source = args.source.resolve()
    revision = subprocess.check_output(
        ["git", "-C", str(source), "rev-parse", "HEAD"], text=True
    ).strip()
    sys.path.insert(0, str(source))
    import cv2
    import numpy as np
    from airtest.aircv.template_matching import TemplateMatching

    rng = np.random.default_rng(42)
    template = rng.integers(40, 210, (32, 40, 3), dtype=np.uint8)
    rows = []
    for name, xy, scale, brightness in [
        ("original", (40, 30), 1, 0),
        ("moved_changed_background", (210, 130), 1, 0),
        ("brightness_shift", (110, 70), 1, 20),
        ("absent", None, 1, 0),
        ("scaled_125_percent", (110, 70), 1.25, 0),
    ]:
        image = rng.integers(0, 256, (200, 320, 3), dtype=np.uint8)
        if xy:
            patch = cv2.resize(template, None, fx=scale, fy=scale)
            patch = np.clip(patch.astype(np.int16) + brightness, 0, 255).astype(
                np.uint8
            )
            x, y = xy
            image[y : y + patch.shape[0], x : x + patch.shape[1]] = patch
        start = time.perf_counter()
        result = TemplateMatching(
            template, image, threshold=0.8, rgb=False
        ).find_best_result()
        rows.append(
            {
                "case": name,
                "match": result,
                "elapsed_ms": (time.perf_counter() - start) * 1000,
            }
        )
    # Constant templates are a known normalized-correlation degeneracy worth guarding.
    flat = np.full((32, 40, 3), 128, dtype=np.uint8)
    result = TemplateMatching(flat, image, threshold=0.8, rgb=False).find_best_result()
    rows.append({"case": "flat_template_negative_control", "match": result})
    report = {
        "source_revision": revision,
        "backend": "airtest.aircv.template_matching.TemplateMatching",
        "opencv": cv2.__version__,
        "numpy": np.__version__,
        "settings": {"threshold": 0.8, "rgb": False},
        "cases": rows,
        "scope": "Synthetic fixed-scale template probe, not Wizard101 accuracy, not full Airtest or MaaFramework qualification",
        "device_connections": 0,
        "input_dispatches": 0,
    }
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
