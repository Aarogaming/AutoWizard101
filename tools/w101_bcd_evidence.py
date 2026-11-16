"""Offline classification of saved world-space BCD evidence, not walkability.

Mesh slope is a geometric candidate only. Unknown primitive conventions and
collision roles remain explicit, so this exporter never marks coverage complete.
No installed-file, client, input or network access is performed.
"""

import argparse
from collections import Counter
import hashlib
import json
import math
from pathlib import Path


def primitive_bounds(proxy):
    """Over-enclose bounded primitives under the saved R*(scale*v)+T convention.
    Full box dimensions and length+radius capsules intentionally overestimate.
    Bounds do not infer collision roles or authorize a floor exemption.
    """
    kind = proxy["type"]
    expected = {"BOX": 3, "SPHERE": 1, "CYLINDER": 2, "TUBE": 2}
    if kind not in expected:
        return None
    dims, rotation, center, scale = (
        proxy["params"],
        proxy["rotation"],
        proxy["translation"],
        proxy["scale"],
    )
    if len(dims) != expected[kind] or len(rotation) != 9 or len(center) != 3:
        raise ValueError("invalid primitive shape")
    if not all(math.isfinite(v) for v in [*dims, *rotation, *center, scale]) or any(
        v < 0 for v in dims
    ):
        raise ValueError("invalid primitive dimensions")
    # Enclosing local sphere avoids underestimating rotated shapes or relying
    # on half-length versus full-length conventions for cylinder variants.
    if kind == "BOX":
        # Transform the enclosing local box, not its much larger sphere.
        # Retain full dimensions as half-extents until conventions qualify.
        extents = [
            abs(scale) * sum(abs(rotation[i * 3 + j]) * dims[j] for j in range(3))
            for i in range(3)
        ]
    elif kind in ("CYLINDER", "TUBE"):
        # Independent katsuba-bcd GeomParams::parse reads length, then radius.
        # CYLINDER is a local-Z capsule; TUBE a local-Z cylinder. Enclose both
        # as a capsule with full length as half-length until length convention
        # is qualified. Minkowski segment+sphere remains conservative.
        length, radius = dims
        extents = [
            abs(scale)
            * (
                length * abs(rotation[i * 3 + 2])
                + radius * math.sqrt(sum(rotation[i * 3 + j] ** 2 for j in range(3)))
            )
            for i in range(3)
        ]
    else:
        radius = sum(dims)
        extents = [
            abs(scale)
            * radius
            * math.sqrt(sum(rotation[i * 3 + j] ** 2 for j in range(3)))
            for i in range(3)
        ]
    lower = [center[i] - extents[i] for i in range(3)]
    upper = [center[i] + extents[i] for i in range(3)]
    if not all(map(math.isfinite, lower + upper)):
        raise ValueError("primitive bound overflow")
    return dict(min=lower, max=upper)


def classify_triangle(triangle, maximum_grade):
    if len(triangle) != 3 or any(
        len(v) != 3 or not all(map(math.isfinite, v)) for v in triangle
    ):
        raise ValueError("invalid triangle")
    a, b, c = triangle
    u = [b[i] - a[i] for i in range(3)]
    v = [c[i] - a[i] for i in range(3)]
    normal = [
        u[1] * v[2] - u[2] * v[1],
        u[2] * v[0] - u[0] * v[2],
        u[0] * v[1] - u[1] * v[0],
    ]
    if not all(map(math.isfinite, normal)):
        raise ValueError("overflow")
    if math.sqrt(sum(x * x for x in normal)) < 1e-10:
        role, grade = "degenerate-unresolved", None
    elif abs(normal[2]) < 1e-10:
        role, grade = "vertical-barrier-candidate", None
    else:
        grade = math.hypot(normal[0], normal[1]) / abs(normal[2])
        role = (
            "support-candidate" if grade <= maximum_grade else "steep-barrier-candidate"
        )
    return dict(
        role=role,
        grade=grade,
        bounds=dict(
            min=[min(p[i] for p in triangle) for i in range(3)],
            max=[max(p[i] for p in triangle) for i in range(3)],
        ),
    )


def classify(document, maximum_grade):
    if not math.isfinite(maximum_grade) or maximum_grade < 0:
        raise ValueError("invalid grade")
    if len(document["zones"]) > 16:
        raise ValueError("zone budget")
    zones = []
    total = 0
    for zone in document["zones"]:
        triangles = []
        counts = Counter()
        for mesh in zone["meshes"]:
            for triangle in mesh["worldTriangles"]:
                total += 1
                if total > 250000:
                    raise ValueError("triangle budget")
                entry = classify_triangle(triangle, maximum_grade)
                counts[entry["role"]] += 1
                triangles.append(dict(proxy=mesh["proxy"], points=triangle, **entry))
        unresolved, blockers = [], []
        for p in zone["unsupportedPrimitives"]:
            bounds = primitive_bounds(p)
            if bounds is None:
                unresolved.append(
                    dict(
                        proxy=p["index"],
                        type=p["type"],
                        reason="unbounded-or-unsupported-shape",
                    )
                )
            else:
                blockers.append(
                    dict(
                        proxy=p["index"],
                        name=p["name"],
                        type=p["type"],
                        bounds=bounds,
                        role="conservative-blocker-candidate",
                        roleVerified=False,
                    )
                )
        zones.append(
            dict(
                archive=zone["archive"],
                memberSha256=zone["memberSha256"],
                triangles=triangles,
                counts=dict(counts),
                unresolved=unresolved,
                primitiveBlockers=blockers,
                coverageComplete=False,
                runtimeEnabled=False,
            )
        )
    return dict(
        schemaVersion=1,
        maximumGrade=maximum_grade,
        evidenceGrade="geometric-candidate",
        zones=zones,
        runtimeEnabled=False,
    )


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--maximum-grade", type=float, required=True)
    args = parser.parse_args()
    if args.input.stat().st_size > 32 * 1024 * 1024:
        raise ValueError("input budget")
    raw = args.input.read_bytes()
    result = classify(json.loads(raw), args.maximum_grade)
    result["sourceSha256"] = hashlib.sha256(raw).hexdigest()
    args.output.write_text(json.dumps(result) + "\n", encoding="utf-8")
    print(
        json.dumps(
            [
                dict(
                    archive=z["archive"],
                    counts=z["counts"],
                    unresolved=len(z["unresolved"]),
                )
                for z in result["zones"]
            ]
        )
    )


if __name__ == "__main__":
    main()
