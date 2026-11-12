"""Bounded saved-image Conjuration recognition; no capture or input APIs.

Pillow and NumPy are optional offline dependencies, imported only on construction
or image loading. Phase and matching group size are never inferred here.
"""

from dataclasses import dataclass
import hashlib
import io
import math
from pathlib import Path

from conjuration_memory import Board, Card
from game_data import decode

MAX_PIXELS = 4_194_304
MAX_IMAGE_BYTES = 16 * 1024 * 1024
DESCRIPTOR_SIZE = (32, 32)


def _number(value, low, high, name):
    if type(value) not in (int, float):
        raise ValueError(f"Invalid {name}")
    try:
        number = float(value)
    except (ValueError, OverflowError) as error:
        raise ValueError(f"Invalid {name}") from error
    if not math.isfinite(number) or not low <= number <= high:
        raise ValueError(f"Invalid {name}")
    return number


def _identifier(value, name):
    if not isinstance(value, str) or not value.strip() or len(value) > 128:
        raise ValueError(f"Invalid {name}")
    return value


def _roi(value):
    if not isinstance(value, (list, tuple)) or len(value) != 4:
        raise ValueError("An ROI needs normalized x, y, width and height")
    x, y, width, height = (_number(v, 0, 1, "normalized ROI") for v in value)
    if min(width, height) < 0.01 or x + width > 1 or y + height > 1:
        raise ValueError("ROI must be nonempty and entirely inside its parent")
    return x, y, width, height


@dataclass(frozen=True)
class SlotProfile:
    slot: str
    roi: tuple[float, float, float, float]


@dataclass(frozen=True)
class ReferenceProfile:
    key: str
    state: str
    identity: str | None


@dataclass(frozen=True)
class VisionProfile:
    profile_id: str
    qualification: str
    reference_size: tuple[int, int]
    board_roi: tuple[float, float, float, float]
    slots: tuple[SlotProfile, ...]
    references: tuple[ReferenceProfile, ...]
    minimum_similarity: float
    minimum_margin: float
    maximum_patch_error: float
    stable_frames: int
    stable_ms: float
    maximum_gap_ms: float

    @classmethod
    def from_dict(cls, data):
        if (
            not isinstance(data, dict)
            or type(data.get("schema_version")) is not int
            or data.get("schema_version") != 1
        ):
            raise ValueError("Unsupported Conjuration vision profile")
        if set(data) - {
            "schema_version",
            "profile_id",
            "qualification",
            "reference_size",
            "board_roi",
            "slots",
            "references",
            "matching",
        }:
            raise ValueError("Unknown vision profile field")
        profile_id = _identifier(data.get("profile_id"), "profile identifier")
        qualification = data.get("qualification")
        if qualification not in {"synthetic", "recorded_candidate"}:
            raise ValueError("Declare synthetic or recorded_candidate qualification")
        size = data.get("reference_size")
        if (
            not isinstance(size, list)
            or len(size) != 2
            or any(type(v) is not int or not 32 <= v <= 8192 for v in size)
            or size[0] * size[1] > MAX_PIXELS
        ):
            raise ValueError("Invalid bounded reference image size")
        board = _roi(data.get("board_roi"))
        entries = data.get("slots")
        if not isinstance(entries, list) or not 2 <= len(entries) <= 64:
            raise ValueError("Supply between 2 and 64 explicit card slots")
        slots = []
        aspects = []
        for entry in entries:
            if not isinstance(entry, dict) or set(entry) != {"slot", "roi"}:
                raise ValueError("Invalid slot entry")
            slot = SlotProfile(
                _identifier(entry.get("slot"), "slot"), _roi(entry.get("roi"))
            )
            x, y, w, h = slot.roi
            for previous in slots:
                px, py, pw, ph = previous.roi
                if slot.slot == previous.slot or (
                    min(x + w, px + pw) > max(x, px)
                    and min(y + h, py + ph) > max(y, py)
                ):
                    raise ValueError("Card slots must be unique and must not overlap")
            native_w, native_h = size[0] * board[2] * w, size[1] * board[3] * h
            if min(native_w, native_h) < 12:
                raise ValueError("Each reference slot must be at least 12 by 12 pixels")
            aspects.append(native_w / native_h)
            slots.append(slot)
        if max(aspects) / min(aspects) > 1.02:
            raise ValueError("A shared gallery requires consistently shaped card slots")
        entries = data.get("references")
        if not isinstance(entries, list) or not 2 <= len(entries) <= 64:
            raise ValueError("Supply between 2 and 64 frozen reference entries")
        references = []
        for entry in entries:
            if not isinstance(entry, dict) or set(entry) - {"key", "state", "identity"}:
                raise ValueError("Invalid reference entry")
            key = _identifier(entry.get("key"), "reference key")
            state, identity = entry.get("state"), entry.get("identity")
            if state not in {"hidden", "face_up", "removed"}:
                raise ValueError("References must positively identify a card state")
            if state == "face_up":
                _identifier(identity, "visible card identity")
            elif identity is not None:
                raise ValueError("Only face-up references may carry an identity")
            if any(ref.key == key for ref in references):
                raise ValueError("Reference keys must be unique")
            references.append(ReferenceProfile(key, state, identity))
        if not {"hidden", "face_up"}.issubset({ref.state for ref in references}):
            raise ValueError("Explicit hidden and face-up references are required")
        settings = data.get("matching", {})
        if not isinstance(settings, dict):
            raise ValueError("Invalid matching settings")
        if set(settings) - {
            "minimum_similarity",
            "minimum_margin",
            "maximum_patch_error",
            "stable_frames",
            "stable_ms",
            "maximum_gap_ms",
        }:
            raise ValueError("Unknown matching setting")
        frames = settings.get("stable_frames", 2)
        if type(frames) is not int or not 2 <= frames <= 8:
            raise ValueError("Require between 2 and 8 stable observations")
        return cls(
            profile_id,
            qualification,
            tuple(size),
            board,
            tuple(slots),
            tuple(references),
            _number(
                settings.get("minimum_similarity", 0.96), 0.90, 0.999, "similarity"
            ),
            _number(settings.get("minimum_margin", 0.03), 0.01, 0.3, "margin"),
            _number(
                settings.get("maximum_patch_error", 0.14), 0.02, 0.2, "patch error"
            ),
            frames,
            _number(settings.get("stable_ms", 50), 20, 500, "stable duration"),
            _number(settings.get("maximum_gap_ms", 250), 50, 1000, "observation gap"),
        )

    @classmethod
    def load(cls, path):
        source = Path(path)
        with source.open("rb") as stream:
            payload = stream.read(65_537)
        if len(payload) > 65_536:
            raise ValueError("Vision profile exceeds 64 KiB")
        return cls.from_dict(decode(payload))


def _rgb(image, *, reference=False):
    from PIL import Image
    import numpy as np

    limit = 262_144 if reference else MAX_PIXELS
    if (
        not isinstance(image, Image.Image)
        or min(image.size) < 12
        or max(image.size) > 8192
        or image.width * image.height > limit
    ):
        raise ValueError("Image geometry exceeds the bounded recognition contract")
    if image.mode not in {"RGB", "RGBA", "L"}:
        raise ValueError("Use an opaque RGB, RGBA or grayscale saved image")
    image.load()
    if image.mode == "RGBA" and image.getextrema()[3] != (255, 255):
        raise ValueError("Transparent or partial images cannot establish card state")
    return np.asarray(image.convert("RGB"), dtype=np.uint8).copy()


def decode_image(payload: bytes):
    """Decode the exact bounded bytes whose digest the caller verified."""
    from PIL import Image

    if not isinstance(payload, bytes):
        raise ValueError("Saved-image payload must be bytes")
    if len(payload) > MAX_IMAGE_BYTES:
        raise ValueError("Saved image exceeds 16 MiB")
    try:
        with Image.open(io.BytesIO(payload)) as image:
            return Image.fromarray(_rgb(image))
    except (OSError, SyntaxError, Image.DecompressionBombError) as error:
        raise ValueError("Saved image is corrupt or unsupported") from error


def load_image(path):
    """Decode one bounded opaque image, fully detached from its read-only file."""
    with Path(path).open("rb") as stream:
        payload = stream.read(MAX_IMAGE_BYTES + 1)
    return decode_image(payload)


@dataclass(frozen=True)
class SlotEvidence:
    slot: str
    state: str
    identity: str | None
    candidate_state: str | None
    candidate_identity: str | None
    similarity: float
    margin: float
    patch_error: float
    stable_samples: int
    stable_span_ms: float
    reason: str


@dataclass(frozen=True)
class VisionObservation:
    board: Board
    time_ms: float
    source_size: tuple[int, int]
    profile_id: str
    phase_source: str
    slots: tuple[SlotEvidence, ...]
    reference_hashes: tuple[tuple[str, str], ...]


class ConjurationVision:
    """Explicit ROI/gallery recognizer with conservative temporal qualification.

    Revisions and source timestamps must increase. New geometry needs a new board
    identifier. Supplied phase is context, not a claim that this class detects it.
    Unknown immediately withholds a prior label; only fresh positive pixels rearm.
    """

    def __init__(self, profile: VisionProfile, reference_images):
        from PIL import Image
        import numpy as np

        self._Image, self._np = Image, np
        # Revalidate even a caller-created dataclass rather than trust its type.
        from dataclasses import asdict

        values = asdict(profile)
        data = {
            name: values[name]
            for name in (
                "profile_id",
                "qualification",
                "reference_size",
                "board_roi",
                "slots",
                "references",
            )
        }
        data.update(
            schema_version=1,
            reference_size=list(profile.reference_size),
            slots=list(data["slots"]),
            references=list(data["references"]),
        )
        data["matching"] = {
            name: values[name]
            for name in (
                "minimum_similarity",
                "minimum_margin",
                "maximum_patch_error",
                "stable_frames",
                "stable_ms",
                "maximum_gap_ms",
            )
        }
        self.profile = VisionProfile.from_dict(data)
        if set(reference_images) != {ref.key for ref in self.profile.references}:
            raise ValueError("Supply exactly the frozen reference gallery keys")
        self._references = []
        hashes = []
        p = self.profile
        expected_aspect = (
            p.reference_size[0]
            * p.board_roi[2]
            * p.slots[0].roi[2]
            / (p.reference_size[1] * p.board_roi[3] * p.slots[0].roi[3])
        )
        for ref in p.references:
            rgb = _rgb(reference_images[ref.key], reference=True)
            if abs(rgb.shape[1] / rgb.shape[0] / expected_aspect - 1) > 0.03:
                raise ValueError(
                    "Reference aspect differs from qualified slot geometry"
                )
            descriptor = self._descriptor(Image.fromarray(rgb))
            if float(np.std(descriptor, axis=(0, 1)).max()) < 0.04:
                raise ValueError(
                    "Plain backgrounds cannot positively identify card state"
                )
            descriptor.setflags(write=False)
            self._references.append((ref, descriptor))
            hashes.append(
                (
                    ref.key,
                    hashlib.sha256(
                        descriptor.astype("<f4", copy=False).tobytes()
                    ).hexdigest(),
                )
            )
        self._hashes = tuple(hashes)
        self._board_id = None
        self._seen_boards = set()
        self._size = None
        self._revision = -1
        self._time = None
        self._stable = {}

    def _descriptor(self, image):
        return (
            self._np.asarray(
                image.resize(DESCRIPTOR_SIZE, self._Image.Resampling.BILINEAR),
                dtype=self._np.float32,
            )
            / 255.0
        )

    def _classify(self, descriptor):
        scores = {}
        for reference, template in self._references:
            difference = self._np.abs(descriptor - template)
            score = 1 - float(difference.mean())
            patch = float(difference.reshape(4, 8, 4, 8, 3).mean(axis=(1, 3, 4)).max())
            key = (reference.state, reference.identity)
            if key not in scores or score > scores[key][0]:
                scores[key] = score, patch
        ranked = sorted(scores.items(), key=lambda item: item[1][0], reverse=True)
        label, (score, patch) = ranked[0]
        margin = score - ranked[1][1][0]
        p = self.profile
        reason = (
            "low-similarity"
            if score < p.minimum_similarity
            else (
                "ambiguous-reference"
                if margin < p.minimum_margin
                else (
                    "partial-or-misaligned-card"
                    if patch > p.maximum_patch_error
                    else "accepted"
                )
            )
        )
        return label, score, margin, patch, reason

    def observe(self, image, *, board_id, revision, time_ms, phase):
        _identifier(board_id, "board identifier")
        if type(revision) is not int or revision < 0:
            raise ValueError("Use a nonnegative integer observation revision")
        time_ms = _number(time_ms, 0, 1e15, "source timestamp")
        if self._time is not None and time_ms <= self._time:
            raise ValueError("Source timestamps must strictly increase")
        if phase not in {"preview", "active", "animation", "result", "unknown"}:
            raise ValueError("Unsupported supplied phase")
        rgb = _rgb(image)
        size = (rgb.shape[1], rgb.shape[0])
        if self._board_id == board_id:
            if revision <= self._revision:
                raise ValueError("Observation revisions must strictly increase")
            if size != self._size:
                raise ValueError(
                    "Changed source geometry requires a new board identifier"
                )
        else:
            if board_id in self._seen_boards:
                raise ValueError("A retired board identifier cannot be reused")
            if len(self._seen_boards) >= 128:
                raise ValueError("Recognition session exceeds 128 distinct boards")
            self._seen_boards.add(board_id)
            self._stable.clear()
        if (
            self._time is not None
            and time_ms - self._time > self.profile.maximum_gap_ms
        ):
            self._stable.clear()
        self._board_id, self._revision, self._time, self._size = (
            board_id,
            revision,
            time_ms,
            size,
        )
        p = self.profile
        aspect_ok = (
            abs(size[0] / size[1] / (p.reference_size[0] / p.reference_size[1]) - 1)
            <= 0.015
        )
        bx, by, bw, bh = p.board_roi
        cards, evidence = [], []
        for slot in p.slots:
            x, y, w, h = slot.roi
            left, top = round((bx + x * bw) * size[0]), round((by + y * bh) * size[1])
            right, bottom = round((bx + (x + w) * bw) * size[0]), round(
                (by + (y + h) * bh) * size[1]
            )
            label, score, margin, patch = (None, None), 0.0, 0.0, 1.0
            reason = "source-aspect-mismatch"
            descriptor = None
            if aspect_ok and min(right - left, bottom - top) >= 12:
                descriptor = self._descriptor(
                    self._Image.fromarray(rgb[top:bottom, left:right])
                )
                label, score, margin, patch, reason = self._classify(descriptor)
            elif aspect_ok:
                reason = "card-resolution-too-small"
            if phase not in {"preview", "active"}:
                reason = "supplied-phase-not-stable"
            samples, span = 0, 0.0
            state, identity = "unknown", None
            if reason == "accepted":
                previous = self._stable.get(slot.slot)
                if (
                    previous
                    and previous[0] == label
                    and float(self._np.abs(descriptor - previous[4]).mean()) <= 0.02
                ):
                    samples, first = min(previous[1] + 1, 1_000_000), previous[2]
                else:
                    samples, first = 1, time_ms
                span = time_ms - first
                self._stable[slot.slot] = (label, samples, first, time_ms, descriptor)
                if samples >= p.stable_frames and span >= p.stable_ms:
                    state, identity = label
                else:
                    reason = "awaiting-stable-pixels"
            else:
                self._stable.pop(slot.slot, None)
            cards.append(
                Card(
                    slot.slot, (left + right) // 2, (top + bottom) // 2, state, identity
                )
            )
            evidence.append(
                SlotEvidence(
                    slot.slot,
                    state,
                    identity,
                    label[0],
                    label[1],
                    score,
                    margin,
                    patch,
                    samples,
                    span,
                    reason,
                )
            )
        return VisionObservation(
            Board(board_id, revision, phase, tuple(cards)),
            time_ms,
            size,
            p.profile_id,
            "caller-supplied-not-detected",
            tuple(evidence),
            self._hashes,
        )
