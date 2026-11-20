"""Native-to-Python smoke fixture. Cannot import/open a game client."""

from pathlib import Path
import asyncio
import os
import sys
import time

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from wizwalker_adapter import main

config = {}


def prepare(request, directory):
    config.update(mode=Path(request["sourceRoot"]).name, directory=directory)


class FakeClient:
    async def loading(self):
        (config["directory"] / "fake-worker.pid").write_text(str(os.getpid()))
        (config["directory"] / "fake-read-started").write_text("fake client only")
        if config["mode"] == "uncooperative":
            while True:
                time.sleep(0.05)
        if config["mode"] == "cooperative":
            await asyncio.sleep(100)
        return True

    async def close(self):
        pass


if __name__ == "__main__":
    raise SystemExit(
        main(
            factory=lambda _: FakeClient(),
            identity=lambda _: True,
            prepare=prepare,
        )
    )
