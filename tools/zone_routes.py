"""Unqualified Wizard101 route sketches retained for planning reference only.

The coordinates below have no measured provenance. They are not real-world
waypoints, verified corridors or an executable travel library. The human route
recording supplies visual landmarks, not these coordinates. Runnable conversion
fails closed; explicit preview export uses a separate schema without macro nodes.
"""

from __future__ import annotations

from dataclasses import dataclass, field
import json
from pathlib import Path
from typing import Any, Dict, List, NoReturn


@dataclass(frozen=True)
class RouteWaypoint:
    x: float
    y: float
    z: float
    name: str
    tolerance: float = 40.0


@dataclass
class ZoneRoute:
    route_id: str
    title: str
    from_zone: str
    to_zone: str
    waypoints: List[RouteWaypoint] = field(default_factory=list)
    tags: List[str] = field(default_factory=list)

    def to_macro_nodes(self) -> NoReturn:
        """Reject unmeasured coordinates instead of manufacturing navigation."""
        raise ValueError(
            "Unqualified example coordinates cannot become navigation actions; "
            "collect target-bound route evidence and use the qualified game-pack path."
        )

    def to_macro_graph(self) -> NoReturn:
        """Retained compatibility entry point, deliberately not an export bypass."""
        self.to_macro_nodes()

    def to_preview(self) -> Dict[str, Any]:
        """Return non-executable planning data, never a macro graph."""
        points = [
            {
                "example_x": wp.x,
                "example_y": wp.y,
                "example_z": wp.z,
                "name": wp.name,
            }
            for wp in self.waypoints
        ]
        return {
            "schema": "maelstrom.route-preview.v1",
            "executable": False,
            "qualification": "unqualified-example-coordinates",
            "coordinate_source": None,
            "live_qualified": False,
            "route_id": self.route_id,
            "title": self.title,
            "from_zone": self.from_zone,
            "to_zone": self.to_zone,
            "tags": list(self.tags),
            "example_waypoints": points,
            "warning": "Do not use these coordinates for movement or arrival checks.",
        }


# ---------------------------------------------------------------------------
# Unqualified route sketches: coordinates and corridor geometry are unmeasured.
# ---------------------------------------------------------------------------

COMMONS_TO_PET_PAVILION = ZoneRoute(
    route_id="commons-to-pet-pavilion",
    title="The Commons to Pet Pavilion",
    from_zone="The Commons",
    to_zone="Pet Pavilion",
    tags=["hub", "pet_training", "dance_game"],
    waypoints=[
        RouteWaypoint(0.0, 0.0, 0.0, "Commons World Gate Center", 35.0),
        RouteWaypoint(145.0, -420.0, 0.0, "Pet Pavilion Bridge Approach", 40.0),
        RouteWaypoint(285.0, -840.0, 10.0, "Pet Pavilion Canal Bridge", 40.0),
        RouteWaypoint(390.0, -1260.0, 15.0, "Pavilion Tunnel Gate Archway", 45.0),
        RouteWaypoint(430.0, -1620.0, 15.0, "Pavilion Inner Courtyard", 40.0),
        RouteWaypoint(515.0, -1960.0, 20.0, "Pet Dance Game Sigil", 35.0),
    ],
)

PET_PAVILION_TO_COMMONS = ZoneRoute(
    route_id="pet-pavilion-to-commons",
    title="Pet Pavilion to The Commons",
    from_zone="Pet Pavilion",
    to_zone="The Commons",
    tags=["hub", "return"],
    waypoints=[
        RouteWaypoint(515.0, -1960.0, 20.0, "Pet Dance Game Sigil", 35.0),
        RouteWaypoint(430.0, -1620.0, 15.0, "Pavilion Inner Courtyard", 40.0),
        RouteWaypoint(390.0, -1260.0, 15.0, "Pavilion Tunnel Gate Archway", 45.0),
        RouteWaypoint(285.0, -840.0, 10.0, "Pet Pavilion Canal Bridge", 40.0),
        RouteWaypoint(145.0, -420.0, 0.0, "Pet Pavilion Bridge Approach", 40.0),
        RouteWaypoint(0.0, 0.0, 0.0, "Commons World Gate Center", 35.0),
    ],
)

COMMONS_TO_GOLEM_COURT = ZoneRoute(
    route_id="commons-to-golem-court",
    title="The Commons to Golem Court",
    from_zone="The Commons",
    to_zone="Golem Court",
    tags=["hub", "golem_tower", "practice"],
    waypoints=[
        RouteWaypoint(0.0, 0.0, 0.0, "Commons World Gate Center", 35.0),
        RouteWaypoint(-380.0, -210.0, 0.0, "Golem Court Tunnel Ramp", 40.0),
        RouteWaypoint(-750.0, -320.0, 10.0, "Golem Court Archway", 40.0),
        RouteWaypoint(-1120.0, -450.0, 15.0, "Golem Tower Sigil Perimeter", 35.0),
    ],
)

COMMONS_TO_OLDE_TOWN = ZoneRoute(
    route_id="commons-to-olde-town",
    title="The Commons to Olde Town",
    from_zone="The Commons",
    to_zone="Olde Town",
    tags=["hub", "avenues", "travel"],
    waypoints=[
        RouteWaypoint(0.0, 0.0, 0.0, "Commons World Gate Center", 35.0),
        RouteWaypoint(20.0, 480.0, 0.0, "Rainbow Bridge South Pier", 40.0),
        RouteWaypoint(35.0, 950.0, 12.0, "Rainbow Bridge Center Arch", 40.0),
        RouteWaypoint(45.0, 1420.0, 15.0, "Olde Town Tunnel Portal", 45.0),
        RouteWaypoint(50.0, 1780.0, 20.0, "Olde Town Central Plaza", 40.0),
    ],
)

OLDE_TOWN_TO_SHOPPING_DISTRICT = ZoneRoute(
    route_id="olde-town-to-shopping-district",
    title="Olde Town to Shopping District",
    from_zone="Olde Town",
    to_zone="Shopping District",
    tags=["hub", "shops", "bazaar"],
    waypoints=[
        RouteWaypoint(50.0, 1780.0, 20.0, "Olde Town Central Plaza", 40.0),
        RouteWaypoint(-320.0, 1850.0, 20.0, "Shopping District Archway Approach", 40.0),
        RouteWaypoint(
            -680.0, 1920.0, 25.0, "Shopping District Portal Transition", 45.0
        ),
        RouteWaypoint(-980.0, 1980.0, 25.0, "Shopping District Main Street", 40.0),
    ],
)

STANDARD_ROUTES: Dict[str, ZoneRoute] = {
    COMMONS_TO_PET_PAVILION.route_id: COMMONS_TO_PET_PAVILION,
    PET_PAVILION_TO_COMMONS.route_id: PET_PAVILION_TO_COMMONS,
    COMMONS_TO_GOLEM_COURT.route_id: COMMONS_TO_GOLEM_COURT,
    COMMONS_TO_OLDE_TOWN.route_id: COMMONS_TO_OLDE_TOWN,
    OLDE_TOWN_TO_SHOPPING_DISTRICT.route_id: OLDE_TOWN_TO_SHOPPING_DISTRICT,
}


def export_standard_routes(dest_dir: Path, *, preview_only: bool = False) -> List[Path]:
    """Export planning previews only after an explicit opt-in; never macro files."""
    if preview_only is not True:
        raise ValueError(
            "These routes are unqualified; only preview_only=True is supported."
        )
    dest_dir.mkdir(parents=True, exist_ok=True)
    exported = []
    for r_id, route in STANDARD_ROUTES.items():
        out_file = dest_dir / f"{r_id}.route-preview.json"
        # Do not overwrite evidence, user edits or an earlier preview silently.
        with out_file.open("x", encoding="utf-8") as output:
            output.write(json.dumps(route.to_preview(), indent=2))
        exported.append(out_file)
    return exported


if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--preview-only", action="store_true", required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    paths = export_standard_routes(args.output, preview_only=args.preview_only)
    print(f"Exported {len(paths)} non-executable route sketches to {args.output}")
