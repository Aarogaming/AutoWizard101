"""Candidate moving-pose windows from injected reads; no client access or hooks.

Identity fences and calibrated kinematics reject some bad reads, but cannot prove
atomicity or rule out torn fields. The owner must independently qualify the layout,
target identity and calibration before using observations for navigation.
"""

import asyncio
import copy
from dataclasses import dataclass
import math
import struct
import time

from w101_combat_reader import _Reader, _Rejected, _address
from w101_pose_reader import LAYOUT_ID


@dataclass(frozen=True)
class PoseContext:
    """Owner-supplied lifetime tokens; refresh session on every loading edge.

    Tokens must change even when a transition returns to the same zone/wizard.
    This module cannot detect an unreported loading edge between callbacks.
    """

    target: str
    session: str
    zone: str
    wizard: str
    loading: bool = False


@dataclass(frozen=True)
class MovingPoseLimits:
    """Explicit calibration, in client units and monotonic seconds/radians.

    Rate limits have deliberately no defaults. They are rejection bounds, not
    measured speed or permission to move. Each call contains 2–8 samples.
    """

    max_planar_speed: float
    max_vertical_speed: float
    max_angular_speed: float
    sample_count: int = 3
    interval_seconds: float = 0.05
    max_gap_seconds: float = 0.25
    max_read_seconds: float = 0.1
    max_sample_seconds: float = 0.2
    max_duration_seconds: float = 1.5


@dataclass(frozen=True)
class MovingPoseStreamLimits(MovingPoseLimits):
    """Finite stream budget; no unbounded recorder or implicit qualification."""

    sample_count: int = 200
    max_duration_seconds: float = 15.0


async def read_moving_pose(
    read_bytes,
    body_address,
    *,
    layout_id,
    expected_context,
    get_context,
    limits,
    clock=time.monotonic,
    delay=asyncio.sleep,
):
    """Return a bounded candidate window or Unknown with a fixed reason code.

    ``get_context()`` is synchronous and must return current PoseContext without
    blocking. ``read_bytes`` and ``delay`` are asynchronous/cancellation-aware.
    Times are monotonic seconds, meaningful only within this clock lifetime.
    Maximum reads/bytes: 40/384. Outer real-time deadline also bounds delays.
    Unknown results never retain partially accepted positions or old samples.
    """
    return await _read_pose(
        read_bytes,
        body_address,
        layout_id=layout_id,
        expected_context=expected_context,
        get_context=get_context,
        limits=limits,
        clock=clock,
        delay=delay,
    )


async def stream_moving_pose(
    read_bytes,
    body_address,
    *,
    layout_id,
    expected_context,
    get_context,
    limits,
    on_sample,
    refresh_context=None,
    on_invalidated=None,
    clock=time.monotonic,
    delay=asyncio.sleep,
):
    """Emit bounded candidate samples with backpressure and constant retention.

    ``refresh_context`` is awaited before and after each sample and must update
    the synchronous ``get_context`` cache. The owner checks loading, zone and
    export identity there; this helper never opens hooks or accesses a client.
    ``on_sample`` receives a detached sample, after all fences pass. Previously
    emitted samples are historical evidence only, not current authority. Any
    failure calls ``on_invalidated`` with a sanitized terminal summary; its
    best-effort delivery is bounded to 100 ms. Cancellation is then propagated.
    Owner must also invalidate on worker exit, missing heartbeat or EOF because
    no callback can guarantee delivery after process/transport failure.

    At most 1200 samples, 6000 reads / 57600 bytes and 60 seconds, excluding
    owner refresh/callback work (which shares the deadline). No samples retained
    in returned summary; only the previous sample is retained for rate checks.
    All callbacks must cooperate with cancellation. No model, input or hooks.
    """
    return await _read_pose(
        read_bytes,
        body_address,
        layout_id=layout_id,
        expected_context=expected_context,
        get_context=get_context,
        limits=limits,
        clock=clock,
        delay=delay,
        streaming=True,
        on_sample=on_sample,
        refresh_context=refresh_context,
        on_invalidated=on_invalidated,
    )


async def _read_pose(
    read_bytes,
    body_address,
    *,
    layout_id,
    expected_context,
    get_context,
    limits,
    clock,
    delay,
    streaming=False,
    on_sample=None,
    refresh_context=None,
    on_invalidated=None,
):
    reader = None
    total_calls = total_bytes = emitted = 0
    result = {
        "status": "unknown",
        "liveQualified": False,
        "movementAuthorized": False,
        "atomicityVerified": False,
        "timebase": "injected-monotonic-seconds",
        "layoutId": layout_id if layout_id == LAYOUT_ID else None,
        "samples": [],
    }
    last_time = None
    start = None

    def tick():
        nonlocal last_time
        value = clock()
        if type(value) not in (float, int) or not math.isfinite(value):
            raise _Rejected("invalid-clock")
        if last_time is not None and value < last_time:
            raise _Rejected("clock-moved-backward")
        last_time = value
        if start is not None and value - start > limits.max_duration_seconds:
            raise _Rejected("window-expired")
        return value

    def context():
        current = get_context()
        if not isinstance(current, PoseContext):
            raise _Rejected("invalid-context")
        if current.loading:
            raise _Rejected("loading")
        if current != expected_context:
            raise _Rejected("context-changed")

    async def collect():
        nonlocal start, reader, total_calls, total_bytes, emitted
        if layout_id != LAYOUT_ID:
            raise _Rejected("unreviewed-layout")
        if not isinstance(
            limits, MovingPoseStreamLimits if streaming else MovingPoseLimits
        ):
            raise _Rejected("invalid-limits")
        numbers = (
            limits.max_planar_speed,
            limits.max_vertical_speed,
            limits.max_angular_speed,
            limits.interval_seconds,
            limits.max_gap_seconds,
            limits.max_read_seconds,
            limits.max_sample_seconds,
            limits.max_duration_seconds,
        )
        if any(
            type(v) not in (int, float) or not math.isfinite(v) or v <= 0
            for v in numbers
        ):
            raise _Rejected("invalid-limits")
        if (
            type(limits.sample_count) is not int
            or not 2 <= limits.sample_count <= (1200 if streaming else 8)
            or limits.max_duration_seconds > (60 if streaming else 1.5)
            or not limits.interval_seconds
            <= limits.max_gap_seconds
            <= limits.max_duration_seconds
            or not limits.max_read_seconds
            <= limits.max_sample_seconds
            <= limits.max_duration_seconds
        ):
            raise _Rejected("invalid-limits")
        if (
            not isinstance(expected_context, PoseContext)
            or expected_context.loading
            or any(
                type(v) is not str or not v.strip()
                for v in (
                    expected_context.target,
                    expected_context.session,
                    expected_context.zone,
                    expected_context.wizard,
                )
            )
        ):
            raise _Rejected("invalid-context")
        if streaming and (
            not callable(on_sample)
            or refresh_context is not None
            and not callable(refresh_context)
            or on_invalidated is not None
            and not callable(on_invalidated)
        ):
            raise _Rejected("invalid-callback")
        body = _address(body_address, 8)
        start = tick()
        samples = []
        identity = None
        previous_sample = None
        for index in range(limits.sample_count):
            if index:
                await delay(limits.interval_seconds)
            if refresh_context is not None:
                await refresh_context()
            context()
            begun = tick()
            if reader is not None:
                total_calls += reader.calls
                total_bytes += reader.bytes
            reader = _Reader(read_bytes, None)
            reads = []

            async def get(offset, count):
                context()
                before = tick()
                value = await reader.get(body + offset, count)
                after = tick()
                context()
                if after - before > limits.max_read_seconds:
                    raise _Rejected("stale-read")
                reads.append({"started": before, "completed": after})
                return value

            parent = await get(72, 8)
            _address(struct.unpack("<Q", parent)[0], 8)
            marker = await get(0x474, 4)
            if struct.unpack("<I", marker)[0] != 8:
                raise _Rejected("actor-marker-mismatch")
            if identity is not None and identity != (parent, marker):
                raise _Rejected("actor-identity-changed")
            identity = (parent, marker)
            pose = struct.unpack("<6f", await get(88, 24))
            if not all(math.isfinite(v) for v in pose):
                raise _Rejected("nonfinite-pose")
            x, y, z, _, _, yaw = pose
            if max(abs(x), abs(y), abs(z)) > 10_000_000 or abs(yaw) > math.tau + 0.01:
                raise _Rejected("implausible-pose")
            if parent != await get(72, 8) or marker != await get(0x474, 4):
                raise _Rejected("actor-identity-changed")
            if refresh_context is not None:
                await refresh_context()
            context()
            ended = tick()
            if ended - begun > limits.max_sample_seconds:
                raise _Rejected("stale-sample")
            measured = (reads[2]["started"] + reads[2]["completed"]) / 2
            sample = {
                "started": begun,
                "completed": ended,
                "observed": measured,
                "position": {"x": x, "y": y, "z": z},
                "yawRadians": yaw,
                "reads": reads,
            }
            if previous_sample is not None:
                old = previous_sample
                elapsed = measured - old["observed"]
                if elapsed <= 0 or elapsed > limits.max_gap_seconds:
                    raise _Rejected("sample-gap")
                p = old["position"]
                angle = abs((yaw - old["yawRadians"] + math.pi) % math.tau - math.pi)
                if (
                    math.hypot(x - p["x"], y - p["y"]) / elapsed
                    > limits.max_planar_speed
                ):
                    raise _Rejected("planar-jump")
                if abs(z - p["z"]) / elapsed > limits.max_vertical_speed:
                    raise _Rejected("vertical-jump")
                if angle / elapsed > limits.max_angular_speed:
                    raise _Rejected("angular-jump")
            previous_sample = sample
            if streaming:
                await on_sample(copy.deepcopy(sample))
                emitted += 1
                context()
                tick()
            else:
                samples.append(sample)
        context()
        tick()
        return samples

    async def invalidate():
        result["emittedSamples"] = emitted
        result.update(
            readCalls=total_calls + (reader.calls if reader else 0),
            readBytes=total_bytes + (reader.bytes if reader else 0),
        )
        if streaming and callable(on_invalidated):
            try:
                await asyncio.wait_for(on_invalidated(copy.deepcopy(result)), 0.1)
            except (Exception, asyncio.CancelledError):
                pass

    try:
        timeout = 60.0 if streaming else 1.5
        if (
            isinstance(limits, MovingPoseLimits)
            and type(limits.max_duration_seconds) in (int, float)
            and math.isfinite(limits.max_duration_seconds)
            and limits.max_duration_seconds > 0
        ):
            timeout = min(timeout, limits.max_duration_seconds)
        result["samples"] = await asyncio.wait_for(collect(), timeout=timeout)
        result["status"] = "candidate"
        if streaming:
            result["sampleCount"] = emitted
    except asyncio.CancelledError:
        result["reason"] = "cancelled"
        await invalidate()
        raise
    except _Rejected as error:
        result["reason"] = str(error)
    except asyncio.TimeoutError:
        result["reason"] = "window-timeout"
    except Exception:
        result["reason"] = "observation-failed"
    result.update(
        readCalls=total_calls + (reader.calls if reader else 0),
        readBytes=total_bytes + (reader.bytes if reader else 0),
    )
    if streaming and result["status"] != "candidate":
        await invalidate()
    return result
