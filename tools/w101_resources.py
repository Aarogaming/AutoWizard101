"""Bounded read-only wizwalker resource projection; owns no hooks or processes.

Caller must qualify and own the stats reader for its exact client build first.
Archived offsets are not authorization to activate a new StatsHook.
"""

import asyncio
import math

METHODS = {
    "health": ("current_hitpoints", "max_hitpoints"),
    "mana": ("current_mana", "max_mana"),
    "energy": ("current_energy", "energy_max"),
    "potions": ("potion_charge", "potion_max"),
}


async def read_resources(client, *, qualified=False, timeout=0.5):
    """Reuse imported methods sequentially; unavailable pairs remain absent.

    Current energy comes from the UI, as in imported wizwalker. Energy maximum
    semantics must be qualified by the caller, including equipment bonuses.
    Each pair is read twice to reject transitions/torn current-max snapshots.
    """
    if not qualified:
        return {"status": "unqualified", "values": {}}
    if not 0 < timeout <= 2:
        raise ValueError("Resource budget must be in (0, 2] seconds")
    values = {}

    async def maximum_value(resource, method):
        base = await getattr(client.stats, method)()
        return (
            base + await client.stats.bonus_energy() if resource == "energy" else base
        )

    async def collect():
        for resource, (current, maximum) in METHODS.items():
            try:
                source = client if resource == "energy" else client.stats
                first = (
                    await getattr(source, current)(),
                    await maximum_value(resource, maximum),
                )
                second = (
                    await getattr(source, current)(),
                    await maximum_value(resource, maximum),
                )
                if first != second:
                    continue
                a, b = first
                if any(
                    type(v) not in (int, float) or not math.isfinite(v) for v in first
                ):
                    continue
                if not 0 <= a <= b <= 2147483647 or b <= 0:
                    continue
                if resource != "potions" and any(int(v) != v for v in first):
                    continue
                values[resource] = {"current": a, "maximum": b}
            except (AttributeError, ValueError, OSError):
                continue

    try:
        await asyncio.wait_for(collect(), timeout)
    except asyncio.TimeoutError:
        # Never label partial values as a complete, fresh batch.
        return {"status": "timeout", "values": {}}
    return {"status": "observed", "values": values}
