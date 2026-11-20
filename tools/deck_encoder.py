"""
Deck Serialization and Token Codec Engine.
Modernizes the exact zlib + base64 serialization logic from Deimos-Wizard101-main/src/deck_encoder.py.
Allows deterministic serialization and deserialization of normal spells, treasure cards, and item cards.
"""

from __future__ import annotations

import base64
import zlib
from typing import Dict, Optional


class DeckEncoderDecoder:
    """
    A utility class used to encode decks into compact tokens, and tokens back into decks.
    Matches Deimos DeckEncoderDecoder contract exactly.
    """

    def __init__(
        self,
        token: Optional[str] = None,
        deck: Optional[Dict[str, Dict[str, int]]] = None,
    ):
        self.deck: Optional[Dict[str, Dict[str, int]]] = deck
        self.token: Optional[str] = token
        self._DELIMITER1 = "\\+"
        self._DELIMITER2 = "\\,"
        self._DELIMITER3 = "\\;"

    def _serialize_section(self, section: Dict[str, int]) -> str:
        """Serialize a dictionary section into a compact delimited string."""
        if not section:
            return "N"
        return self._DELIMITER2.join(
            f"{key}{self._DELIMITER1}{value}" for key, value in section.items()
        )

    def _deserialize_section(self, section: str) -> Dict[str, int]:
        """Deserialize a section back into dictionary format."""
        if section == "N" or not section:
            return {}
        result: Dict[str, int] = {}
        for item in section.split(self._DELIMITER2):
            if not item:
                continue
            parts = item.split(self._DELIMITER1)
            if len(parts) == 2:
                result[parts[0]] = int(parts[1])
        return result

    def encode(self) -> str:
        """Encode the deck dictionary into a reusable compact token."""
        if not self.deck and not self.token:
            raise ValueError("No deck found.")
        elif self.token and not self.deck:
            return self.token

        for key in ["normal", "tc", "item"]:
            if key not in self.deck:
                raise ValueError(f"Deck is missing {key} section.")

        deck_final = self._DELIMITER3.join(
            [
                self._serialize_section(self.deck["normal"]),
                self._serialize_section(self.deck["tc"]),
                self._serialize_section(self.deck["item"]),
            ]
        )

        compressed = zlib.compress(deck_final.encode("utf-8"))
        self.token = base64.b64encode(compressed).decode("ascii")
        return self.token

    def decode(self) -> Dict[str, Dict[str, int]]:
        """Decode the deck back into its original dictionary format."""
        if not self.token:
            raise ValueError("No token found.")
        try:
            compressed = base64.b64decode(self.token.encode("ascii"))
            deck_final = zlib.decompress(compressed).decode("utf-8")
            sections = deck_final.split(self._DELIMITER3)
            if len(sections) < 3:
                raise ValueError("Malformed sections in token payload.")
        except (zlib.error, ValueError, IndexError) as e:
            raise ValueError("Invalid token format or corrupted data.") from e

        self.deck = {
            "normal": self._deserialize_section(sections[0]),
            "tc": self._deserialize_section(sections[1]),
            "item": self._deserialize_section(sections[2]),
        }
        return self.deck
