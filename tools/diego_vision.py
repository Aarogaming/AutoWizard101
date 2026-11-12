"""Offline, example-based Dueling Diego vision. Contains no capture or input APIs."""

from dataclasses import asdict, dataclass
import json
from pathlib import Path
import uuid

import cv2
import numpy as np
from PIL import Image

LABELS = (
    "Diego",
    "Barrel",
    "Gobbler",
    "Gobbler shield high",
    "Gobbler shield low",
    "Pirate",
    "Pirate windup",
    "Fire Elf",
    "Nose Goblin",
    "Skeleton",
    "Roberto",
    "Projectile",
    "Falling stalactite",
)


def rectangle(box, size):
    if len(box) != 4 or any(type(value) is not int for value in box):
        raise ValueError("A selection must contain four whole-number coordinates.")
    x, y, width, height = box
    if (
        x < 0
        or y < 0
        or width < 8
        or height < 8
        or x + width > size[0]
        or y + height > size[1]
    ):
        raise ValueError("Select an area at least 8 by 8 pixels inside the image.")
    return tuple(box)


@dataclass(frozen=True)
class Detection:
    label: str
    box: tuple
    similarity: float

    @property
    def center(self):
        x, y, width, height = self.box
        return x + width / 2, y + height / 2


def overlap(first, second):
    ax, ay, aw, ah = first
    bx, by, bw, bh = second
    area = max(0, min(ax + aw, bx + bw) - max(ax, bx)) * max(
        0, min(ay + ah, by + bh) - max(ay, by)
    )
    return area / min(aw * ah, bw * bh)


class Examples:
    """A local pack tied to an exact source size and user-selected play area."""

    def __init__(self, folder):
        self.folder = Path(folder)
        self.size = None
        self.roi = None
        self.entries = []
        self.images = {}
        manifest = self.folder / "examples.json"
        if manifest.exists():
            data = json.loads(manifest.read_text(encoding="utf-8"))
            if not isinstance(data, dict) or data.get("version") != 1:
                raise ValueError("Unsupported example pack version.")
            size = data.get("size")
            if (
                not isinstance(size, list)
                or len(size) != 2
                or any(type(v) is not int or v <= 0 for v in size)
            ):
                raise ValueError("Invalid example image size.")
            self.size = tuple(size)
            self.roi = rectangle(data["roi"], self.size)
            entries = data.get("examples")
            if not isinstance(entries, list) or len(entries) > 80:
                raise ValueError("An example pack supports up to 80 examples.")
            for entry in entries:
                if not isinstance(entry, dict) or entry.get("label") not in LABELS:
                    raise ValueError("Unrecognized example label.")
                name = entry.get("file", "")
                if (
                    Path(name).name != name
                    or not name.endswith(".png")
                    or "/" in name
                    or "\\" in name
                ):
                    raise ValueError(
                        "Example files must be PNGs inside the pack folder."
                    )
                with Image.open(self.folder / name) as image:
                    array = np.array(image.convert("RGB"))
                self._validate_crop(array)
                self.images[name] = array
                self.entries.append(dict(entry))

    @staticmethod
    def _validate_crop(array):
        if min(array.shape[:2]) < 8 or max(array.shape[:2]) > 1024:
            raise ValueError("Examples must be between 8 and 1024 pixels on each side.")
        # Constant patches match backgrounds ambiguously and cannot identify a subject.
        if float(np.std(array.astype(np.float32), axis=(0, 1)).max()) < 8:
            raise ValueError(
                "That example is too plain. Select a recognizable subject with detail."
            )

    def configure(self, size, roi):
        checked = rectangle(roi, size)
        if self.entries and (tuple(size) != self.size or checked != self.roi):
            raise ValueError(
                "Start a new example pack to change the play area or source size."
            )
        self.size, self.roi = tuple(size), checked

    def add(self, image, box, label):
        if self.roi is None or image.size != self.size:
            raise ValueError("Mark the play area for this image size first.")
        if label not in LABELS:
            raise ValueError("Choose a supported label.")
        if len(self.entries) >= 80:
            raise ValueError(
                "This pack already contains 80 examples. Start a new pack."
            )
        x, y, width, height = rectangle(box, image.size)
        rx, ry, rw, rh = self.roi
        if x < rx or y < ry or x + width > rx + rw or y + height > ry + rh:
            raise ValueError("Select the subject entirely inside the marked play area.")
        array = np.array(image.convert("RGB"))[y : y + height, x : x + width].copy()
        self._validate_crop(array)
        name = uuid.uuid4().hex + ".png"
        self.folder.mkdir(parents=True, exist_ok=True)
        Image.fromarray(array).save(self.folder / name)
        self.images[name] = array
        self.entries.append({"label": label, "file": name})
        self.save()

    def save(self):
        if self.size is None or self.roi is None:
            raise ValueError("Mark a play area before saving.")
        self.folder.mkdir(parents=True, exist_ok=True)
        data = {
            "version": 1,
            "size": self.size,
            "roi": self.roi,
            "examples": self.entries,
        }
        destination = self.folder / "examples.json"
        temporary = destination.with_suffix(".tmp")
        temporary.write_text(json.dumps(data, indent=2), encoding="utf-8")
        temporary.replace(destination)

    def detect(self, image, threshold=0.94):
        if not 0.8 <= threshold <= 0.999:
            raise ValueError("Similarity threshold must be between 0.8 and 0.999.")
        if self.roi is None:
            raise ValueError("Mark a play area first.")
        if image.size != self.size:
            raise ValueError(
                "Image size differs from this pack. Use matching captures or a new pack."
            )
        x, y, width, height = self.roi
        frame = np.array(image.convert("RGB"))[y : y + height, x : x + width]
        candidates = []
        for entry in self.entries:
            template = self.images[entry["file"]]
            th, tw = template.shape[:2]
            if th > height or tw > width:
                continue
            scores = cv2.matchTemplate(frame, template, cv2.TM_SQDIFF_NORMED)
            # Cap repeated matches and suppress nearby peaks before combining examples.
            for _ in range(12):
                minimum, _, position, _ = cv2.minMaxLoc(scores)
                similarity = 1 - minimum
                if not np.isfinite(similarity) or similarity < threshold:
                    break
                px, py = position
                candidates.append(
                    Detection(
                        entry["label"], (x + px, y + py, tw, th), round(similarity, 4)
                    )
                )
                scores[
                    max(0, py - th // 2) : py + th // 2 + 1,
                    max(0, px - tw // 2) : px + tw // 2 + 1,
                ] = 2
        kept = []
        for candidate in sorted(
            candidates, key=lambda match: match.similarity, reverse=True
        ):
            if any(
                other.label != candidate.label
                and abs(other.similarity - candidate.similarity) < 0.025
                and overlap(candidate.box, other.box) > 0.35
                for other in candidates
            ):
                continue
            if not any(overlap(candidate.box, other.box) > 0.35 for other in kept):
                kept.append(candidate)
        return kept


class SceneTracker:
    """Associate only currently visible subjects; never schedule or predict spawns."""

    def __init__(self):
        self.previous = []
        self.frame_id = None
        self.current = []

    def update(self, detections, frame_id):
        if frame_id == self.frame_id:
            return self.current
        if self.frame_id is not None and frame_id != self.frame_id + 1:
            self.previous = []
        unused = list(self.previous)
        players = [item for item in detections if item.label == "Diego"]
        player_x = players[0].center[0] if len(players) == 1 else None
        current = []
        for item in detections:
            # Different labels/poses are reacquired rather than assumed equivalent.
            nearby = [
                old
                for old in unused
                if old["detection"].label == item.label
                and abs(old["detection"].center[0] - item.center[0])
                < max(100, item.box[2] * 2)
                and abs(old["detection"].center[1] - item.center[1])
                < max(70, item.box[3])
            ]
            old = (
                min(
                    nearby,
                    key=lambda candidate: sum(
                        (a - b) ** 2
                        for a, b in zip(candidate["detection"].center, item.center)
                    ),
                )
                if nearby
                else None
            )
            if old is not None:
                unused.remove(old)
            gap = item.center[0] - player_x if player_x is not None else None
            trend = "unknown"
            if old is not None and gap is not None and old["gap"] is not None:
                change = gap - old["gap"]
                trend = (
                    "approaching"
                    if change < -3
                    else "receding" if change > 3 else "steady"
                )
            current.append(
                {
                    "detection": item,
                    "seen": old["seen"] + 1 if old else 1,
                    "gap": gap,
                    "trend": trend,
                }
            )
        # An absent subject is discarded immediately; there is no remembered next enemy.
        self.frame_id, self.previous, self.current = frame_id, current, current
        return current


def suggestions(detections, tracks=None):
    """Conservative review prompts, never executable actions or calibrated timings."""
    players = [item for item in detections if item.label == "Diego"]
    if len(players) != 1:
        return ["Unknown: identify exactly one Diego before suggesting responses."]
    player = players[0]
    if tracks is not None and not any(
        track["detection"] == player and track["seen"] >= 2 for track in tracks
    ):
        return [
            "Acquire Diego in a second consecutive frame before suggesting responses."
        ]
    threats = [item for item in detections if item.label != "Diego"]
    if not threats:
        return ["No known threat detected. Unrecognized objects may still be present."]
    prompts = []
    for threat in sorted(
        threats, key=lambda item: abs(item.center[0] - player.center[0])
    ):
        if threat.center[0] < player.center[0]:
            continue
        gap = max(0, threat.box[0] - (player.box[0] + player.box[2]))
        track = next(
            (track for track in tracks or [] if track["detection"] == threat), None
        )
        if tracks is not None and (track is None or track["seen"] < 2):
            prompts.append(
                f"{threat.label}: new match; verify in the next frame before choosing a response."
            )
            continue
        trend = track["trend"] if track is not None else "unknown"
        response = {
            "Barrel": "Jump candidate; check approach distance.",
            "Projectile": "Dodge candidate; verify its height and direction.",
            "Falling stalactite": "Check vertical danger; do not move underneath it.",
            "Gobbler shield high": "Low attack candidate; confirm shield pose.",
            "Gobbler shield low": "High attack candidate; confirm shield pose.",
            "Pirate windup": "Attack opportunity candidate; confirm range.",
            "Roberto": "Track falling stalactites and boss position.",
        }.get(
            threat.label, "Inspect attack height and pose before choosing a response."
        )
        prompts.append(
            f"{threat.label}: gap {gap}px, relative motion {trend}. {response} Timing is uncalibrated."
        )
    return prompts[:4] or ["Known objects are behind Diego; inspect the next frame."]


def report(image, examples, threshold=0.94, tracker=None, frame_id=0):
    detections = examples.detect(image, threshold)
    tracks = tracker.update(detections, frame_id) if tracker is not None else None
    return {
        "mode": "offline-observer",
        "inputSent": False,
        "sourceSize": image.size,
        "playArea": examples.roi,
        "exampleCount": len(examples.entries),
        "similarityThreshold": threshold,
        "detections": [asdict(item) for item in detections],
        "tracks": [
            {
                "label": track["detection"].label,
                "box": track["detection"].box,
                "consecutiveFrames": track["seen"],
                "relativeMotion": track["trend"],
            }
            for track in tracks or []
        ],
        "suggestions": suggestions(detections, tracks),
        "spawnSequenceAssumed": False,
        "limitations": "Example similarity is not probability. No motion or input timing is inferred from one frame.",
    }


def demo(folder):
    """Synthetic fixtures demonstrate plumbing, not recognition of actual game artwork."""
    rng = np.random.default_rng(24101)
    first = np.full((420, 760, 3), (30, 43, 61), dtype=np.uint8)
    player = rng.integers(30, 240, (76, 55, 3), dtype=np.uint8)
    barrel = rng.integers(30, 240, (45, 45, 3), dtype=np.uint8)
    first[230:306, 120:175] = player
    first[261:306, 550:595] = barrel
    image = Image.fromarray(first)
    pack = Examples(folder)
    pack.configure(image.size, (20, 60, 720, 300))
    pack.add(image, (120, 230, 55, 76), "Diego")
    pack.add(image, (550, 261, 45, 45), "Barrel")
    second = first.copy()
    second[261:306, 550:595] = (30, 43, 61)
    second[261:306, 330:375] = barrel
    third = second.copy()
    third[261:306, 330:375] = (30, 43, 61)
    third[261:306, 300:345] = barrel
    return pack, [image, Image.fromarray(second), Image.fromarray(third)]
