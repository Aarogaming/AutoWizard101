"""Generate portable synthetic report fixtures with unchanged observer payloads."""

import argparse
import copy
import hashlib
import json
from pathlib import Path
import tempfile

from PIL import Image

from diego_vision import Examples, SceneTracker, demo, report

ROOT = Path(__file__).resolve().parents[1]


def write_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(data, indent=2, allow_nan=False) + "\n", encoding="utf-8"
    )


def build(destination):
    destination = Path(destination)
    if destination.exists() and any(destination.iterdir()):
        raise ValueError("Fixture destination must be empty.")
    destination.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="diego-fixtures-") as folder:
        pack, frames = demo(folder)
        stable_entries = []
        for entry in pack.entries:
            filename = entry["label"].lower() + ".png"
            target = destination / "pack" / filename
            target.parent.mkdir(parents=True, exist_ok=True)
            with Image.open(pack.folder / entry["file"]) as image:
                image.save(target)
            stable_entries.append({"label": entry["label"], "file": filename})
        write_json(
            destination / "pack/examples.json",
            {
                "version": 1,
                "size": pack.size,
                "roi": pack.roi,
                "examples": stable_entries,
            },
        )
    pack = Examples(destination / "pack")
    tracker = SceneTracker()
    cases = []
    for index, image in enumerate(frames):
        frame_name = f"frames/frame-{index:04}.png"
        report_name = f"valid/frame-{index:04}.json"
        (destination / "frames").mkdir(exist_ok=True)
        image.save(destination / frame_name)
        result = report(image, pack, tracker=tracker, frame_id=index)
        result["syntheticDemo"] = True
        write_json(destination / report_name, result)
        cases.append(
            {
                "path": report_name,
                "expect": "accept",
                "sourceFrame": frame_name,
                "case": "synthetic consecutive-frame observation",
            }
        )
    base = json.loads((destination / "valid/frame-0000.json").read_text())
    single = report(frames[0], pack)
    single["syntheticDemo"] = True
    write_json(destination / "valid/single-frame.json", single)
    cases.append(
        {
            "path": "valid/single-frame.json",
            "expect": "accept",
            "sourceFrame": "frames/frame-0000.png",
            "case": "headless CLI shape; no temporal confirmation",
        }
    )
    blank = Image.new("RGB", frames[0].size, "black")
    blank.save(destination / "frames/negative.png")
    unknown = report(blank, pack)
    unknown["syntheticDemo"] = True
    write_json(destination / "valid/unknown.json", unknown)
    cases.append(
        {
            "path": "valid/unknown.json",
            "expect": "accept",
            "sourceFrame": "frames/negative.png",
            "case": "no detections; explicit unknown, not safe-to-act",
        }
    )
    extra = copy.deepcopy(base)
    extra["futureReadOnlyMetadata"] = {
        "note": "Preserve without interpreting as authority."
    }
    write_json(destination / "valid/additional-field.json", extra)
    cases.append(
        {
            "path": "valid/additional-field.json",
            "expect": "accept",
            "case": "retain additional fields",
        }
    )
    mutations = {
        "input-enabled": lambda data: data.update(inputSent=True),
        "wrong-mode": lambda data: data.update(mode="live-control"),
        "missing-field": lambda data: data.pop("limitations"),
        "bad-dimensions": lambda data: data.update(sourceSize=[True, 420]),
        "outside-roi": lambda data: data["detections"][0].update(box=[0, 0, 20, 20]),
        "bad-similarity": lambda data: data["detections"][0].update(similarity=1.5),
        "bad-track": lambda data: data["tracks"][0].update(consecutiveFrames=0),
    }
    for name, mutate in mutations.items():
        data = copy.deepcopy(base)
        mutate(data)
        path = f"invalid/{name}.json"
        write_json(destination / path, data)
        cases.append({"path": path, "expect": "reject", "case": name})
    for name, text in {
        "malformed": '{"mode":',
        "duplicate-key": '{"mode":"offline-observer","mode":"live-control"}',
        "non-finite": (destination / "valid/single-frame.json")
        .read_text()
        .replace('"similarity": 1.0', '"similarity": NaN', 1),
    }.items():
        path = f"invalid/{name}.json"
        (destination / path).write_text(text, encoding="utf-8")
        cases.append({"path": path, "expect": "reject", "case": name})
    files = {}
    for path in sorted(destination.rglob("*")):
        if path.is_file():
            raw = path.read_bytes()
            files[path.relative_to(destination).as_posix()] = {
                "sha256": hashlib.sha256(raw).hexdigest(),
                "bytes": len(raw),
            }
    write_json(
        destination / "manifest.json",
        {
            "fixtureSetVersion": 1,
            "kind": "autowizard101-offline-observer-fixtures",
            "syntheticOnly": True,
            "payloadVersionField": None,
            "authority": "AutoWizard101 producer evidence; Aaroneous contract adoption pending",
            "cases": cases,
            "files": files,
        },
    )
    return destination


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    print(build(parser.parse_args().output))
