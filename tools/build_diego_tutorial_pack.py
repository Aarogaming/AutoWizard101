"""Build a local reference example pack from reviewed tutorial screenshots, not game input."""

import argparse
import json
from pathlib import Path

from PIL import Image

from diego_vision import Examples, report

ROOT = Path(__file__).resolve().parents[1]


def build(frames_folder, destination):
    frames_folder, destination = Path(frames_folder), Path(destination)
    if destination.exists() and any(destination.iterdir()):
        raise ValueError(
            "Choose an empty destination; existing example packs are never overwritten."
        )
    annotation = json.loads(
        (ROOT / "tools/diego_tutorial_annotations.json").read_text(encoding="utf-8")
    )
    images = {}
    for name in {
        entry["frame"] for entry in annotation["examples"] + annotation["heldOut"]
    }:
        with Image.open(frames_folder / name) as source:
            images[name] = source.convert("RGB")
        if images[name].size != tuple(annotation["sourceSize"]):
            raise ValueError(f"{name}: expected the reviewed 648×400 game-area crop.")
    pack = Examples(destination)
    pack.configure(tuple(annotation["sourceSize"]), annotation["playArea"])
    for entry in annotation["examples"]:
        pack.add(images[entry["frame"]], entry["box"], entry["label"])
    validation = []
    for entry in annotation["heldOut"]:
        result = report(images[entry["frame"]], pack)
        actual = {item["label"] for item in result["detections"]}
        validation.append(
            {
                "frame": entry["frame"],
                "expectedLabels": entry["expectedLabels"],
                "detectedLabels": sorted(actual),
                "missingLabels": sorted(set(entry["expectedLabels"]) - actual),
                "report": result,
            }
        )
    metadata = {
        "source": annotation["source"],
        "credit": annotation["credit"],
        "exampleCount": len(pack.entries),
        "trainingFrames": sorted({entry["frame"] for entry in annotation["examples"]}),
        "heldOut": validation,
        "liveClientValidated": False,
        "neuralNetworkTrained": False,
        "limitations": "Small historical reference pack. Held-out results are not a general accuracy estimate. Scale/layout and additional poses need current-client validation.",
    }
    (destination / "provenance.json").write_text(
        json.dumps(metadata, indent=2), encoding="utf-8"
    )
    return metadata


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--frames", type=Path, default=ROOT / ".local/diego-observer/tutorial"
    )
    parser.add_argument(
        "--output", type=Path, default=ROOT / ".local/diego-observer/tutorial-pack"
    )
    args = parser.parse_args()
    result = build(args.frames, args.output)
    print(
        json.dumps(
            {
                "exampleCount": result["exampleCount"],
                "heldOut": [
                    {key: value for key, value in entry.items() if key != "report"}
                    for entry in result["heldOut"]
                ],
            },
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
