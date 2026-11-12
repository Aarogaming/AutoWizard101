"""Wizard101 Boss Wiki & Creature Intelligence Resolver.

Ingests 1,851 creature and boss entries from WizWiki Central
(ProjectMaelstrom/ProjectMaelstrom/Scripts/Library/WizWikiAPI-main/wizwiki_companion.json).
Provides instant O(1) resolution for boss health, school, zone, and cheat mechanics.
"""

from __future__ import annotations

from dataclasses import dataclass, field
import json
from pathlib import Path
import re
import sys
from typing import Any, Dict, List, Optional

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

WIKI_JSON_PATH = (
    ROOT
    / "ProjectMaelstrom/ProjectMaelstrom/Scripts/Library/WizWikiAPI-main/wizwiki_companion.json"
)


@dataclass
class BossCheat:
    trigger_text: str
    cheat_type: str
    sub_points: List[str] = field(default_factory=list)


@dataclass
class CreatureWikiProfile:
    name: str
    zone: str
    school: str
    max_hp: int
    creature_type: str  # "Boss", "Minion", "Creature"
    cheats: List[BossCheat] = field(default_factory=list)
    description: str = ""
    source_url: str = ""

    @property
    def has_cheats(self) -> bool:
        return len(self.cheats) > 0

    @property
    def is_boss(self) -> bool:
        return self.creature_type.lower() == "boss"


class BossWikiResolver:
    """Indexed dictionary client for creature wiki stats and cheats."""

    def __init__(self, data_path: Optional[Path] = None) -> None:
        self.data_path = data_path or WIKI_JSON_PATH
        self._index: Dict[str, CreatureWikiProfile] = {}
        self._load()

    def _load(self) -> None:
        if not self.data_path.exists():
            return

        raw_list = json.loads(self.data_path.read_text(encoding="utf-8"))
        for entry in raw_list:
            name = entry.get("Name", "").strip()
            if not name:
                continue

            hp_str = entry.get("Health", "0").replace(",", "").strip()
            try:
                hp_val = int(hp_str)
            except ValueError:
                hp_val = 0

            cheats_list = []
            for ch in entry.get("Cheats", []):
                cheats_list.append(
                    BossCheat(
                        trigger_text=ch.get("text", ""),
                        cheat_type=ch.get("type", "unknown"),
                        sub_points=ch.get("sub_points", []),
                    )
                )

            profile = CreatureWikiProfile(
                name=name,
                zone=entry.get("Zone", ""),
                school=entry.get("School", "Universal"),
                max_hp=hp_val,
                creature_type=entry.get("Type", "Creature"),
                cheats=cheats_list,
                description=entry.get("Description", ""),
                source_url=entry.get("SourceUrl", ""),
            )
            # Index by exact lowercase name and alphanumeric slug
            self._index[name.lower()] = profile
            slug = re.sub(r"[^a-z0-9]", "", name.lower())
            if slug:
                self._index[slug] = profile

    @property
    def count(self) -> int:
        # Return unique profiles count
        return len(set(id(p) for p in self._index.values()))

    def resolve(self, creature_name: str) -> Optional[CreatureWikiProfile]:
        """Resolves creature by name or partial slug."""
        if not creature_name:
            return None
        cleaned = creature_name.strip().lower()
        if cleaned in self._index:
            return self._index[cleaned]

        slug = re.sub(r"[^a-z0-9]", "", cleaned)
        if slug in self._index:
            return self._index[slug]

        # Substring search fallback
        for key, prof in self._index.items():
            if cleaned in key or key in cleaned:
                return prof
        return None
