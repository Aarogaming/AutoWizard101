"""Wizard101 UI Element Path Catalog.

Derived from Deimos-Wizard101 and verified memory UI tree structures.
Maps canonical game UI interactions to their exact hierarchical window names
for path-aware observation and clicking.
"""

from __future__ import annotations

from typing import Dict, List, Tuple


class UIPaths:
    # -------------------------------------------------------------------------
    # Dialogue & NPC Interaction
    # -------------------------------------------------------------------------
    ADVANCE_DIALOG = ["WorldView", "wndDialogMain", "btnRight"]
    DECLINE_QUEST = ["WorldView", "wndDialogMain", "btnLeft"]
    DIALOG_TEXT = ["WorldView", "wndDialogMain", "txtArea", "txtMessage"]
    NPC_RANGE_TITLE = [
        "WorldView",
        "NPCRangeWin",
        "wndTitleBackground",
        "NPCRangeTxtTitle",
    ]
    NPC_RANGE_POPUP = ["WorldView", "NPCRangeWin"]

    # -------------------------------------------------------------------------
    # Potion Vendor & Potion Bottle Usage
    # -------------------------------------------------------------------------
    POTION_SHOP_BASE = ["WorldView", "main"]
    POTION_BUY = ["WorldView", "main", "buyAction"]
    POTION_FILL_ALL = ["WorldView", "main", "fillallpotions"]
    POTION_EXIT = ["WorldView", "main", "exit"]
    POTION_USAGE_HUD = ["WorldView", "windowHUD", "btnPotions"]

    # -------------------------------------------------------------------------
    # Spiral Door
    # -------------------------------------------------------------------------
    SPIRAL_DOOR_CYCLE_RIGHT = [
        "WorldView",
        "",
        "messageBoxBG",
        "ControlSprite",
        "optionWindow",
        "rightButton",
    ]
    SPIRAL_DOOR_CYCLE_LEFT = [
        "WorldView",
        "",
        "messageBoxBG",
        "ControlSprite",
        "optionWindow",
        "leftButton",
    ]
    SPIRAL_DOOR_TELEPORT = [
        "WorldView",
        "",
        "messageBoxBG",
        "ControlSprite",
        "teleportButton",
    ]
    SPIRAL_DOOR_EXIT = [
        "WorldView",
        "",
        "messageBoxBG",
        "ControlSprite",
        "cancelButton",
    ]

    # -------------------------------------------------------------------------
    # Team Up & Dungeons
    # -------------------------------------------------------------------------
    TEAM_UP_BUTTON = ["WorldView", "NPCRangeWin", "imgBackground", "TeamUpButton"]
    TEAM_UP_CONFIRM = [
        "WorldView",
        "TeamUpConfirmationWindow",
        "TeamUpConfirmationBackground",
        "TeamUpButton",
    ]
    DUNGEON_WARNING_ENTER = [
        "MessageBoxModalWindow",
        "messageBoxBG",
        "messageBoxLayout",
        "AdjustmentWindow",
        "Layout",
        "centerButton",
    ]
    EXIT_DUNGEON_CONFIRM = [
        "MessageBoxModalWindow",
        "messageBoxBG",
        "messageBoxLayout",
        "AdjustmentWindow",
        "Layout",
        "centerButton",
    ]

    # -------------------------------------------------------------------------
    # HUD & Navigation
    # -------------------------------------------------------------------------
    QUEST_GOAL_NAME = [
        "WorldView",
        "windowHUD",
        "QuestHelperHud",
        "ElementWindow",
        "",
        "txtGoalName",
    ]
    RESUME_INSTANCE = [
        "WorldView",
        "windowHUD",
        "compassAndTeleporterButtons",
        "ResumeInstanceButton",
    ]
    RECALL_BUTTON = [
        "WorldView",
        "windowHUD",
        "compassAndTeleporterButtons",
        "RecallButton",
    ]

    # -------------------------------------------------------------------------
    # Shop Exits
    # -------------------------------------------------------------------------
    EXIT_EQUIPMENT_SHOP = ["WorldView", "shopGUI", "buyWindow", "exit"]
    EXIT_RECIPE_SHOP = ["WorldView", "", "Exit"]
    EXIT_SNACK_SHOP = ["WorldView", "ShoppingPetSnackWindow", "buyWindow", "exit"]
    EXIT_REAGENT_SHOP = ["WorldView", "ShoppingReagentWindow", "buyWindow", "exit"]
    EXIT_TC_VENDOR = ["WorldView", "main", "exit"]

    # -------------------------------------------------------------------------
    # Pet Game Tracks & Post-Game
    # -------------------------------------------------------------------------
    PET_FEED_WINDOW = ["WorldView", "PetGameTracks"]
    PET_FEED_CANCEL = ["WorldView", "PetGameTracks", "btnBack"]
    DANCE_GAME_TRACK_0 = [
        "WorldView",
        "PetGameTracks",
        "wndBkgTracks",
        "wndTracks",
        "btnTrack0",
    ]
    PLAY_DANCE_GAME = ["WorldView", "PetGameTracks", "btnNext"]
    SKIP_PET_GAME = ["WorldView", "PetGameTracks", "SkipGameButton"]
    WON_PET_GAME_REWARDS = ["WorldView", "PetGameSplash", "", "PetGameRewards"]
    WON_PET_GAME_CONTINUE = [
        "WorldView",
        "PetGameSplash",
        "",
        "PetGameRewards",
        "btnNext",
    ]
    WON_PET_GAME_PLAY_AGAIN = [
        "WorldView",
        "PetGameSplash",
        "",
        "PetGameRewards",
        "btnPlayAgain",
    ]
    ENERGY_AMOUNT_TEXT = [
        "WorldView",
        "DeckConfiguration",
        "",
        "ControlSprite",
        "wndEnergyFrame",
        "Layout",
        "txtEnergy",
    ]


CATALOG: Dict[str, List[str]] = {
    "dialog.advance": UIPaths.ADVANCE_DIALOG,
    "dialog.decline": UIPaths.DECLINE_QUEST,
    "dialog.text": UIPaths.DIALOG_TEXT,
    "potion.buy": UIPaths.POTION_BUY,
    "potion.fill_all": UIPaths.POTION_FILL_ALL,
    "potion.exit": UIPaths.POTION_EXIT,
    "potion.use_bottle": UIPaths.POTION_USAGE_HUD,
    "spiral_door.teleport": UIPaths.SPIRAL_DOOR_TELEPORT,
    "spiral_door.next": UIPaths.SPIRAL_DOOR_CYCLE_RIGHT,
    "spiral_door.prev": UIPaths.SPIRAL_DOOR_CYCLE_LEFT,
    "team_up.start": UIPaths.TEAM_UP_BUTTON,
    "team_up.confirm": UIPaths.TEAM_UP_CONFIRM,
    "dungeon.enter": UIPaths.DUNGEON_WARNING_ENTER,
    "dungeon.exit": UIPaths.EXIT_DUNGEON_CONFIRM,
    "quest.goal_text": UIPaths.QUEST_GOAL_NAME,
    "pet.dance_sigil_play": UIPaths.PLAY_DANCE_GAME,
    "pet.rewards_continue": UIPaths.WON_PET_GAME_CONTINUE,
    "pet.play_again": UIPaths.WON_PET_GAME_PLAY_AGAIN,
    "shop.exit_equipment": UIPaths.EXIT_EQUIPMENT_SHOP,
    "shop.exit_tc": UIPaths.EXIT_TC_VENDOR,
}


def get_ui_path(key: str) -> List[str]:
    """Retrieves canonical UI element path by semantic key."""
    if key not in CATALOG:
        raise KeyError(
            f"Unknown UI element key: '{key}'. Available: {list(CATALOG.keys())}"
        )
    return list(CATALOG[key])
