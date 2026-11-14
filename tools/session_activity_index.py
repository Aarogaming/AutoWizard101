"""Offline activity index over existing SessionEvents; never controls/captures a game.

Observation payload activity_context supplies source, scene (active/idle/loading),
zone and activity. Missing evidence stays unknown. Intervals are sampling bounds,
not exact transitions; links are candidate observed adjacencies, not playable routes.
"""

import math


def build_index(events, end_ms):
    if not math.isfinite(end_ms) or end_ms < 0:
        raise ValueError("Invalid session end")
    segments = []
    previous_time = -1.0
    current = None
    count = 0
    for event in events:
        count += 1
        if count > 1_000_000:
            raise ValueError("Event budget exceeded")
        if (
            not math.isfinite(event.time_ms)
            or not previous_time <= event.time_ms <= end_ms
        ):
            raise ValueError("Unordered/out-of-range event")
        previous_time = event.time_ms
        if event.kind != "observation":
            continue
        context = event.payload.get("activity_context", {})
        if not isinstance(context, dict):
            raise ValueError("Invalid activity context")
        source = context.get("source")
        scene = context.get("scene")
        if (
            not isinstance(source, str)
            or not source.strip()
            or scene not in {"active", "idle", "loading"}
        ):
            scene, source = "unknown", None
        zone, activity = context.get("zone"), context.get("activity")
        for value in (zone, activity, source):
            if value is not None and (not isinstance(value, str) or len(value) > 256):
                raise ValueError("Invalid context label")
        if scene == "unknown":
            zone, activity = None, None
        key = (scene, zone, activity, source)
        if current is None or current[0] != key:
            if segments:
                segments[-1]["end_ms"] = event.time_ms
            if len(segments) >= 10000:
                raise ValueError("Segment budget exceeded")
            segments.append(
                dict(
                    scene=scene,
                    zone=zone,
                    activity=activity,
                    source=source,
                    start_ms=event.time_ms,
                    end_ms=end_ms,
                    first_event=event.event_id,
                    last_event=event.event_id,
                )
            )
            current = (key,)
        else:
            segments[-1]["last_event"] = event.event_id
    links = []
    for i, segment in enumerate(segments):
        if segment["scene"] != "loading" or i == 0 or i + 1 == len(segments):
            continue
        before, after = segments[i - 1], segments[i + 1]
        if (
            before["scene"] == after["scene"] == "active"
            and before["zone"]
            and after["zone"]
        ):
            links.append(
                dict(
                    from_segment=i - 1,
                    via_segment=i,
                    to_segment=i + 1,
                    kind="observed-loading-adjacency",
                    executable=False,
                )
            )
    return dict(
        schema_version=1,
        segments=segments,
        transition_candidates=links,
        executable=False,
        leading_unobserved_ms=segments[0]["start_ms"] if segments else end_ms,
    )
