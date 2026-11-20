"""Compare saved GDI+ panels with frozen Pet Dance references, without gameplay.

Run diagnose_pet_dance_gdi.ps1 first. This reproduces the native *per-frame*
sampling/threshold math, not its temporal outcome/sequence gates. PIL only reads
PNGs/crops; geometry resampling in the supplied panel manifest is actual GDI+.
"""

import argparse
import hashlib
import json
import os
from collections import Counter
from pathlib import Path

os.environ["OPENBLAS_NUM_THREADS"] = "1"
os.environ["OMP_NUM_THREADS"] = "1"
os.environ["MKL_NUM_THREADS"] = "1"

import numpy as np
from PIL import Image


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def rgb(path):
    with Image.open(path) as image:
        return np.array(image.convert("RGB"), dtype=np.float64)


class Classifier:
    def __init__(self, profile_path):
        self.profile = json.loads(profile_path.read_text(encoding="utf-8-sig"))
        self.references = {}
        for reference in self.profile["References"]:
            path = profile_path.parent / reference["File"]
            if digest(path) != reference["Sha256"].upper():
                raise ValueError("Reference hash mismatch")
            self.references[reference["Label"]] = rgb(path)
        _, _, width, height = self.profile["Cue"]
        xx, yy = np.meshgrid(np.arange(0, width, 2), np.arange(0, height, 2))
        cx, cy, radius = self.profile["Mask"]
        self.mask = (xx - cx) ** 2 + (yy - cy) ** 2 < radius**2
        self.neutral = self.cue_pixels(self.references["Neutral"])
        self.arrows = {
            label: self.cue_pixels(self.references[label]) - self.neutral
            for label in ("Up", "Down", "Left", "Right")
        }

    def region(self, panel, name, stride=2):
        x, y, width, height = self.profile[name]
        return panel[y : y + height : stride, x : x + width : stride]

    def cue_pixels(self, panel):
        return self.region(panel, "Cue")[self.mask].reshape(-1)

    def score(self, panel):
        delta = self.cue_pixels(panel) - self.neutral
        blank = float(np.sqrt(np.mean(delta**2)))
        fits = []
        for label, model in self.arrows.items():
            opacity = float(np.clip(np.sum(delta * model) / np.sum(model**2), 0, 1.5))
            error = float(np.sqrt(np.mean((delta - opacity * model) ** 2)))
            fits.append(dict(label=label, opacity=opacity, error=error))
        fits.sort(key=lambda row: row["error"])
        best = fits[0]
        margin = fits[1]["error"] - best["error"]
        cue, fade_of = "Unknown", "Unknown"
        if blank <= 12 and best["opacity"] < 0.15:
            cue = "Neutral"
        elif 0.7 <= best["opacity"] <= 1.5 and best["error"] <= 16 and margin >= 8:
            cue = best["label"]
        elif best["opacity"] < 0.7 and best["error"] <= 12 and margin >= 1.5:
            cue, fade_of = "Fading", best["label"]
        phase_scores = sorted(
            (
                float(
                    np.sqrt(
                        np.mean(
                            (
                                self.region(panel, "Phase")
                                - self.region(self.references[label], "Phase")
                            )
                            ** 2
                        )
                    )
                ),
                label,
            )
            for label in ("Watch", "Go", "Resolving")
        )
        phase_margin = phase_scores[1][0] - phase_scores[0][0]
        phase = (
            phase_scores[0][1]
            if phase_scores[0][0] <= 12 and phase_margin >= 8
            else "Unknown"
        )
        loading_rms = None
        if "Loading" in self.references:
            loading_rms = float(
                np.sqrt(
                    np.mean(
                        (panel[::2, ::2] - self.references["Loading"][::2, ::2]) ** 2
                    )
                )
            )
            if phase == "Unknown" and loading_rms <= 8:
                phase, cue, fade_of = "Loading", "Unknown", "Unknown"
        return dict(
            phase=phase,
            cue=cue,
            fade_of=fade_of,
            blank=blank,
            best_direction=best["label"],
            opacity=best["opacity"],
            cue_error=best["error"],
            cue_margin=margin,
            phase_error=phase_scores[0][0],
            phase_margin=phase_margin,
            phase_scores={label: error for error, label in phase_scores},
            loading_rms=loading_rms,
        )

    def crop(self, frame, dx=0, dy=0):
        pieces = []
        for name in ("Top", "Bottom"):
            x, y, width, height = self.profile[name]
            pieces.append(frame[y + dy : y + dy + height, x + dx : x + dx + width])
        return np.concatenate(pieces)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("manifest", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise ValueError("Choose a new report path")
    manifest = json.loads(args.manifest.read_text(encoding="utf-8-sig"))
    profile_path = Path(manifest["profile"])
    if digest(profile_path) != manifest["profile_sha256"]:
        raise ValueError("Profile changed after panel extraction")
    classifier = Classifier(profile_path)
    expected = {
        "watch-neutral": ("Watch", "Neutral"),
        "right": ("Watch", "Right"),
        "left": ("Watch", "Left"),
        "up": ("Watch", "Up"),
        "down": ("Watch", "Down"),
        "success-feedback": ("Resolving", "Neutral"),
        "failure-feedback": ("Resolving", "Neutral"),
        "go-reference": ("Go", "Neutral"),
    }
    rows, offsets = [], []
    for panel in manifest["panels"]:
        path = args.manifest.parent / panel["panel"]
        if digest(path) != panel["panel_sha256"]:
            raise ValueError("Panel changed")
        result = classifier.score(rgb(path))
        row = dict(panel, **result)
        row["expected_phase"], row["expected_cue"] = expected[panel["name"]]
        row["matches_expected"] = (result["phase"], result["cue"]) == expected[
            panel["name"]
        ]
        rows.append(row)
        if panel["variant"] == "original" and panel["kind"] == "saved-full-frame":
            source = Path(panel["source"])
            if digest(source) != panel["source_sha256"]:
                raise ValueError("Source frame changed")
            frame = rgb(source)
            row["gdi_vs_direct_crop_rms"] = float(
                np.sqrt(np.mean((rgb(path) - classifier.crop(frame)) ** 2))
            )
            for delta in (-4, -2, -1, 1, 2, 4):
                for dx, dy in ((delta, 0), (0, delta)):
                    offsets.append(
                        dict(
                            name=panel["name"],
                            dx=dx,
                            dy=dy,
                            **classifier.score(classifier.crop(frame, dx, dy)),
                        )
                    )
    variants = sorted({row["variant"] for row in rows})
    summary = {
        variant: {
            "samples": len(
                selected := [row for row in rows if row["variant"] == variant]
            ),
            "expected_phase_and_cue_matches": sum(
                row["matches_expected"] for row in selected
            ),
            "phase_counts": dict(Counter(row["phase"] for row in selected)),
            "cue_counts": dict(Counter(row["cue"] for row in selected)),
            "max_phase_error": max(row["phase_error"] for row in selected),
            "max_cue_error": max(row["cue_error"] for row in selected),
        }
        for variant in variants
    }
    report = dict(
        schema_version=1,
        kind="offline-calibration-geometry-diagnostic",
        profile_sha256=manifest["profile_sha256"],
        manifest_sha256=digest(args.manifest),
        source_video_sha256=manifest["source_video_sha256"],
        geometry_engine=manifest["drawing_assembly"],
        classifier="Python reproduction of C# per-frame sampled cue/phase math; temporal outcome gate and sequence behavior not evaluated",
        limits="Seven actual saved human frames and one Go panel reconstructed into a full frame; geometry variants and offsets synthetic. Not held-out accuracy, current gameplay evidence, or established root cause.",
        input_count=manifest["input_count"],
        summary=summary,
        panels=rows,
        roi_offset_sensitivity=offsets,
        inputs_sent=0,
        live_qualified=False,
    )
    args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()
