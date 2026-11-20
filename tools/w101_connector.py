"""Pure candidate connector ranking; no process access or input.

Heading is canonical CCW from +X. Obstacle boxes are already transformed world
bounds, never raw BCD dimensions. Broad-phase hits reject conservatively; absence
of hits cannot qualify a corridor or establish floor support.
"""

import heapq
import math


def intersects(start, end, lower, upper, margin):
    lo, hi = 0.0, 1.0
    for s, e, a, b in zip(start, end, lower, upper):
        a, b = a - margin, b + margin
        delta = e - s
        if abs(delta) < 1e-12:
            if s < a or s > b:
                return False
        else:
            x, y = sorted(((a - s) / delta, (b - s) / delta))
            lo, hi = max(lo, x), min(hi, y)
            if lo > hi:
                return False
    return True


def select(
    points,
    edges,
    goal,
    pose,
    *,
    settled,
    fresh,
    heading,
    max_distance,
    max_elevation,
    max_grade,
    margin,
    turn_cost,
    obstacles=(),
    geometry_complete=False,
    qualified_vertices=()
):
    """Rank candidate connectors; qualification must be supplied for THIS pose,
    asset/profile/epoch by the owning atlas, not reused from another landing.
    All limits are explicit calibration inputs, not asserted game constants.
    """
    if not 0 < len(points) <= 65536 or len(edges) > 1000000:
        raise ValueError("graph budget")
    vectors = [pose, *points]
    if any(len(p) != 3 or not all(math.isfinite(v) for v in p) for p in vectors):
        raise ValueError("invalid position")
    values = [heading, max_distance, max_elevation, max_grade, margin, turn_cost]
    if not all(math.isfinite(v) for v in values) or min(values[1:]) < 0:
        raise ValueError("invalid calibration")
    if not 0 <= goal < len(points) or len(obstacles) > 10000:
        raise ValueError("goal or obstacle budget")
    for box in obstacles:
        if any(
            len(box[k]) != 3 or not all(map(math.isfinite, box[k]))
            for k in ("min", "max")
        ):
            raise ValueError("invalid obstacle")
        if any(a > b for a, b in zip(box["min"], box["max"])):
            raise ValueError("inverted obstacle")
    if not settled or not fresh:
        return dict(status="pose-not-fresh-and-settled", candidates=[], selected=None)
    reverse = [[] for _ in points]
    for a, b in edges:
        if not 0 <= a < len(points) or not 0 <= b < len(points):
            raise ValueError("edge endpoint")
        reverse[b].append((a, math.dist(points[a], points[b])))
    distances = {goal: 0.0}
    queue = [(0.0, goal)]
    while queue:
        distance, node = heapq.heappop(queue)
        if distance != distances[node]:
            continue
        for previous, cost in reverse[node]:
            value = distance + cost
            if value < distances.get(previous, math.inf):
                distances[previous] = value
                heapq.heappush(queue, (value, previous))
    candidates = []
    for i, point in enumerate(points):
        xy = math.hypot(point[0] - pose[0], point[1] - pose[1])
        dz = point[2] - pose[2]
        if (
            i not in distances
            or xy > max_distance
            or abs(dz) > max_elevation
            or abs(dz) / max(xy, 1) > max_grade
        ):
            continue
        hits = [
            b["name"]
            for b in obstacles
            if intersects(pose, point, b["min"], b["max"], margin)
        ]
        turn = (
            0
            if xy == 0
            else abs(
                math.atan2(
                    math.sin(
                        math.atan2(point[1] - pose[1], point[0] - pose[0]) - heading
                    ),
                    math.cos(
                        math.atan2(point[1] - pose[1], point[0] - pose[0]) - heading
                    ),
                )
            )
        )
        candidates.append(
            dict(
                vertex=i,
                distanceXy=xy,
                deltaZ=dz,
                turnRadians=turn,
                score=xy + distances[i] + turn_cost * turn,
                obstacleHits=hits,
                qualified=geometry_complete and not hits and i in qualified_vertices,
            )
        )
    candidates.sort(key=lambda c: (c["score"], c["vertex"]))
    selected = next((c["vertex"] for c in candidates if c["qualified"]), None)
    return dict(
        status=(
            "connector-qualified"
            if selected is not None
            else "arrived-in-zone-connector-unqualified"
        ),
        candidates=candidates,
        selected=selected,
    )
