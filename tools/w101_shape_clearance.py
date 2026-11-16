"""Pure conservative swept-body/affine-box check; no game shape qualification.

Callers provide explicitly interpreted local half-extents. A cylinder may be
enclosed in such a box only after its axis/length conventions are established.
False means disjoint under the supplied assumptions, not a game clearance grant.
"""

import math

from w101_connector import intersects


def overlaps(start, end, center, rotation, scale, half_extents, radius, height):
    vectors = (start, end, center, half_extents)
    if any(len(v) != 3 for v in vectors) or len(rotation) != 9:
        raise ValueError("invalid shape")
    if not all(
        math.isfinite(x)
        for v in (*vectors, rotation, [scale, radius, height])
        for x in v
    ):
        raise ValueError("nonfinite shape")
    if scale == 0 or min(*half_extents, radius, height) < 0:
        raise ValueError("invalid extent")
    a, b, c, d, e, f, g, h, i = [x * scale for x in rotation]
    determinant = a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g)
    if not math.isfinite(determinant) or abs(determinant) < 1e-10:
        raise ValueError("singular transform")
    inverse = [
        x / determinant
        for x in (
            e * i - f * h,
            c * h - b * i,
            b * f - c * e,
            f * g - d * i,
            a * i - c * g,
            c * d - a * f,
            d * h - e * g,
            b * g - a * h,
            a * e - b * d,
        )
    ]
    if max(abs(x) for x in inverse) > 1e8:
        raise ValueError("ill-conditioned transform")
    body = [radius, radius, height / 2]
    expansion = [
        sum(abs(inverse[r * 3 + j]) * body[j] for j in range(3)) for r in range(3)
    ]

    def local(p):
        delta = [p[j] - center[j] + (height / 2 if j == 2 else 0) for j in range(3)]
        return [sum(inverse[r * 3 + j] * delta[j] for j in range(3)) for r in range(3)]

    extents = [half_extents[j] + expansion[j] + 1e-7 for j in range(3)]
    return intersects(local(start), local(end), [-x for x in extents], extents, 0)
