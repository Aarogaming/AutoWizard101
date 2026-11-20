"""Run one tool's offline smoke check with its isolated Python interpreter."""

import argparse
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("tool", choices=("companion", "dance", "deimos", "legacy", "moonlight"))
    args = parser.parse_args()
    if args.tool == "companion":
        import os
        os.environ["QT_QPA_PLATFORM"] = "offscreen"
        from PyQt5.QtWidgets import QApplication
        from PyQt5.QtCore import QTimer
        sys.path.insert(0, str(ROOT / ".local/upstream/wizard101-companion"))
        import boss_wiki
        # This smoke check exercises the UI and database, without global hotkeys,
        # overlays, model downloads, game capture or the delayed update checker.
        boss_wiki.KEYBINDS_AVAILABLE = False
        boss_wiki.HUD_AVAILABLE = False
        boss_wiki.OCR_AVAILABLE = False
        boss_wiki.BossWikiApp._startup_update_check = lambda self: None
        app = QApplication([])
        window = boss_wiki.BossWikiApp()
        window.show()
        QTimer.singleShot(1000, app.quit)
        app.exec_()
        destination = ROOT / ".local/artifacts/companion-smoke.png"
        destination.parent.mkdir(parents=True, exist_ok=True)
        if not window.grab().save(str(destination)):
            raise RuntimeError("Could not capture Companion test window")
        print(f"Companion UI constructed; {len(window.boss_names)} boss names loaded")
        window.close()
        app.processEvents()
    elif args.tool == "dance":
        import os
        os.chdir(ROOT / ".local/upstream/Wizard101_DanceBot")
        sys.path.insert(0, str(ROOT / ".local/upstream/Wizard101_DanceBot"))
        sys.argv = ["Wizard101_DanceBot"]  # Upstream logger parses argv on import.
        import main as dance
        for method in ("setup_game", "play_game", "finish_game", "on_press"):
            if not callable(getattr(dance, method)):
                raise RuntimeError(f"Missing DanceBot method: {method}")
        print("DanceBot module and workflow methods imported; no input sent")
    elif args.tool == "deimos":
        from wizwalker import Keycode, HotkeyListener, ModifierKeys, XYZ, Orient
        from wizwalker.client_handler import ClientHandler, Client
        from wizwalker.extensions.wizsprinter.sprinty_combat import SprintyCombat
        from wizwalker.extensions.wizsprinter.wiz_navigator import toZone
        print("Deimos backend client/navigation/combat symbols imported; no client attached")
    elif args.tool == "legacy":
        import wizwalker
        from wizsdk.client import Client
        from wizsdk.battle import Battle
        for method in ("get_quest_xyz", "teleport_to", "get_battle"):
            if not callable(getattr(Client, method)):
                raise RuntimeError(f"Missing SDK method: {method}")
        gaps = [name for name in ("move_lock", "set_yaw") if not hasattr(wizwalker.Client, name)]
        print("Legacy SDK Client/Battle imported; no client registered")
        if gaps:
            print(f"API gaps in legacy movement methods: {', '.join(gaps)}. Movement is not validated.")
    elif args.tool == "moonlight":
        from moonlight.net.decode import PacketReader
        from printrospector import TypeCache
        fixtures = ROOT / ".local/upstream/moonlight/test/fixtures"
        reader = PacketReader(fixtures / "dml/messages")
        decoded = reader.decode_ki_packet((fixtures / "packets/dml_proto1_fake.bin").read_bytes())
        if not decoded:
            raise RuntimeError("Moonlight did not decode its synthetic fixture")
        TypeCache({"Boss": {"hash": 123, "bases": [], "properties": {}}})
        print("Moonlight synthetic packet decoded; wiztype v1-shaped type cache accepted")


if __name__ == "__main__":
    main()
