"""
Automatus World & Zone Topology Ingestion Engine.
Directly ingests the full 55-zone hierarchy, file-name mappings, and point routes
from Automatus-v2-master (Automatus-v2-master.zip).
"""

from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, List, Optional, Tuple


@dataclass
class ZoneNode:
    name: str
    code_name: str
    children: List[ZoneNode] = field(default_factory=list)


class AutomatusTopologyResolver:
    """
    Ingests and provides query methods over the complete WizardCity zone tree
    and zone filename mappings from Automatus-v2.
    """

    FILE_NAMES: Dict[str, str] = {
        "WizardCity/WC_Hub": "COMMONS",
        "WizardCity/WC_Ravenwood": "RAVENWOOD",
        "WizardCity/Interiors/WC_SchoolFire": "SCHOOL_OF_FIRE",
        "WizardCity/Interiors/WC_TowerFire": "FIRE_TOWER",
        "WizardCity/Interiors/WC_SchoolFrost": "SCHOOL_OF_ICE",
        "WizardCity/Interiors/WC_TowerIce": "ICE_TOWER",
        "WizardCity/Interiors/WC_SchoolStorm": "SCHOOL_OF_STORM",
        "WizardCity/Interiors/WC_TowerStorm": "STORM_TOWER",
        "WizardCity/Interiors/WC_SchoolLife": "SCHOOL_OF_LIFE",
        "WizardCity/Interiors/WC_TowerLife": "LIFE_TOWER",
        "WizardCity/Interiors/WC_SchoolMyth": "SCHOOL_OF_MYTH",
        "WizardCity/Interiors/WC_TowerMyth": "MYTH_TOWER",
        "WizardCity/WC_Ravenwood_Teleporter": "WORLD_TREE",
        "WizardCity/WC_NightSide": "NIGHTSIDE",
        "WizardCity/Interiors/WC_SchoolDeath": "SCHOOL_OF_DEATH",
        "WizardCity/Interiors/WC_TowerDeath": "DEATH_TOWER",
        "G14_DM/DM_Z01_CastleDarkmoor": "DARKMOOR",
        "WizardCity/WC_Streets/WC_Unicorn": "UNICORN_WAY",
        "WizardCity/WC_Duel_Arena": "ARENA",
        "WizardCity/WC_Streets/Interiors/WC_Unicorn_HedgeMaze": "HEDGE_MAZE",
        "WizardCity/Interiors/WC_Library": "LIBRARY",
        "WizardCity/Interiors/WC_Castle_Tours": "CASTLE_TOURS",
        "WizardCity/WC_Streets/Interiors/WC_PET_Park": "PET_PAVILION",
        "WizardCity/Interiors/WC_Hatchery": "HATCHERY",
        "WizardCity/Interiors/WC_Headmistress_House": "HEADMASTERS_HOUSE",
        "WizardCity/Interiors/WC_Headmaster_Tower": "HEADMASTERS_TOWER",
        "WizardCity/WC_Golem_Tower": "GOLEM_COURT",
        "WizardCity/WC_Streets/WC_Drains/Z00_Drains_HUB": "DRAINS",
        "WizardCity/WC_Streets/WC_Catacombs": "CATACOMBS",
        "WizardCity/WC_Streets/WC_Catacombs_B": "LOWER_CATACOMBS",
        "WizardCity/WC_Shop_Area": "SHOPPING_DISTRICT",
        "WizardCity/Interiors/WC_Shop_Makeup": "MIRROR_SHOP",
        "WizardCity/Interiors/WC_ShopDye": "DYE_SHOP",
        "WizardCity/Interiors/WC_Shop_Hat": "HAT_SHOP",
        "WizardCity/Interiors/WC_Shop_Pets": "PET_SHOP",
        "WizardCity/Interiors/WC_Shop_Boots": "BOOT_SHOP",
        "WizardCity/Interiors/WC_Shop_Decks": "DECK_SHOP",
        "WizardCity/Interiors/WC_Shop_House": "HOUSING_SHOP",
        "WizardCity/Interiors/WC_Shop_Rings": "RING_SHOP",
        "WizardCity/Interiors/WC_Shop_Athames": "ATHAME_SHOP",
        "WizardCity/Interiors/WC_Shop_Jeweler": "JEWEL_SHOP",
        "WizardCity/Interiors/WC_Shop_Robes": "ROBE_SHOP",
        "WizardCity/Interiors/WC_Shop_Wand": "WAND_SHOP",
        "WizardCity/Interiors/WC_Shop_Amulets": "AMULET_SHOP",
        "WizardCity/WC_Streets/WC_Colossus": "COLOSSUS_BOULEVARD",
        "WizardCity/WC_Streets/Interiors/WC_Colossus_H1": "MILDREDS_HOUSE",
        "WizardCity/WC_Streets/WC_OldeTown": "OLDE_TOWN",
        "WizardCity/WC_Streets/Interiors/WC_OldeTown_AuctionHouse": "BAZAAR",
        "WizardCity/WC_Streets/WC_Cyclops": "CYCLOPS_LANE",
        "Aquila/AQ_Z00_Hub": "AQUILA",
        "WizardCity/WC_Streets/WC_Triton": "TRITON_AVENUE",
        "WizardCity/WC_Streets/WC_HauntedCave": "HAUNTED_CAVE",
        "WizardCity/WC_Streets/WC_Firecat": "FIRECAT_ALLEY",
        "WizardCity/WC_Streets/Interiors/WC_Firecat_H2": "GRETTAS_HOUSE",
        "WizardCity/WC_Streets/WC_DarkCave": "DARK_CAVE",
    }

    # Verified coordinates from Automatus Unicorn Way map
    UNICORN_WAY_COORDINATES: List[Tuple[int, int, int]] = [
        (27, 27, -27),
        (1097, 346, 3),
        (3484, 2323, -28),
        (5099, 3725, 3),
        (6088, 5027, -28),
        (0, 0, 1),  # Keypress interface (1: W forward press)
    ]

    def __init__(self):
        self._reverse_map = {v: k for k, v in self.FILE_NAMES.items()}
        self._zone_adjacency: Dict[str, List[str]] = self._build_adjacency_graph()

    def get_zone_path(self, codename: str) -> Optional[str]:
        """Resolves friendly zone codename (e.g. COMMONS, BAZAAR) to client internal path."""
        return self._reverse_map.get(codename.upper())

    def get_codename(self, internal_path: str) -> Optional[str]:
        """Resolves client internal path to friendly zone codename."""
        return self.FILE_NAMES.get(internal_path)

    def _build_adjacency_graph(self) -> Dict[str, List[str]]:
        """Constructs an undirected zone adjacency graph based on Automatus hierarchy."""
        graph: Dict[str, List[str]] = {}

        def add_edge(u: str, v: str):
            graph.setdefault(u, []).append(v)
            graph.setdefault(v, []).append(u)

        # Core Commons hub connections
        add_edge("COMMONS", "RAVENWOOD")
        add_edge("COMMONS", "UNICORN_WAY")
        add_edge("COMMONS", "SHOPPING_DISTRICT")
        add_edge("COMMONS", "GOLEM_COURT")
        add_edge("COMMONS", "PET_PAVILION")
        add_edge("COMMONS", "LIBRARY")
        add_edge("COMMONS", "CASTLE_TOURS")
        add_edge("COMMONS", "HEADMASTERS_HOUSE")
        add_edge("COMMONS", "HEADMASTERS_TOWER")
        add_edge("COMMONS", "NIGHTSIDE")

        # Ravenwood Schools
        for school in [
            "FIRE",
            "ICE",
            "STORM",
            "LIFE",
            "MYTH",
        ]:
            add_edge("RAVENWOOD", f"SCHOOL_OF_{school}")
            add_edge("RAVENWOOD", f"{school}_TOWER")
        add_edge("RAVENWOOD", "WORLD_TREE")

        # Nightside
        add_edge("NIGHTSIDE", "SCHOOL_OF_DEATH")
        add_edge("NIGHTSIDE", "DEATH_TOWER")
        add_edge("NIGHTSIDE", "DARKMOOR")

        # Unicorn Way
        add_edge("UNICORN_WAY", "ARENA")
        add_edge("UNICORN_WAY", "HEDGE_MAZE")

        # Pet Pavilion
        add_edge("PET_PAVILION", "HATCHERY")

        # Golem Court & Drains
        add_edge("GOLEM_COURT", "DRAINS")
        add_edge("DRAINS", "CATACOMBS")
        add_edge("CATACOMBS", "LOWER_CATACOMBS")

        # Shopping District & Olde Town
        add_edge("SHOPPING_DISTRICT", "COLOSSUS_BOULEVARD")
        add_edge("SHOPPING_DISTRICT", "OLDE_TOWN")
        add_edge("COLOSSUS_BOULEVARD", "MILDREDS_HOUSE")

        # Shopping District interiors
        for shop in [
            "MIRROR_SHOP",
            "DYE_SHOP",
            "HAT_SHOP",
            "PET_SHOP",
            "BOOT_SHOP",
            "DECK_SHOP",
            "HOUSING_SHOP",
            "RING_SHOP",
            "ATHAME_SHOP",
            "JEWEL_SHOP",
            "ROBE_SHOP",
            "WAND_SHOP",
            "AMULET_SHOP",
        ]:
            add_edge("SHOPPING_DISTRICT", shop)

        # Olde Town Hub
        add_edge("OLDE_TOWN", "BAZAAR")
        add_edge("OLDE_TOWN", "CYCLOPS_LANE")
        add_edge("OLDE_TOWN", "TRITON_AVENUE")
        add_edge("OLDE_TOWN", "FIRECAT_ALLEY")

        # Olde Town Streets
        add_edge("CYCLOPS_LANE", "DARK_CAVE")
        add_edge("CYCLOPS_LANE", "AQUILA")
        add_edge("TRITON_AVENUE", "HAUNTED_CAVE")
        add_edge("FIRECAT_ALLEY", "GRETTAS_HOUSE")

        return graph

    def find_shortest_zone_path(self, start: str, goal: str) -> List[str]:
        """BFS shortest path through WizardCity zone network."""
        start = start.upper()
        goal = goal.upper()
        if start == goal:
            return [start]
        if start not in self._zone_adjacency or goal not in self._zone_adjacency:
            return []

        visited = {start}
        queue = [[start]]

        while queue:
            path = queue.pop(0)
            node = path[-1]
            for neighbor in self._zone_adjacency.get(node, []):
                if neighbor == goal:
                    return path + [neighbor]
                if neighbor not in visited:
                    visited.add(neighbor)
                    queue.append(path + [neighbor])
        return []
