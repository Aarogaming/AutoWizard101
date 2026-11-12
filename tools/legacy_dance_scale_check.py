"""Offline scale calibration experiment using the supplied Pet Dance images.

Searches only a small supplied cue region, never the desktop. A broad scale
search is calibration work; a future live adapter should reuse the calibrated
scale and invalidate it when window/UI geometry changes.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import time

from rehearse_w101_dance_teacher import imaging, environment, Arrow, PINS, locate


class ScaleMatcher:
    def __init__(self, images: dict, scales=None):
        self.scales = (
            tuple(scales)
            if scales is not None
            else tuple(0.6 + i * 0.05 for i in range(21))
        )
        if not 1 <= len(self.scales) <= 41 or any(
            not 0.5 <= s <= 2 for s in self.scales
        ):
            raise ValueError("Use 1–41 scales in the range 0.5–2")
        cv2, np, _ = imaging()
        self.templates = []
        for label, image in images.items():
            rgba = np.array(image.convert("RGBA"))
            if min(rgba.shape[:2]) < 8 or max(rgba.shape[:2]) > 256:
                raise ValueError("Use small arrow templates")
            for scale in self.scales:
                width, height = round(image.width * scale), round(image.height * scale)
                resized = cv2.resize(
                    rgba,
                    (width, height),
                    interpolation=cv2.INTER_AREA if scale < 1 else cv2.INTER_LINEAR,
                )
                mask = np.where(resized[:, :, 3] >= 240, 255, 0).astype(np.uint8)
                if np.count_nonzero(mask) < 20:
                    continue
                self.templates.append((label, scale, resized[:, :, :3].copy(), mask))

    def predict(self, region, minimum=0.96, margin=0.025):
        if not 0 <= minimum <= 1 or not 0 < margin <= 1:
            raise ValueError("Use finite score and ambiguity thresholds in 0–1")
        cv2, np, _ = imaging()
        if max(region.size) > 256 or min(region.size) < 20:
            raise ValueError("Calibrate a cue region of 20–256 pixels per dimension")
        pixels = np.array(region.convert("RGB"))
        scores = {}
        for label, scale, template, mask in self.templates:
            height, width = template.shape[:2]
            if width > region.width or height > region.height:
                continue
            matches = cv2.matchTemplate(
                pixels, template, cv2.TM_CCORR_NORMED, mask=mask
            )
            matches = np.nan_to_num(matches, nan=-1, posinf=-1, neginf=-1)
            _, score, _, position = cv2.minMaxLoc(matches)
            candidate = {
                "label": label,
                "score": float(score),
                "scale": scale,
                "region": [*position, width, height],
            }
            if label not in scores or score > scores[label]["score"]:
                scores[label] = candidate
        ordered = sorted(scores.values(), key=lambda row: row["score"], reverse=True)
        if len(ordered) < 2:
            return {"label": None, "reason": "insufficient-alternatives"}
        best = dict(ordered[0])
        best["margin"] = best["score"] - ordered[1]["score"]
        if best["score"] < minimum or best["margin"] < margin:
            best["label"] = None
            best["reason"] = "uncertain"
        else:
            best["reason"] = "matched"
        return best


def benchmark(source: Path):
    cv2, _, Image = imaging()
    cv2.setNumThreads(1)
    started = time.perf_counter()
    trials = []
    negatives = []
    for resolution in ("800x600", "1280x800"):
        legacy, _, _ = environment(source, resolution)
        icons = {
            arrow.name: legacy["arrow_icons"][arrow.value].convert("RGBA")
            for arrow in Arrow
        }
        matcher = ScaleMatcher(icons)
        # Scales deliberately include values between calibration grid entries.
        for scale in (0.72, 0.91, 1.0, 1.13, 1.37, 1.49):
            for label, icon in icons.items():
                frame = Image.new("RGB", (192, 192), (26, 34, 48))
                resized = icon.resize(
                    (round(icon.width * scale), round(icon.height * scale)),
                    Image.Resampling.BILINEAR,
                )
                frame.paste(resized, (31, 42), resized)
                original = [
                    arrow.name
                    for arrow in Arrow
                    if any(
                        locate(part, frame, 0.9) is not None
                        for part in legacy["arrow_subicons"][arrow.value]
                    )
                ]
                prediction = matcher.predict(frame)
                trials.append(
                    {
                        "resolution": resolution,
                        "scale": scale,
                        "expected": label,
                        "legacy_candidates": original,
                        "legacy_correct": original == [label],
                        "calibrated": prediction,
                        "calibrated_correct": prediction["label"] == label,
                    }
                )
        blank = Image.new("RGB", (192, 192), (26, 34, 48))
        negatives.append(
            {
                "resolution": resolution,
                "case": "blank",
                "prediction": matcher.predict(blank),
            }
        )
        ambiguous = blank.copy()
        ambiguous.paste(icons["Up"], (2, 50), icons["Up"])
        ambiguous.paste(icons["Left"], (110, 50), icons["Left"])
        negatives.append(
            {
                "resolution": resolution,
                "case": "two-directions",
                "prediction": matcher.predict(ambiguous),
            }
        )
        for image in icons.values():
            image.close()
        for image in legacy["arrow_icons"]:
            image.close()
        for parts in legacy["arrow_subicons"]:
            for image in parts:
                image.close()
    return {
        "schema_version": 1,
        "mode": "offline_scale_experiment",
        "source_sha256": PINS,
        "asset_sha256": {
            p.relative_to(source).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in sorted((source / "assets").glob("*/*.png"))
        },
        "trials": trials,
        "negative_cases": negatives,
        "legacy_correct": sum(t["legacy_correct"] for t in trials),
        "calibrated_correct": sum(t["calibrated_correct"] for t in trials),
        "total": len(trials),
        "negatives_rejected": sum(n["prediction"]["label"] is None for n in negatives),
        "elapsed_seconds": time.perf_counter() - started,
        "desktop_inputs": 0,
        "live_frames": 0,
        "limits": "Synthetic source-derived icons in a known cue region. No actual UI-scale, Windows DPI, animation or clutter qualification. Resolution alone cannot determine widget scale.",
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    report = benchmark(args.source.resolve())
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("x", encoding="utf-8") as stream:
        json.dump(report, stream, indent=2)
        stream.write("\n")
    print(
        json.dumps(
            {
                k: report[k]
                for k in (
                    "legacy_correct",
                    "calibrated_correct",
                    "total",
                    "negatives_rejected",
                    "elapsed_seconds",
                )
            }
        )
    )


if __name__ == "__main__":
    main()
