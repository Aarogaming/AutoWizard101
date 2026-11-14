import sys
from pathlib import Path
import unittest
from unittest.mock import AsyncMock
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from w101_resources import read_resources


class ResourcesTests(unittest.IsolatedAsyncioTestCase):
    def client(self):
        stats = SimpleNamespace(
            **{
                name: AsyncMock(return_value=value)
                for name, value in {
                    "current_hitpoints": 100,
                    "max_hitpoints": 200,
                    "current_mana": 50,
                    "max_mana": 100,
                    "energy_max": 120,
                    "bonus_energy": 20,
                    "potion_charge": 3.5,
                    "potion_max": 4,
                }.items()
            }
        )
        return SimpleNamespace(stats=stats, current_energy=AsyncMock(return_value=80))

    async def test_unqualified_never_reads(self):
        client = self.client()
        self.assertEqual("unqualified", (await read_resources(client))["status"])
        client.stats.current_hitpoints.assert_not_called()

    async def test_pairs_and_fractional_potions(self):
        result = await read_resources(self.client(), qualified=True)
        self.assertEqual(4, len(result["values"]))
        self.assertEqual(140, result["values"]["energy"]["maximum"])
        self.assertEqual({"current": 3.5, "maximum": 4}, result["values"]["potions"])

    async def test_torn_invalid_and_missing_are_unknown(self):
        client = self.client()
        client.stats.current_hitpoints.side_effect = [100, 99]
        client.stats.max_mana.return_value = 0
        client.current_energy.side_effect = ValueError("not visible")
        result = await read_resources(client, qualified=True)
        self.assertEqual(["potions"], list(result["values"]))
