"""Bounded, observation-only route landmark study using existing local OpenCV.

Never sends inputs or grants movement authority. Saved demonstrations are
references, not pose truth, path clearance or proof of arrival.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import cv2
import numpy as np

MAX_PIXELS = 4_000_000


def geometry(
    source, destination, inlier_mask, transform, source_height=380, observed_height=380
):
    """Describe image alignment, never world heading or walkable clearance.

    Coordinates use the study's 640-pixel width. A broad, nondegenerate
    consensus is necessary before reporting an image-space alignment cue.
    """
    unavailable = {"alignmentCandidate": False}
    if transform is None or inlier_mask is None:
        return unavailable
    selected = np.asarray(inlier_mask).reshape(-1).astype(bool)
    src = np.asarray(source[selected], dtype=np.float32)
    dst = np.asarray(destination[selected], dtype=np.float32)
    if (
        len(src) < 12
        or not np.isfinite(transform).all()
        or not np.isfinite(src).all()
        or not np.isfinite(dst).all()
    ):
        return unavailable
    source_area = float(cv2.contourArea(cv2.convexHull(src))) / (640 * source_height)
    destination_area = float(cv2.contourArea(cv2.convexHull(dst))) / (
        640 * observed_height
    )
    # Localized UI/text matches and nearly collinear points are not alignment.
    broad = source_area >= 0.025 and destination_area >= 0.025
    anchors = np.float32(
        [[[160, source_height / 2], [320, source_height / 2], [480, source_height / 2]]]
    )
    projected = cv2.perspectiveTransform(anchors, transform)[0]
    finite = bool(np.isfinite(projected).all())
    ordered = finite and bool(np.all(np.diff(projected[:, 0]) > 0))
    bounded = finite and bool(np.all(np.abs(projected) <= 1280))
    accepted = broad and ordered and bounded
    return {
        "alignmentCandidate": bool(accepted),
        "sourceCoverage": round(source_area, 5),
        "observedCoverage": round(destination_area, 5),
        "referenceCenterShiftX": (
            round(float(projected[1, 0] - 320) / 640, 5) if accepted else None
        ),
        "meaning": "image-space-only-not-world-bearing",
    }


def features(path, detector="orb"):
    if detector not in ("orb", "sift"):
        raise ValueError("Choose orb or sift")
    path = Path(path)
    if path.stat().st_size > 12_000_000:
        raise ValueError("Image exceeds study budget")
    frame = cv2.imread(str(path), cv2.IMREAD_GRAYSCALE)
    if frame is None or frame.size > MAX_PIXELS or min(frame.shape) < 100:
        raise ValueError("Invalid or oversized image")
    height, width = frame.shape
    scaled_height = round(height * 640 / width)
    if not 100 <= scaled_height <= 640:
        raise ValueError("Unsupported study aspect ratio")
    frame = cv2.resize(frame, (640, scaled_height))
    height, width = frame.shape
    mask = np.zeros_like(frame)
    mask[
        round(height * 0.16) : round(height * 0.80),
        round(width * 0.07) : round(width * 0.93),
    ] = 255
    # Exclude avatar/mount and much floating player text. This is a study mask,
    # not semantic segmentation or proof that every remaining feature is static.
    mask[round(height * 0.35) :, round(width * 0.36) : round(width * 0.64)] = 0
    extractor = (
        cv2.ORB_create(nfeatures=500)
        if detector == "orb"
        else cv2.SIFT_create(nfeatures=1000)
    )
    points, descriptors = extractor.detectAndCompute(frame, mask)
    return points, descriptors, hashlib.sha256(path.read_bytes()).hexdigest(), height


def compare(reference, observed):
    first, descriptors, _, source_height = reference
    second, current, _, observed_height = observed
    if descriptors is None or current is None or len(current) < 2:
        return {"inliers": 0, "matches": 0, "fraction": 0.0}
    if descriptors.dtype != current.dtype:
        raise ValueError("Cannot mix detector descriptors")
    norm = cv2.NORM_HAMMING if descriptors.dtype == np.uint8 else cv2.NORM_L2
    pairs = cv2.BFMatcher(norm).knnMatch(descriptors, current, k=2)
    candidates = [
        pair[0]
        for pair in pairs
        if len(pair) == 2 and pair[0].distance < 0.7 * pair[1].distance
    ]
    # Prevent several reference features from claiming the same observed point.
    unique = {}
    for match in sorted(candidates, key=lambda item: item.distance):
        unique.setdefault(match.trainIdx, match)
    matches = list(unique.values())
    if len(matches) < 4:
        return {"inliers": 0, "matches": len(matches), "fraction": 0.0}
    source = np.float32([first[m.queryIdx].pt for m in matches])
    destination = np.float32([second[m.trainIdx].pt for m in matches])
    transform, mask = cv2.findHomography(
        source, destination, cv2.RANSAC, 3.0, maxIters=300
    )
    inliers = int(mask.sum()) if mask is not None else 0
    return {
        "inliers": inliers,
        "matches": len(matches),
        "fraction": inliers / len(matches),
        "geometry": geometry(
            source, destination, mask, transform, source_height, observed_height
        ),
    }


class Landmarks:
    def __init__(self, definitions, detector="orb"):
        self.detector = detector
        if not isinstance(definitions, list) or not 1 <= len(definitions) <= 16:
            raise ValueError("Provide 1-16 reviewed landmark references")
        self.references = []
        seen = set()
        for item in definitions:
            name = item["id"]
            if not isinstance(name, str) or not name or len(name) > 80 or name in seen:
                raise ValueError("Landmark IDs must be distinct bounded strings")
            seen.add(name)
            self.references.append((name, features(item["image"], detector)))

    def recognize(self, path):
        observed = features(path, self.detector)
        scores = [
            {
                "id": name,
                **compare(reference, observed),
                "referenceSha256": reference[2],
            }
            for name, reference in self.references
        ]
        scores.sort(key=lambda row: row["inliers"], reverse=True)
        first = scores[0]
        runner_up = scores[1]["inliers"] if len(scores) > 1 else 0
        accepted = (
            first["inliers"] >= 12
            and first["fraction"] >= 0.60
            and first["inliers"] >= runner_up * 1.3 + 3
        )
        return {
            "detector": self.detector,
            "landmarkCandidate": first["id"] if accepted else "Unknown",
            "scores": scores,
            "sourceSha256": observed[2],
            "liveQualified": False,
            "movementAuthorized": False,
            "arrivalVerified": False,
        }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("references")
    parser.add_argument("output")
    parser.add_argument("images", nargs="+")
    parser.add_argument("--detector", choices=("orb", "sift"), default="orb")
    args = parser.parse_args()
    if not 1 <= len(args.images) <= 100:
        raise ValueError("Review at most 100 images per invocation")
    cv2.setNumThreads(1)
    matcher = Landmarks(
        json.loads(Path(args.references).read_text(encoding="utf-8")), args.detector
    )
    results = [
        {"image": str(Path(path).resolve()), **matcher.recognize(path)}
        for path in args.images
    ]
    with Path(args.output).open("x", encoding="utf-8") as stream:
        json.dump(
            {
                "schemaVersion": 1,
                "kind": "offline-route-landmark-study",
                "results": results,
            },
            stream,
            indent=2,
        )


if __name__ == "__main__":
    main()
