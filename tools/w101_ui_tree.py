"""Optional, bounded W101 UI observation under Maelstrom's existing owner.

The reviewed root-window hook is used; explicit pose opt-in adds a serialized
player hook only after root restoration. No input, movement, network or
installed-file writes. Internal node names are kept; chat/account branches and
arbitrary text are excluded. Candidate states are observations, never actions.
"""

from __future__ import annotations

import argparse
import asyncio
from concurrent.futures import ThreadPoolExecutor
import hashlib
import html
import importlib.util
import json
from pathlib import Path
import re
import struct
import sys
import time
import uuid

VERSION = "w101-ui-tree-v1"
COMMON_SHA256 = "70f38c0f875bce43d78f2b94e7933cf27a9051eaadad2a73f7743a3dd23f2d19"
COMBAT_READER_SHA256 = (
    "a405acdadd9b983a512e853155d832e6a1126439dcd783cffe393992b2b97c74"
)
DUEL_READER_SHA256 = "632a826d1aee0757a01dbe442d61b3045eecc0f274d5a94650d6f0048c060165"
POSE_READER_SHA256 = "d6463168ad2342747655d06d265a12c67cf2d3bbe9bbf960e75b9e833463e9af"
NAVIGATION_READERS = {
    "w101_zone_reader": "1e7ddcfae9bd825cfc864b86dfdab9af5a89501eb4c4de577c15883fd2712a42",
    "w101_moving_pose": "785c4a62b9934a0c00644d8788fc592fdfee4e3ac63cc1baca2f946a61f1fbe9",
}
IDENTITY_READER_SHA256 = (
    "6c72c951986eb84713ba3e6b78914e5038f24469ca798a881d7bb9e3cddc2bd8"
)
MAX_NODES = 384
MAX_DEPTH = 16
MAX_TRACE_BYTES = 4 * 1024 * 1024
PRIVATE_BRANCH = re.compile(r"chat|whisper|friend|password|login|account|guild", re.I)
STATES = ("interactionPrompt", "dialogue", "menu", "nativeIdle")
STRUCTURAL_LEAVES = {
    "CompassWindow",
    "windowHUD",
    "BallonLayer",
    "WizardPatchingWindow",
    "SettingPage",
    "SettingsPage",
    "wndDialogMain",
    "NPCServicesWin",
    "shopGUI",
    "PayToPlay",
}
# Exact game-owned paths from the archived Deimos paths.py. Only these fields
# become readable with the separate quest-context opt-in; never dump HUD text.
QUEST_OBJECTIVE = (
    "Root",
    "WorldView",
    "windowHUD",
    "QuestHelperHud",
    "ElementWindow",
    "",
    "txtGoalName",
)
QUEST_DIALOGUE = ("Root", "WorldView", "wndDialogMain", "txtArea", "txtMessage")
QUEST_ADVANCE = ("Root", "WorldView", "wndDialogMain", "btnRight")
QUEST_PATHS = (QUEST_OBJECTIVE, QUEST_DIALOGUE, QUEST_ADVANCE)
INTERACTION_TITLE = (
    "Root",
    "WorldView",
    "NPCRangeWin",
    "wndTitleBackground",
    "NPCRangeTxtTitle",
)
# Structural candidates only; these names do not identify a castable spell or
# prove a battle outcome. No combat text/participant/spell memory is read.
COMBAT_PLANNING = ("Root", "WorldView", "PlanningPhase")
COMBAT_HAND = COMBAT_PLANNING + (
    "Alignment",
    "PlanningPhaseSubWindow",
    "SpellSelection",
    "Hand",
)
COMBAT_BUTTONS = {"Focus", "Draw", "Flee", "DefeatedPassButton", "DefeatedFleeButton"}
MAX_COMBAT_CONTROLS = 64
# Historical Deimos PetGameTracks paths are discovery hints, not live qualification.
DANCE_ENTRY_ROOT = ("Root", "WorldView", "PetGameTracks")
DANCE_ENTRY_TEXT = {
    DANCE_ENTRY_ROOT + ("wndBkgEnergy", "txtEnergyCost"),
    DANCE_ENTRY_ROOT + ("wndBkgEnergy", "txtYourEnergy"),
}
MAX_DANCE_ENTRY_CONTROLS = 96
# Reviewed Deimos-Wizard101/wizwalker development definitions, retrieved 2026-11-10.
# Limited to this independently scanned executable; never infer new registers/offsets.
CURRENT_IMAGE_SHA256 = (
    "544ab726ef7b920a5ccb67aa3d06b245db7e0012317d2fe7856d58c17c31cf7e"
)
CURRENT_ROOT_ENTRY = bytes.fromhex("49 8b 8d d8 00 00 00")
ARCHIVED_LAYOUT_IMAGE_SHA256 = (
    "d410a2463e7ff0e221d6d11ca10fa53de265abd17cacfba211f9eeca8ec8511a"
)
REVIEWED_BUILD_20261118_SHA256 = (
    "818dbe66037bcb6913a87ab4c2b5f77b0c4fad8d2d10bed921c9fe36ef7d1114"
)


def profile_for(image):
    if image in (CURRENT_IMAGE_SHA256, REVIEWED_BUILD_20261118_SHA256):
        return "r13-current", CURRENT_ROOT_ENTRY, 4100
    if image == ARCHIVED_LAYOUT_IMAGE_SHA256:
        return "r15-archived", bytes.fromhex("49 8b 8f d8 00 00 00"), 3900
    raise UiCompatibilityError("client-build-unreviewed")


CURRENT_SCRATCH_PATTERN = (
    rb"\x48\x89\x5C\x24.\x48\x89\x74\x24.\x48\x89\x7C\x24."
    rb"\x55\x41\x54\x41\x55\x41\x56\x41\x57"
    rb"\x48\x8D\xAC\x24....\x48\x81\xEC...."
    rb"\x48\x8B\x05....\x48\x33\xC4\x48\x89\x85...."
    rb"\x4C\x8B\xF1.......\x80......\x0F\x84...."
)


def current_root_hook(base):
    """Narrow source reuse; retain the existing transport, not all upstream hooks."""

    class ReviewedRootHook(base):
        async def bytecode_generator(self, packed_exports):
            return (
                b"\x50"
                + bytes.fromhex("49 8b 85 d8 00 00 00")
                + b"\x48\xa3"
                + packed_exports[0][1]
                + b"\x58"
                + CURRENT_ROOT_ENTRY
            )

    return ReviewedRootHook


class UiCompatibilityError(ValueError):
    """Fixed diagnostic codes; never expose addresses, bytes or raw exceptions."""

    def __init__(self, code):
        if code not in {
            "root-signature-unavailable",
            "root-instruction-changed",
            "scratch-signature-unavailable",
            "client-build-unreviewed",
        }:
            raise ValueError("Unsupported compatibility code")
        self.code = code
        super().__init__(code)


def common_module():
    path = Path(__file__).with_name("wizwalker_adapter.py")
    raw = path.read_bytes()
    if hashlib.sha256(raw).hexdigest() != COMMON_SHA256:
        raise ValueError("Reviewed loading helper changed")
    spec = importlib.util.spec_from_file_location("maelstrom_loading_common", path)
    module = importlib.util.module_from_spec(spec)
    exec(compile(raw, str(path), "exec"), module.__dict__)
    return module


common = common_module()


def combat_reader_module():
    """Load only the pinned, read-only field decoder on explicit combat opt-in."""
    path = Path(__file__).with_name("w101_combat_reader.py")
    raw = path.read_bytes()
    if hashlib.sha256(raw).hexdigest() != COMBAT_READER_SHA256:
        raise ValueError("Reviewed combat reader changed")
    spec = importlib.util.spec_from_file_location("w101_combat_reader", path)
    module = importlib.util.module_from_spec(spec)
    # Dataclass annotations require the module to be visible during execution.
    sys.modules[spec.name] = module
    try:
        exec(compile(raw, str(path), "exec"), module.__dict__)
    except BaseException:
        sys.modules.pop(spec.name, None)
        raise
    return module


def duel_reader_module():
    path = Path(__file__).with_name("w101_duel_reader.py")
    raw = path.read_bytes()
    if hashlib.sha256(raw).hexdigest() != DUEL_READER_SHA256:
        raise ValueError("Reviewed duel reader changed")
    spec = importlib.util.spec_from_file_location("maelstrom_duel_fields", path)
    module = importlib.util.module_from_spec(spec)
    exec(compile(raw, str(path), "exec"), module.__dict__)
    return module


def pose_reader_module():
    combat_reader_module()  # installs the pinned shared bounded reader
    path = Path(__file__).with_name("w101_pose_reader.py")
    raw = path.read_bytes()
    if hashlib.sha256(raw).hexdigest() != POSE_READER_SHA256:
        raise ValueError("Reviewed pose reader changed")
    spec = importlib.util.spec_from_file_location("maelstrom_pose_fields", path)
    module = importlib.util.module_from_spec(spec)
    exec(compile(raw, str(path), "exec"), module.__dict__)
    # The isolated worker has no tools directory on sys.path. Expose the already
    # hash-checked module to the moving decoder instead of importing an unpinned copy.
    sys.modules["w101_pose_reader"] = module
    return module


def navigation_reader_module(name):
    """Pinned candidate helpers, available only within explicit pose observation."""
    path = Path(__file__).with_name(name + ".py")
    raw = path.read_bytes()
    if hashlib.sha256(raw).hexdigest() != NAVIGATION_READERS[name]:
        raise ValueError("Reviewed navigation reader changed")
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    exec(compile(raw, str(path), "exec"), module.__dict__)
    return module


def identity_reader_module():
    path = Path(__file__).with_name("w101_identity.py")
    raw = path.read_bytes()
    if hashlib.sha256(raw).hexdigest() != IDENTITY_READER_SHA256:
        raise ValueError("Reviewed identity reader changed")
    spec = importlib.util.spec_from_file_location("maelstrom_wizard_identity", path)
    module = importlib.util.module_from_spec(spec)
    exec(compile(raw, str(path), "exec"), module.__dict__)
    return module


async def current_duel_manager(read, image_base, image_size):
    """Exact-image static binding reviewed in W101-COMBAT-DUEL-10. No scan/hook."""
    pointer(image_base)
    if image_size != 57729024:
        raise ValueError("Unreviewed image mapping")
    instruction = await read(image_base + 14476370, 7)
    if instruction != bytes.fromhex("48 8b 0d 47 63 55 02"):
        raise ValueError("Unreviewed duel instruction")
    slot = image_base + 14476370 + 7 + struct.unpack("<i", instruction[3:])[0]
    if slot != image_base + 53626784:
        raise ValueError("Unreviewed duel binding")
    return slot, pointer(struct.unpack("<Q", await read(slot, 8))[0])


def validate_request(request):
    if not isinstance(request, dict) or request.get("operation") != VERSION:
        raise ValueError("Unsupported UI observation")
    if request.get("uiTreeEnabled") is not True:
        raise ValueError("UI tree requires explicit opt-in")
    if type(request.get("includeQuestContext", False)) is not bool:
        raise ValueError("Quest context requires a boolean opt-in")
    if type(request.get("includeCombatContext", False)) is not bool:
        raise ValueError("Combat context requires a boolean opt-in")
    if type(request.get("includeResourceContext", False)) is not bool:
        raise ValueError("Resource context requires boolean opt-in")
    if type(request.get("includeDanceEntryContext", False)) is not bool:
        raise ValueError("Dance entry context requires a boolean opt-in")
    if type(request.get("includePoseContext", False)) is not bool:
        raise ValueError("Pose context requires a boolean opt-in")
    if type(request.get("stopAfterTwoTrees", False)) is not bool:
        raise ValueError("Two-tree snapshot requires a boolean opt-in")
    stream_seconds = request.get("movingPoseSeconds", 0)
    stream_id = request.get("poseStreamId")
    if stream_id is not None and (
        not isinstance(stream_id, str)
        or len(stream_id) != 32
        or any(c not in "0123456789abcdef" for c in stream_id)
    ):
        raise ValueError("Invalid owner stream identity")
    if (
        type(stream_seconds) is not int
        or not 0 <= stream_seconds <= 3
        or (stream_seconds and not request.get("includePoseContext"))
    ):
        raise ValueError("Moving stream requires pose opt-in and 1 through 3 seconds")
    duration = request.get("durationSeconds")
    if type(duration) is not int or not 1 <= duration <= 60:
        raise ValueError("Bound UI observation to 1 through 60 seconds")
    base = {
        k: v
        for k, v in request.items()
        if k
        not in (
            "durationSeconds",
            "movingPoseSeconds",
            "poseStreamId",
            "uiTreeEnabled",
            "includeQuestContext",
            "includeCombatContext",
            "includePoseContext",
            "includeDanceEntryContext",
            "includeResourceContext",
            "stopAfterTwoTrees",
        )
    }
    common.validate_request(base | {"operation": common.VERSION})
    return request


class ReadSchedule:
    """Serialized multirate reads. Missed intervals are dropped, never queued."""

    def __init__(self, start):
        self.next_loading = start
        self.next_tree = start + 0.125

    def take(self, now):
        if now >= self.next_loading:
            self.next_loading = now + 0.25
            return "loading"
        if now >= self.next_tree:
            self.next_tree = now + 1.0
            return "tree"
        return None


class RootScope:
    """Reuse only RootWindowHook; preserve/verify the complete scratch block.

    The legacy handler zeroes 3900 bytes but saves only a pattern-length prefix.
    This scope captures the entire block before any write and does not zero it.
    It tracks partial setup, restores entry code before the small hook body,
    verifies the whole original block, and only then releases the export.
    """

    def __init__(
        self,
        handler,
        hook_type,
        settle=asyncio.sleep,
        expected_entry=None,
        export_name="current_root_window_addr",
    ):
        self.handler, self.hook_type, self.settle = handler, hook_type, settle
        self.hook = None
        self.original_entry = self.original_scratch = None
        self.entry = self.scratch = None
        self.touched = False
        self.restored = False
        self.expected_entry = expected_entry or bytes.fromhex("49 8b 8f d8 00 00 00")
        self.entry_size = len(self.expected_entry)
        self.export_name = export_name

    async def open(self):
        h = self.handler
        try:
            self.entry = await h.pattern_scan(
                self.hook_type.pattern, module="WizardGraphicalClient.exe"
            )
        except Exception as error:
            raise UiCompatibilityError("root-signature-unavailable") from error
        self.original_entry = await h.read_bytes(self.entry, self.entry_size)
        if self.original_entry != self.expected_entry:
            raise UiCompatibilityError("root-instruction-changed")
        try:
            self.scratch = await h.pattern_scan(
                h.AUTOBOT_PATTERN, module="WizardGraphicalClient.exe"
            )
        except Exception as error:
            raise UiCompatibilityError("scratch-signature-unavailable") from error
        if (
            self.entry < self.scratch + h.AUTOBOT_SIZE
            and self.scratch < self.entry + self.entry_size
        ):
            raise ValueError("Hook regions overlap")
        self.original_scratch = await h.read_bytes(self.scratch, h.AUTOBOT_SIZE)
        if len(self.original_scratch) != h.AUTOBOT_SIZE:
            raise ValueError("Incomplete restoration baseline")
        h._autobot_address = self.scratch
        h._autobot_pos = 0
        h._original_autobot_bytes = self.original_scratch
        self.hook = self.hook_type(h)

        async def verified_entry(pattern, module=None):
            if await h.read_bytes(self.entry, self.entry_size) != self.original_entry:
                raise ValueError("Root instruction changed before setup")
            return self.entry

        self.hook.get_jump_address = verified_entry
        self.touched = True  # includes partially failed writes
        await self.hook.hook()
        if (
            self.hook.jump_address != self.entry
            or self.hook.jump_original_bytecode != self.original_entry
        ):
            raise ValueError("Unexpected root hook binding")
        if await h.read_bytes(self.entry, self.entry_size) != self.hook.jump_bytecode:
            raise ValueError("Root hook setup not confirmed")
        if (
            await h.read_bytes(self.hook.hook_address, len(self.hook.hook_bytecode))
            != self.hook.hook_bytecode
        ):
            raise ValueError("Root hook body not confirmed")

    async def root(self):
        if self.hook is None:
            raise ValueError("Root hook unavailable")
        address = await self.handler.read_typed(
            getattr(self.hook, self.export_name), "unsigned long long"
        )
        if address < 65536 or address % 8:
            raise ValueError("Root window unavailable")
        return address

    async def close(self):
        if self.restored:
            return
        if self.touched:
            h = self.handler
            # Never free exports or overwrite scratch while the entry still points there.
            await h.write_bytes(self.entry, self.original_entry)
            if await h.read_bytes(self.entry, self.entry_size) != self.original_entry:
                raise ValueError("Root entry restoration unconfirmed")
            await self.settle(0.5)
            body_size = len(getattr(self.hook, "hook_bytecode", None) or b"")
            if body_size > 50:
                raise ValueError("Unexpected root hook body size")
            if body_size:
                await h.write_bytes(self.scratch, self.original_scratch[:body_size])
            if (
                await h.read_bytes(self.scratch, len(self.original_scratch))
                != self.original_scratch
            ):
                raise ValueError("Full scratch restoration unconfirmed")
            export = getattr(self.hook, self.export_name, None)
            if export:
                await h.free(export)
                setattr(self.hook, self.export_name, None)
        self.restored = True


class UiClient:
    def __init__(
        self,
        target,
        include_quest_context=False,
        include_combat_context=False,
        include_resource_context=False,
        include_dance_entry_context=False,
    ):
        from wizwalker import Client
        from wizwalker.memory.hooks import RootWindowHook

        self.image_sha256 = target["imageSha256"]
        self.include_dance_entry_context = include_dance_entry_context
        self.include_resource_context = include_resource_context
        self.resource_labels = {}
        self.include_quest_context = include_quest_context
        self.include_combat_context = include_combat_context
        self.combat_reader = combat_reader_module() if include_combat_context else None
        self.duel_reader = duel_reader_module() if include_combat_context else None
        try:
            profile, entry, scratch_size = profile_for(target["imageSha256"])
        except UiCompatibilityError:
            profile, entry, scratch_size = "unreviewed", CURRENT_ROOT_ENTRY, 4100
        self.current_layout = profile == "r13-current"
        self.archived_profile = profile == "r15-archived"
        self.client = Client(target["handle"])
        self.reviewed_build = profile != "unreviewed"
        handler = self.client.hook_handler
        if self.current_layout:
            handler.AUTOBOT_PATTERN = CURRENT_SCRATCH_PATTERN
        handler.AUTOBOT_SIZE = scratch_size
        self.scope = RootScope(
            handler,
            (
                current_root_hook(RootWindowHook)
                if self.current_layout
                else RootWindowHook
            ),
            expected_entry=entry,
        )
        self.handle_closed = False
        if self.client.process_id != target["processId"]:
            self.client._pymem.close_process()
            raise ValueError("Selected client changed")

    async def open(self):
        if not self.reviewed_build:
            raise UiCompatibilityError("client-build-unreviewed")
        await self.scope.open()

    async def loading(self):
        return await self.client.is_loading()

    async def tree(self):
        self.resource_acquired = common.utc_ticks()
        tree = await snapshot(
            self.client.hook_handler.read_bytes,
            await self.scope.root(),
            current_layout=self.current_layout,
            archived_profile=self.archived_profile,
            include_quest_context=self.include_quest_context,
            include_combat_context=self.include_combat_context,
            combat_reader=self.combat_reader,
            include_resource_context=self.include_resource_context,
            include_dance_entry_context=getattr(
                self, "include_dance_entry_context", False
            ),
        )
        if self.include_resource_context:
            self.resource_globes = tree.get("resourceGlobes", {})
            self.resource_potions = tree.get("resourcePotions")
            self.resource_labels = {
                n["name"]: n["text"]
                for n in tree["nodes"]
                if n.get("name") in {"textHealth", "textMana", "textEnergy"}
                and n.get("text") is not None
            }
        fields = tree.get("combatFieldCandidates")
        if self.duel_reader is not None and fields and fields["participants"]:
            try:
                from pymem.process import module_from_name

                module = module_from_name(
                    self.client._pymem.process_handle, "WizardGraphicalClient.exe"
                )
                read = self.client.hook_handler.read_bytes
                slot, manager = await current_duel_manager(
                    read, module.lpBaseOfDll, module.SizeOfImage
                )
                duel = await self.duel_reader.read_duel_context(
                    read,
                    manager,
                    [p["ownerId"] for p in fields["participants"]],
                    layout_id="deimos-development-df2283f",
                )
                if struct.unpack("<Q", await read(slot, 8))[0] != manager:
                    raise ValueError("Changed duel binding")
                tree["duelCandidates"] = duel
            except (ValueError, OSError, KeyError, AttributeError):
                tree["duelCandidates"] = None
        return tree

    async def close(self):
        try:
            await self.scope.close()
        finally:
            self.client._pymem.close_process()
            self.handle_closed = True

    async def resources(self):
        """Qualification only: serialized legacy stats hook, no resource publication."""
        if (
            self.image_sha256
            != "d410a2463e7ff0e221d6d11ca10fa53de265abd17cacfba211f9eeca8ec8511a"
        ):
            return dict(status="unknown", reason="stats-layout-unreviewed")
        from wizwalker.memory.hooks import PlayerStatHook

        await self.scope.close()
        scope = RootScope(
            self.client.hook_handler,
            PlayerStatHook,
            expected_entry=bytes.fromhex("2b d8 b8 00 00 00 00"),
            export_name="stat_addr",
        )
        self.scope = scope
        stage = "hook-open"
        try:
            await scope.open()
            stage = "stats-export"
            address = None
            for _ in range(20):
                await asyncio.sleep(0.05)
                try:
                    address = await scope.root()
                    break
                except ValueError:
                    continue
            if address is None:
                return dict(
                    status="unknown", reason="stats-export-not-observed", stage=stage
                )
            stage = "stats-read"
            acquired = common.utc_ticks()
            read = self.client.hook_handler.read_bytes
            from pymem.process import module_from_name

            module = module_from_name(
                self.client._pymem.process_handle, "WizardGraphicalClient.exe"
            )
            identity_reader = identity_reader_module()
            before_identity = await identity_reader.read_identity(
                read, module.lpBaseOfDll, module.SizeOfImage, address
            )
            first = await read(address + 80, 992)
            await asyncio.sleep(0.1)
            second = await read(address + 80, 992)
            after_identity = await identity_reader.read_identity(
                read, module.lpBaseOfDll, module.SizeOfImage, address
            )
            wizard_identity = qualify_identity_pair(before_identity, after_identity)
            offsets = (112, 136)
            if (
                any(first[o - 80 : o - 76] != second[o - 80 : o - 76] for o in offsets)
                or await scope.root() != address
            ):
                return dict(status="unknown", reason="stats-changed-during-read")

            def integer(offset):
                return struct.unpack_from("<i", second, offset - 80)[0]

            def floating(offset):
                return struct.unpack_from("<f", second, offset - 80)[0]

            import math

            named_fields = []
            for name, offset, kind in RESOURCE_SCALARS:
                fmt, width = {
                    "int32": ("<i", 4),
                    "uint32": ("<I", 4),
                    "uint8": ("<B", 1),
                    "float32": ("<f", 4),
                }[kind]
                value = struct.unpack_from(fmt, second, offset - 80)[0]
                stable = (
                    first[offset - 80 : offset - 80 + width]
                    == second[offset - 80 : offset - 80 + width]
                )
                valid = stable and math.isfinite(value)
                named_fields.append(
                    dict(
                        name=name,
                        offset=offset,
                        dataType=kind,
                        value=value if valid else None,
                        quality="candidate" if valid else "unknown",
                        reason=(
                            "awaiting-independent-verification"
                            if valid
                            else "changed-or-nonfinite"
                        ),
                        source="exact-build-property-registration",
                        executable=False,
                    )
                )

            def finite_float(offset):
                value = floating(offset)
                return value if math.isfinite(value) else None

            object_type = None
            try:
                from pymem.process import module_from_name

                module = module_from_name(
                    self.client._pymem.process_handle, "WizardGraphicalClient.exe"
                )
                base, size = module.lpBaseOfDll, module.SizeOfImage

                def inside(value, length):
                    if not base <= value <= base + size - length:
                        raise ValueError("type-reference-outside-image")
                    return value

                vtable = struct.unpack("<Q", await read(address, 8))[0]
                locator = struct.unpack("<Q", await read(inside(vtable - 8, 8), 8))[0]
                col = await read(inside(locator, 24), 24)
                signature, _, _, descriptor, _, self_rva = struct.unpack("<6I", col)
                if signature != 1 or base + self_rva != locator:
                    raise ValueError("type-locator-invalid")
                name = (await read(inside(base + descriptor + 16, 96), 96)).split(
                    b"\0", 1
                )[0]
                if re.fullmatch(rb"[.?@$A-Za-z0-9_]{1,95}", name):
                    object_type = name.decode("ascii")
                if struct.unpack("<Q", await read(address, 8))[0] != vtable:
                    raise ValueError("type-changed")
            except Exception:
                object_type = None
            return dict(
                status="candidate",
                liveQualified=False,
                observedUtcTicks=acquired,
                layout="d410-property-registration-candidate",
                uiResources=getattr(self, "resource_labels", {}),
                uiGlobes=getattr(self, "resource_globes", {}),
                uiPotions=getattr(self, "resource_potions", None),
                uiObservedUtcTicks=getattr(self, "resource_acquired", 0),
                objectType=object_type,
                namedFields=named_fields,
                wizardIdentity=wizard_identity,
                health=dict(current=integer(112), maximum=integer(80) + integer(224)),
                mana=dict(current=integer(136), maximum=integer(84) + integer(228)),
                energy=dict(baseMaximum=integer(108), bonus=integer(244)),
                potions=dict(current=finite_float(172), maximum=finite_float(168)),
            )
        except Exception as error:
            return dict(
                status="unknown",
                reason="stats-candidate-unavailable",
                stage=stage,
                error=type(error).__name__,
            )
        finally:
            await scope.close()

    async def pose_owner(self, read, parent):
        """Compare the actor parent directly with the guarded local registry owner.

        This establishes an endpoint association, not continuous scene identity.
        No pointers are returned or cached for use by another observer.
        """
        if getattr(self, "image_sha256", None) != ARCHIVED_LAYOUT_IMAGE_SHA256:
            return dict(status="unknown", reason="owner-profile-unreviewed")
        try:
            from pymem.process import module_from_name

            module = module_from_name(
                self.client._pymem.process_handle, "WizardGraphicalClient.exe"
            )
            reader = identity_reader_module()
            stats = struct.unpack("<Q", await read(parent + reader.STATS_OFFSET, 8))[0]
            return await reader.read_identity(
                read,
                module.lpBaseOfDll,
                module.SizeOfImage,
                stats,
                expected_owner=parent,
            )
        except Exception:
            return dict(status="unknown", reason="pose-owner-unavailable")

    async def pose(self):
        """Serialize hooks; never let two hooks share the scratch block."""
        from wizwalker.memory.hooks import PlayerHook

        self.pose_stage = "reader-load"
        reader = pose_reader_module()
        await self.scope.close()
        scope = RootScope(
            self.client.hook_handler,
            reader.player_hook(PlayerHook),
            expected_entry=bytes.fromhex("f2 0f 10 40 58"),
            export_name="player_struct",
        )
        # Retain the new scope even if open fails, so outer cleanup retries it.
        self.scope = scope
        try:
            self.pose_stage = "player-hook-open"
            await scope.open()
            await asyncio.sleep(0.25)
            self.pose_stage = "player-export"
            address = await scope.root()
            self.pose_stage = "stationary-pose"
            value = await reader.read_pose(
                self.client.hook_handler.read_bytes, address, layout_id=reader.LAYOUT_ID
            )
            self.pose_stage = "navigation-readers"
            zone_reader = navigation_reader_module("w101_zone_reader")
            moving_reader = navigation_reader_module("w101_moving_pose")
            read = self.client.hook_handler.read_bytes
            self.pose_stage = "zone-candidate"
            parent_raw = await read(address + 72, 8)
            parent = struct.unpack("<Q", parent_raw)[0]
            owner_before = await self.pose_owner(read, parent)
            zone = await zone_reader.read_zone(
                read, parent, layout_id=zone_reader.LAYOUT_ID
            )
            # Parent-as-ClientObject remains a hypothesis. Never publish this as
            # a current-zone fact or use it to authorize navigation.
            zone["ownerBinding"] = "actor-parent-unqualified"
            stream_id = getattr(self, "pose_stream_id", None) or uuid.uuid4().hex
            sequence = 0
            context = moving_reader.PoseContext(
                str(self.client.process_id),
                stream_id,
                zone.get("zoneName") or "unqualified-zone",
                owner_before.get("characterKey") or "unqualified-wizard",
            )
            self.pose_stage = "moving-window"
            ready = getattr(self, "pose_window_ready", None)
            # Recheck after hook setup, before permitting the owner handshake.
            if await self.loading() is not False or await scope.root() != address:
                raise ValueError("Navigation readiness changed")
            if getattr(self, "pose_stream_current", lambda: True)() is not True:
                raise ValueError("Navigation owner changed")
            stream_seconds = getattr(self, "moving_pose_seconds", 0)
            if stream_seconds:

                async def refresh():
                    nonlocal context
                    if not self.pose_stream_current() or await scope.root() != address:
                        raise ValueError("Navigation owner changed")
                    current_owner = await self.pose_owner(read, parent)
                    if (
                        current_owner.get("status") != "observed"
                        or current_owner.get("ownerMatched") is not True
                        or current_owner.get("characterKey")
                        != owner_before.get("characterKey")
                    ):
                        raise ValueError("Navigation wizard changed or unverified")
                    loading = await self.loading()
                    current_zone = await zone_reader.read_zone(
                        read, parent, layout_id=zone_reader.LAYOUT_ID
                    )
                    context = moving_reader.PoseContext(
                        str(self.client.process_id),
                        stream_id,
                        current_zone.get("zoneName") or "unqualified-zone",
                        current_owner["characterKey"],
                        loading is not False,
                    )

                async def sample(value):
                    nonlocal sequence
                    sequence += 1
                    await self.pose_stream_emit(
                        dict(
                            kind="moving-pose-candidate",
                            streamId=stream_id,
                            sequence=sequence,
                            binding=getattr(self, "pose_stream_binding", None),
                            observedUtcTicks=common.utc_ticks(),
                            sampleAgeMs=max(
                                0, (time.perf_counter() - value["started"]) * 1000
                            ),
                            sample=value,
                            zone=context.zone,
                            wizardKey=context.wizard,
                            wizardOwnerChecked=True,
                            contextQualified=False,
                        )
                    )

                async def invalidated(value):
                    await self.pose_stream_emit(
                        dict(
                            kind="moving-pose-invalidated",
                            observedUtcTicks=common.utc_ticks(),
                            reason=value.get("reason", "stream-ended"),
                        )
                    )

                await refresh()
                if context.loading or context.zone != zone.get("zoneName"):
                    raise ValueError("Navigation readiness changed")
                if ready is not None:
                    await ready()
                window = await moving_reader.stream_moving_pose(
                    read,
                    address,
                    layout_id=reader.LAYOUT_ID,
                    expected_context=context,
                    get_context=lambda: context,
                    refresh_context=refresh,
                    limits=moving_reader.MovingPoseStreamLimits(
                        2000,
                        1000,
                        20,
                        sample_count=stream_seconds * 12,
                        max_duration_seconds=stream_seconds,
                    ),
                    clock=time.perf_counter,
                    on_sample=sample,
                    on_invalidated=invalidated,
                )
            else:
                if ready is not None:
                    await ready()
                window = await moving_reader.read_moving_pose(
                    read,
                    address,
                    layout_id=reader.LAYOUT_ID,
                    expected_context=context,
                    get_context=lambda: context,
                    limits=moving_reader.MovingPoseLimits(
                        2000, 1000, 20, sample_count=8
                    ),
                    clock=time.perf_counter,
                )
            zone_after = await zone_reader.read_zone(
                read, parent, layout_id=zone_reader.LAYOUT_ID
            )
            owner_after = await self.pose_owner(read, parent)
            owner_pair = qualify_identity_pair(owner_before, owner_after)
            zone["wizardOwnership"] = owner_pair
            dimensions_reader = getattr(reader, "read_dimensions", None)
            if dimensions_reader is not None:
                zone["bodyDimensionsCandidate"] = await dimensions_reader(read, address)
            zone["ownerBinding"] = (
                "registry-owner-endpoints-matched"
                if owner_pair.get("status") == "observed"
                and owner_before.get("ownerMatched") is True
                and owner_after.get("ownerMatched") is True
                else "actor-parent-unqualified"
            )
            if (
                parent_raw != await read(address + 72, 8)
                or zone.get("zoneName") != zone_after.get("zoneName")
                or await self.loading()
            ):
                raise ValueError("Navigation context changed")
            value["zoneCandidate"] = zone
            window["contextQualified"] = False
            window["limitsSource"] = "experimental-rejection-bounds-not-calibration"
            value["movingWindowCandidate"] = window
            if await scope.root() != address:
                raise ValueError("Player export changed")
            return dict(value, observedUtcTicks=common.utc_ticks())
        finally:
            try:
                if getattr(self, "moving_pose_seconds", 0):
                    await asyncio.wait_for(
                        self.pose_stream_emit(
                            dict(
                                kind="moving-pose-ended",
                                observedUtcTicks=common.utc_ticks(),
                                currentAuthority=False,
                            )
                        ),
                        0.1,
                    )
            finally:
                await scope.close()


# Exact-build named scalar candidates. Static registration + imported type semantics.
def qualify_identity_pair(before, after):
    """Two owner-checked reads must agree around the complete stats acquisition."""
    if (
        before.get("status") == after.get("status") == "observed"
        and before.get("source") == after.get("source") == "client-object-registry-v1"
        and before.get("statsMatched") is True
        and after.get("statsMatched") is True
        and isinstance(before.get("characterKey"), str)
        and re.fullmatch(r"[0-9a-f]{64}", before["characterKey"])
        and before["characterKey"] == after.get("characterKey")
    ):
        return dict(
            after,
            readCalls=before["readCalls"] + after["readCalls"],
            readBytes=before["readBytes"] + after["readBytes"],
        )
    return dict(
        status="unknown",
        reason="wizard-owner-not-stable",
        source="client-object-registry-v1",
        statsMatched=False,
    )


RESOURCE_SCALARS = (
    ("m_baseHitpoints", 80, "int32"),
    ("m_baseMana", 84, "int32"),
    ("m_baseGoldPouch", 88, "int32"),
    ("m_baseEventCurrency1Pouch", 92, "int32"),
    ("m_baseEventCurrency2Pouch", 96, "int32"),
    ("m_basePvPCurrencyPouch", 100, "int32"),
    ("m_basePvPTourneyCurrencyPouch", 104, "int32"),
    ("m_energyMax", 108, "int32"),
    ("m_currentHitpoints", 112, "int32"),
    ("m_currentGold", 116, "int32"),
    ("m_currentEventCurrency1", 120, "int32"),
    ("m_currentEventCurrency2", 124, "int32"),
    ("m_currentPvPCurrency", 128, "int32"),
    ("m_currentPvPTourneyCurrency", 132, "int32"),
    ("m_currentMana", 136, "int32"),
    ("m_currentArenaPoints", 140, "int32"),
    ("m_potionMax", 168, "float32"),
    ("m_potionCharge", 172, "float32"),
    ("m_bonusHitpoints", 224, "int32"),
    ("m_bonusMana", 228, "int32"),
    ("m_bonusEnergy", 244, "int32"),
    ("m_criticalHitPercentAll", 248, "float32"),
    ("m_blockPercentAll", 252, "float32"),
    ("m_criticalHitRatingAll", 256, "float32"),
    ("m_blockRatingAll", 260, "float32"),
    ("m_referenceLevel", 324, "int32"),
    ("m_petActChance", 344, "int32"),
    ("m_dmgBonusPercentAll", 712, "float32"),
    ("m_dmgBonusFlatAll", 716, "float32"),
    ("m_accBonusPercentAll", 720, "float32"),
    ("m_apBonusPercentAll", 724, "float32"),
    ("m_dmgReducePercentAll", 728, "float32"),
    ("m_dmgReduceFlatAll", 732, "float32"),
    ("m_accReducePercentAll", 736, "float32"),
    ("m_healBonusPercentAll", 740, "float32"),
    ("m_healIncBonusPercentAll", 744, "float32"),
    ("m_spellChargeBonusAll", 752, "int32"),
    ("m_powerPipBase", 756, "float32"),
    ("m_powerPipBonusPercentAll", 792, "float32"),
    ("m_xpPercentIncrease", 800, "float32"),
    ("m_balanceMastery", 832, "int32"),
    ("m_deathMastery", 836, "int32"),
    ("m_fireMastery", 840, "int32"),
    ("m_iceMastery", 844, "int32"),
    ("m_lifeMastery", 848, "int32"),
    ("m_mythMastery", 852, "int32"),
    ("m_stormMastery", 856, "int32"),
    ("m_maximumNumberOfIslands", 860, "int32"),
    ("m_gardeningLevel", 864, "uint8"),
    ("m_gardeningXP", 868, "int32"),
    ("m_stunResistancePercent", 880, "float32"),
    ("m_shadowPipMax", 888, "int32"),
    ("m_fishingLevel", 893, "uint8"),
    ("m_fishingXP", 896, "int32"),
    ("m_fishingLuckBonusPercentAll", 748, "float32"),
    ("m_shadowPipBonusPercent", 796, "float32"),
    ("m_wispBonusPercent", 824, "float32"),
    ("m_pipConversionRatingAll", 288, "float32"),
    ("m_pipConversionPercentAll", 320, "float32"),
    ("m_monsterMagicLevel", 908, "uint8"),
    ("m_monsterMagicXP", 912, "int32"),
    ("m_extraInventorySpace", 920, "int32"),
    ("m_pipConversionBaseAllSchools", 760, "int32"),
    ("m_adventurePowerCooldownTime", 1000, "uint32"),
    ("m_shadowPipRating", 1004, "float32"),
    ("m_bonusShadowPipRating", 1008, "float32"),
    ("m_shadowPipRateAccumulated", 1012, "float32"),
    ("m_shadowPipRateThreshold", 1016, "float32"),
    ("m_shadowPipRatePercentage", 1020, "int32"),
    ("m_cantripLevel", 1040, "uint8"),
    ("m_cantripXP", 1044, "int32"),
    ("m_archmasteryBase", 1048, "float32"),
    ("m_archmasteryBonusFlat", 1052, "float32"),
    ("m_archmasteryBonusPercentage", 1056, "float32"),
    ("m_schoolID", 328, "uint32"),
    ("m_levelScaled", 332, "int32"),
)


def qualify_resources(candidate, image, snapshot_ticks):
    """Current image property layout + independently observed HUD agreement."""
    if image != "d410a2463e7ff0e221d6d11ca10fa53de265abd17cacfba211f9eeca8ec8511a":
        return None
    if (
        candidate.get("status") != "candidate"
        or candidate.get("layout") != "d410-property-registration-candidate"
    ):
        return None
    stamp = candidate.get("observedUtcTicks", 0)
    hud_stamp = candidate.get("uiObservedUtcTicks", 0)
    if (
        not 0 < snapshot_ticks <= hud_stamp <= stamp
        or stamp - snapshot_ticks > 20000000
    ):
        return None
    labels = candidate.get("uiResources", {})

    def number(name):
        text = re.sub(r"<[^>]*>", "", labels.get(name, "")).strip()
        return int(text) if re.fullmatch(r"[0-9]{1,8}", text) else None

    currents = {}
    for key, name in (("health", "textHealth"), ("mana", "textMana")):
        current = candidate.get(key, {}).get("current")
        if (
            type(current) is not int
            or not 0 <= current <= 100000
            or number(name) != current
        ):
            return None
        currents[key] = current
    energy = number("textEnergy")
    if energy is not None and 0 <= energy <= 100000:
        currents["energy"] = energy
    # Exact-build StatGlobe getters establish displayed maximum semantics.
    # Never recompute maxima from the transient base/bonus stats representation.
    values = {}
    for key, current in currents.items():
        globe = candidate.get("uiGlobes", {}).get(key, {})
        maximum = globe.get("maximum")
        if (
            type(globe.get("current")) is int
            and globe["current"] == current
            and type(maximum) is int
            and 0 < maximum <= 100000
            and current <= maximum
        ):
            values[key] = dict(current=current, maximum=maximum)
    potion = candidate.get("potions", {})
    ui_potion = candidate.get("uiPotions") or {}
    charge, capacity = potion.get("current"), potion.get("maximum")
    if (
        type(charge) in (int, float)
        and type(capacity) in (int, float)
        and 0 <= charge <= capacity <= 100
        and capacity > 0
        and type(ui_potion.get("current")) in (int, float)
        and type(ui_potion.get("maximum")) in (int, float)
        and ui_potion["current"] == charge
        and ui_potion["maximum"] == capacity
    ):
        values["potions"] = dict(current=charge, maximum=capacity)
        currents["potions"] = charge
    return dict(
        status="observed",
        observedUtcTicks=stamp,
        values=values,
        currentValues=currents,
        source="hud-controls-and-correlated-stats",
    )


def pointer(value, alignment=8):
    if (
        type(value) is not int
        or not 65536 <= value < 0x7FFFFFFFFFFF
        or value % alignment
    ):
        raise ValueError("Invalid UI pointer")
    return value


async def snapshot(
    read,
    root,
    *,
    current_layout=False,
    archived_profile=False,
    include_quest_context=False,
    include_combat_context=False,
    combat_reader=None,
    include_resource_context=False,
    include_dance_entry_context=False,
):
    """Bounded visible tree; batched headers and child vectors, no unsafe text API."""
    nodes, seen = [], set()
    resource_globes = {}
    resource_potions = None
    combat_bindings = []
    dance_entry_count = 0
    pending = [(pointer(root), None, 0, True, ())]
    total_bytes = 0

    async def bounded(address, count):
        nonlocal total_bytes
        if not 0 < count <= 4096 or total_bytes + count > 131072:
            raise ValueError("UI byte budget exceeded")
        total_bytes += count
        value = await read(address, count)
        if len(value) != count:
            raise ValueError("Short UI read")
        return value

    async def control_type(address):
        # Current upstream ControlText/ControlList layout. Validate the type-name
        # instruction before following it; no arbitrary fallback text offsets.
        vtable = pointer(struct.unpack("<Q", await bounded(address, 8))[0])
        function = pointer(struct.unpack("<Q", await bounded(vtable, 8))[0], 1)
        prefix = await bounded(function, 5)
        if prefix[0] == 0xE9:
            function = pointer(function + 5 + struct.unpack("<i", prefix[1:])[0], 1)
        lea = await bounded(function + 63, 7)
        if lea[:3] != bytes.fromhex("48 8d 0d"):
            raise ValueError("Unqualified control type instruction")
        name_address = pointer(function + 70 + struct.unpack("<i", lea[3:])[0], 1)
        raw = await bounded(name_address, 60)
        if b"\0" not in raw:
            raise ValueError("Unterminated control type")
        return raw.split(b"\0", 1)[0]

    selection_paths = {
        ("Root", "WorldView", "mainWindow", "sprBanner", "txtName"),
        ("Root", "WorldView", "mainWindow", "sprSubBanner", "txtLevel"),
        ("Root", "WorldView", "mainWindow", "sprSubBanner", "txtLocation"),
    }

    async def text_offset(address, idle=False):
        if not current_layout and not (archived_profile and idle):
            return 584
        if await control_type(address) not in (b"ControlText", b"ControlList"):
            raise ValueError("Unsupported static text control")
        return 712

    while pending:
        if len(nodes) >= MAX_NODES:
            raise ValueError("UI node budget exceeded")
        address, parent, depth, parent_visible, ancestors = pending.pop(0)
        if depth > MAX_DEPTH or address in seen:
            raise ValueError("UI cycle or depth limit")
        seen.add(address)
        header = await bounded(address + 80, 96)
        length = struct.unpack_from("<Q", header, 16)[0]
        if length > 128:
            raise ValueError("UI name limit")
        name_bytes = (
            header[:length]
            if length < 16
            else await bounded(pointer(struct.unpack_from("<Q", header)[0]), length)
        )
        name = name_bytes.decode("utf-8")
        if any(ord(c) < 32 for c in name):
            raise ValueError("Invalid UI name")
        flags = struct.unpack_from("<I", header, 76)[0]
        visible = bool(flags & 1) and parent_visible
        enabled = not bool(flags & 0x80000000)
        private = bool(PRIVATE_BRANCH.search(name))
        node = dict(
            index=len(nodes),
            parent=parent,
            name="[excluded]" if private else name,
            visible=visible,
            enabled=enabled,
            text=None,
        )
        nodes.append(node)
        if private or not visible:
            continue
        path = (*ancestors, name)
        dance_entry_node = include_dance_entry_context and path[:3] == DANCE_ENTRY_ROOT
        if path == DANCE_ENTRY_ROOT and not include_dance_entry_context:
            node["childrenSkipped"] = True
            continue
        if dance_entry_node:
            dance_entry_count += 1
            if dance_entry_count > MAX_DANCE_ENTRY_CONTROLS:
                raise ValueError("Dance entry control budget exceeded")
            rectangle = list(struct.unpack_from("<iiii", header, 80))
            node["rawRectangle"] = rectangle if valid_raw_rectangle(rectangle) else None
        # Geometry for reviewed gate controls only; raw parent-relative layout units.
        if (
            "optionWindow" in ancestors
            and name in {"leftButton", "rightButton", "selectedWorldCheckMark"}
            or name.startswith("wbtn")
            and "optionWindow" in ancestors
        ):
            rectangle = list(struct.unpack_from("<iiii", header, 80))
            node["rawRectangle"] = rectangle if valid_raw_rectangle(rectangle) else None
        if include_combat_context and combat_kind(path) is not None:
            # Already inside the bounded header; raw layout units, NOT client pixels.
            raw_rectangle = list(struct.unpack_from("<iiii", header, 80))
            node["rawRectangle"] = (
                raw_rectangle if valid_raw_rectangle(raw_rectangle) else None
            )
            if current_layout and combat_reader is not None:
                kind = combat_kind(path)
                if kind in ("hand-descendant", "combatant-container"):
                    expected = (
                        b"SpellCheckBox"
                        if kind == "hand-descendant"
                        else b"CombatantDataControl"
                    )
                    try:
                        matched_type = await control_type(address) == expected
                    except (ValueError, OSError, KeyError):
                        matched_type = False
                    if matched_type:
                        if len(combat_bindings) >= 24:
                            raise ValueError("Combat binding budget exceeded")
                        combat_bindings.append(
                            combat_reader.ControlBinding(
                                node["index"],
                                address,
                                "card" if kind == "hand-descendant" else "participant",
                            )
                        )
        resource_paths = tuple(
            ("Root", "WorldView", "windowHUD", sprite, rect, text)
            for sprite, rect, text in (
                ("spriteHealthBkg", "HealthGlobeRect", "textHealth"),
                ("spriteManaBkg", "ManaGlobeRect", "textMana"),
                ("spriteEnergyBkg", "EnergyGlobeRect", "textEnergy"),
            )
        )
        resource_branch = include_resource_context and any(
            p[: len(path)] == path for p in resource_paths
        )
        resource_text = include_resource_context and path in resource_paths
        if (
            include_resource_context
            and archived_profile
            and path == ("Root", "WorldView", "windowHUD", "btnPotions")
        ):
            try:
                if await control_type(address) == b"PotionButton":
                    maximum, current = struct.unpack(
                        "<ff", await bounded(address + 0x440, 8)
                    )
                    if 0 <= current <= maximum <= 100 and maximum > 0:
                        resource_potions = dict(current=current, maximum=maximum)
            except (ValueError, OSError, KeyError):
                pass
        globe = {
            "spriteHealthBkg": ("health", b"HealthGlobe"),
            "spriteManaBkg": ("mana", b"ManaGlobe"),
            "spriteEnergyBkg": ("energy", b"EnergyGlobe"),
        }.get(name)
        if (
            include_resource_context
            and archived_profile
            and globe is not None
            and ancestors == ("Root", "WorldView", "windowHUD")
        ):
            try:
                if await control_type(address) == globe[1]:
                    raw = await bounded(address + 0x3A0, 8)
                    current, maximum = struct.unpack("<II", raw)
                    if 0 <= current <= maximum <= 100000 and maximum > 0:
                        resource_globes[globe[0]] = dict(
                            current=current, maximum=maximum
                        )
            except (ValueError, OSError, KeyError):
                pass
        quest_branch = include_quest_context and any(
            p[: len(path)] == path for p in QUEST_PATHS
        )
        inside_quest_branch = ancestors[:3] in (QUEST_OBJECTIVE[:3], QUEST_DIALOGUE[:3])
        hud_root = (current_layout or archived_profile) and path == (
            "Root",
            "WorldView",
            "windowHUD",
        )
        hud_child = ancestors == ("Root", "WorldView", "windowHUD")
        if (
            (name in STRUCTURAL_LEAVES and not quest_branch and not hud_root)
            or (
                (hud_child or ancestors == ("Root", "WorldView", "DeckConfiguration"))
                and name != "compassAndTeleporterButtons"
                and not quest_branch
                and not resource_branch
            )
            or (
                inside_quest_branch
                and not quest_branch
                and not resource_branch
                and path[:4]
                != ("Root", "WorldView", "windowHUD", "compassAndTeleporterButtons")
            )
        ):
            # Retain the control itself for path matching; its decorative/content
            # descendants cannot establish any of this observer's four states.
            node["childrenSkipped"] = True
            continue
        # Only the qualified static Idle path and the known NPC title field.
        # Quest text requires its own opt-in and exact path. No general text dump.
        if (
            name == "idleText"
            and ancestors == ("Root", "idleMessage", "idleBackground")
            or path in selection_paths
            or path == INTERACTION_TITLE
            or resource_text
            or include_dance_entry_context
            and current_layout
            and path in DANCE_ENTRY_TEXT
            or include_quest_context
            and path in (QUEST_OBJECTIVE, QUEST_DIALOGUE)
        ):
            try:
                string = await bounded(
                    address
                    + await text_offset(
                        address,
                        path == ("Root", "idleMessage", "idleBackground", "idleText")
                        or path in selection_paths
                        or path == INTERACTION_TITLE
                        or resource_text,
                    ),
                    24,
                )
                chars = struct.unpack_from("<Q", string, 16)[0]
                limit = (
                    2048
                    if path == QUEST_DIALOGUE
                    else 256 if path == QUEST_OBJECTIVE else 160
                )
                if chars <= limit:
                    size = chars * 2
                    text_bytes = (
                        string[:size]
                        if size < 8
                        else await bounded(
                            pointer(struct.unpack_from("<Q", string)[0]), size
                        )
                    )
                    node["text"] = text_bytes.decode("utf-16-le") if size else ""
            except (ValueError, OSError, KeyError):
                # Text is optional evidence: an unsupported control layout cannot
                # establish Idle, but need not discard otherwise valid node names.
                node["text"] = None
        start, end = struct.unpack_from("<QQ", header, 32)
        size = end - start
        if size < 0 or size % 16 or size > 4096:
            raise ValueError("UI children vector invalid")
        if size:
            children = await bounded(pointer(start), size)
            # Detect a vector changing during the read instead of inventing an empty tree.
            if await bounded(address + 112, 16) != header[32:48]:
                raise ValueError("UI changed during observation")
            for offset in range(0, size, 16):
                child = pointer(struct.unpack_from("<Q", children, offset)[0])
                pending.append(
                    (child, node["index"], depth + 1, visible, (*ancestors, name))
                )
    fields = None
    if include_combat_context and current_layout and combat_reader is not None:
        fields = await combat_reader.read_combat_fields(
            bounded, combat_bindings, layout_id="deimos-development-df2283f"
        )
        for card in fields.get("cards", []):
            ancestor = nodes[card["controlIndex"]]
            while True:
                if not ancestor["enabled"]:
                    card["castableCandidate"] = False
                    break
                if ancestor["parent"] is None:
                    break
                ancestor = nodes[ancestor["parent"]]
    return dict(
        nodes=nodes,
        complete=True,
        readBytes=total_bytes,
        layout=(
            CURRENT_IMAGE_SHA256
            if current_layout
            else (
                ARCHIVED_LAYOUT_IMAGE_SHA256
                if archived_profile
                else "legacy-unqualified"
            )
        ),
        questContextIncluded=include_quest_context,
        combatContextIncluded=include_combat_context,
        danceEntryContextIncluded=include_dance_entry_context,
        combatFieldCandidates=fields,
        resourceGlobes=resource_globes,
        resourcePotions=resource_potions,
    )


def combat_kind(path):
    """Name/ancestry allowlist; never follows spell/participant pointers or types."""
    if path == COMBAT_PLANNING:
        return "planning-container"
    if path == COMBAT_HAND:
        return "hand-container"
    if path[: len(COMBAT_HAND)] == COMBAT_HAND:
        return "hand-descendant"
    if path[:2] != ("Root", "WorldView"):
        return None
    if path[-1] == "CombatantControl":
        return "combatant-container"
    if (
        len(path) >= 4
        and path[-2] == "CombatantControl"
        and path[-1] in {"Name", "Health"}
    ):
        return "combatant-label"
    if path[-1] == "DoneWindow":
        return "done-container"
    if path[-1] in COMBAT_BUTTONS and (
        path[: len(COMBAT_PLANNING)] == COMBAT_PLANNING or "DoneWindow" in path[2:-1]
    ):
        return "button-candidate"
    return None


def valid_raw_rectangle(value):
    return (
        isinstance(value, list)
        and len(value) == 4
        and all(type(v) is int and -65536 <= v <= 65536 for v in value)
        and value[0] < value[2]
        and value[1] < value[3]
    )


def combat_context(tree):
    """Unqualified structural evidence only; no phase, castability or win inference."""
    if (
        tree.get("complete") is not True
        or tree.get("combatContextIncluded") is not True
        or tree.get("layout") != CURRENT_IMAGE_SHA256
    ):
        return None
    raw_nodes = tree.get("nodes")
    if not isinstance(raw_nodes, list) or not 1 <= len(raw_nodes) <= MAX_NODES:
        return None
    if any(
        not isinstance(node, dict)
        or "parent" not in node
        or type(node.get("index")) is not int
        or not 0 <= node["index"] < MAX_NODES
        or node.get("parent") is not None
        and type(node["parent"]) is not int
        or not isinstance(node.get("name"), str)
        or len(node["name"]) > 128
        or any(ord(c) < 32 for c in node["name"])
        or type(node.get("visible")) is not bool
        or type(node.get("enabled")) is not bool
        for node in raw_nodes
    ):
        return None
    nodes = {node["index"]: node for node in raw_nodes}
    if len(nodes) != len(raw_nodes):
        return None
    paths = []
    controls = []
    for node in raw_nodes:
        chain, seen = [], set()
        cursor = node
        while cursor is not None:
            if cursor["index"] in seen or len(chain) > MAX_DEPTH:
                return None
            seen.add(cursor["index"])
            chain.append(cursor)
            parent = cursor["parent"]
            if parent is not None and parent not in nodes:
                return None
            cursor = nodes.get(parent)
        path = tuple(n["name"] for n in reversed(chain))
        if not all(n["visible"] is True for n in chain) or any(
            PRIVATE_BRANCH.search(name) or name == "[excluded]" for name in path
        ):
            continue
        paths.append(path)
        if node.get("childrenSkipped") and COMBAT_HAND[: len(path)] == path:
            return None
        kind = combat_kind(path)
        if kind is None:
            continue
        if len(controls) >= MAX_COMBAT_CONTROLS or node.get("childrenSkipped"):
            return None
        rectangle = node.get("rawRectangle")
        controls.append(
            dict(
                index=node["index"],
                parent=node["parent"],
                name=node["name"],
                kind=kind,
                visible=True,
                enabled=all(n["enabled"] is True for n in chain),
                rawRectangle=(
                    rectangle.copy() if valid_raw_rectangle(rectangle) else None
                ),
            )
        )
    if (
        paths.count(("Root", "WorldView")) != 1
        or paths.count(COMBAT_PLANNING) > 1
        or paths.count(COMBAT_HAND) > 1
    ):
        return None
    return dict(
        schemaVersion=1,
        qualification="structural-unqualified",
        geometrySpace="raw-layout-unqualified",
        metadataQualified=False,
        castabilityKnown=False,
        planningVisible=COMBAT_PLANNING in paths,
        handVisible=COMBAT_HAND in paths,
        controls=controls,
        fieldCandidates=tree.get("combatFieldCandidates"),
        duelCandidates=tree.get("duelCandidates"),
    )


def dance_entry_context(tree, observed_ticks):
    """Bounded structural candidates, never selected/approved controls or pixels.

    Visibility/enabled come from the existing header flags and ancestor chain.
    No reviewed selection field is available; selected stays explicitly unknown.
    Static text is allowlisted to the historical energy labels only.
    """
    if (
        tree.get("complete") is not True
        or tree.get("danceEntryContextIncluded") is not True
        or tree.get("layout")
        not in (CURRENT_IMAGE_SHA256, ARCHIVED_LAYOUT_IMAGE_SHA256)
        or type(observed_ticks) is not int
        or observed_ticks <= 0
    ):
        return None
    raw = tree.get("nodes")
    if not isinstance(raw, list) or not 1 <= len(raw) <= MAX_NODES:
        return None
    nodes = {}
    for n in raw:
        if (
            not isinstance(n, dict)
            or type(n.get("index")) is not int
            or not 0 <= n["index"] < MAX_NODES
            or n["index"] in nodes
            or "parent" not in n
            or n["parent"] is not None
            and type(n["parent"]) is not int
            or not isinstance(n.get("name"), str)
            or len(n["name"]) > 128
            or any(ord(c) < 32 for c in n["name"])
            or type(n.get("visible")) is not bool
            or type(n.get("enabled")) is not bool
        ):
            return None
        nodes[n["index"]] = n
    controls = []
    for n in raw:
        chain, seen = [], set()
        cursor = n
        while cursor is not None:
            if cursor["index"] in seen or len(chain) > MAX_DEPTH:
                return None
            seen.add(cursor["index"])
            chain.append(cursor)
            parent = cursor["parent"]
            if parent is not None and parent not in nodes:
                return None
            cursor = nodes.get(parent)
        path = tuple(item["name"] for item in reversed(chain))
        if path[:3] != DANCE_ENTRY_ROOT or any(
            PRIVATE_BRANCH.search(name) or name == "[excluded]" for name in path
        ):
            continue
        if len(controls) >= MAX_DANCE_ENTRY_CONTROLS or n.get("childrenSkipped"):
            return None
        text = n.get("text") if path in DANCE_ENTRY_TEXT else None
        if text is not None and (not isinstance(text, str) or len(text) > 160):
            return None
        rect = n.get("rawRectangle")
        controls.append(
            dict(
                index=n["index"],
                parent=n["parent"],
                path=list(path),
                name=n["name"],
                text=text,
                visible=all(item["visible"] for item in chain),
                enabled=all(item["enabled"] for item in chain),
                rawRectangle=rect.copy() if valid_raw_rectangle(rect) else None,
                selected=None,
                selectionReason="selection-field-unqualified",
            )
        )
    if sum(control["path"] == list(DANCE_ENTRY_ROOT) for control in controls) > 1:
        return None
    return dict(
        schemaVersion=1,
        observedUtcTicks=observed_ticks,
        qualification="structural-unqualified",
        geometrySpace="raw-parent-layout-unqualified",
        controls=controls,
    )


def quest_context(tree):
    """Projection only; presence/enabled is not approval to advance a quest."""
    if (
        not tree.get("complete")
        or not tree.get("questContextIncluded")
        or tree.get("layout") != CURRENT_IMAGE_SHA256
    ):
        return None
    nodes = {node["index"]: node for node in tree["nodes"]}

    def matches(path):
        found = []
        for node in nodes.values():
            chain, seen = [], set()
            cursor = node
            while (
                cursor is not None
                and cursor["index"] not in seen
                and len(chain) <= MAX_DEPTH
            ):
                seen.add(cursor["index"])
                chain.append(cursor)
                cursor = nodes.get(cursor["parent"])
            if tuple(n["name"] for n in reversed(chain)) == path and all(
                n["visible"] for n in chain
            ):
                found.append((node, all(n["enabled"] for n in chain)))
        return found[0] if len(found) == 1 else None

    def text(path, limit):
        match = matches(path)
        raw = match[0].get("text") if match else None
        if not isinstance(raw, str) or len(raw) > limit:
            return None
        value = " ".join(html.unescape(re.sub(r"<[^>]*>", "", raw)).split())
        return (
            value
            if value and len(value) <= limit and not any(ord(c) < 32 for c in value)
            else None
        )

    advance = matches(QUEST_ADVANCE)
    return dict(
        objectiveText=text(QUEST_OBJECTIVE, 256),
        dialogueText=text(QUEST_DIALOGUE, 2048),
        advanceEnabled=advance[1] if advance else None,
    )


def interaction_title(tree):
    """Source-backed title candidate, not permission to interact or a menu outcome."""
    if not tree.get("complete") or tree.get("layout") not in (
        CURRENT_IMAGE_SHA256,
        ARCHIVED_LAYOUT_IMAGE_SHA256,
    ):
        return None
    raw_nodes = tree.get("nodes", [])
    if not raw_nodes or len(raw_nodes) > MAX_NODES:
        return None
    nodes = {node["index"]: node for node in raw_nodes}
    if len(nodes) != len(raw_nodes):
        return None
    matches = []
    for node in raw_nodes:
        chain, seen = [], set()
        cursor = node
        while (
            cursor is not None
            and cursor["index"] not in seen
            and len(chain) <= MAX_DEPTH
        ):
            seen.add(cursor["index"])
            chain.append(cursor)
            if cursor["parent"] is None:
                break
            cursor = nodes.get(cursor["parent"])
        if (
            chain[-1]["parent"] is None
            and tuple(n["name"] for n in reversed(chain)) == INTERACTION_TITLE
            and all(
                n.get("visible") is True and n.get("enabled") is True for n in chain
            )
        ):
            matches.append(node)
    if len(matches) != 1:
        return None
    raw = matches[0].get("text")
    if not isinstance(raw, str) or len(raw) > 160 or any(ord(c) < 32 for c in raw):
        return None
    decoded = html.unescape(re.sub(r"<[^>]*>", "", raw))
    if any(ord(c) < 32 for c in decoded):
        return None
    value = " ".join(decoded.split())
    return value if value and len(value) <= 160 else None


def classify(tree):
    values = dict.fromkeys(STATES)
    if not tree.get("complete"):
        return values
    visible = [node for node in tree["nodes"] if node["visible"]]
    worlds = {node["index"] for node in visible if node["name"] == "WorldView"}
    if not worlds:
        return values
    children = [node for node in visible if node["parent"] in worlds]
    names = {node["name"] for node in children}
    values["interactionPrompt"] = "NPCRangeWin" in names
    values["dialogue"] = bool(names & {"wndDialogMain", "NPCServicesWin"})
    settings = {
        node["index"] for node in children if node["name"] == "DeckConfiguration"
    }
    values["menu"] = bool(names & {"shopGUI", "PayToPlay", "NPCServicesWin"}) or any(
        node["name"] in {"SettingPage", "SettingsPage"} and node["parent"] in settings
        for node in visible
    )
    # Spiral Map can remain open underneath native Idle; Escape is not Exit.
    # Require the observed control set, rather than treating unknown panels as outside.
    visible_names = {node["name"] for node in visible}
    values["menu"] |= {
        "spiralScene",
        "teleportButton",
        "cancelButton",
        "optionWindow",
    } <= visible_names
    # Current-client Idle departure/return was user-coordinated and recorded.
    # Only this qualified layout may turn an absent overlay into a negative.
    by_index = {node["index"]: node for node in tree["nodes"]}
    roots = {
        node["index"]
        for node in visible
        if node["name"] == "Root" and node["parent"] is None
    }
    if (
        tree.get("layout") in (CURRENT_IMAGE_SHA256, ARCHIVED_LAYOUT_IMAGE_SHA256)
        and roots
        and not any(
            node["parent"] in roots and node["name"] == "idleMessage"
            for node in visible
        )
    ):
        values["nativeIdle"] = False

    def path(node):
        names = []
        while node is not None and len(names) <= MAX_DEPTH:
            names.append(node["name"])
            node = by_index.get(node["parent"])
        return tuple(reversed(names))

    for node in visible:
        text = re.sub(r"<[^>]*>", "", node.get("text") or "").strip().upper()
        if (
            path(node) == ("Root", "idleMessage", "idleBackground", "idleText")
            and text == "YOU ARE IDLE"
        ):
            values["nativeIdle"] = True
    return values


async def observe(request, factory, identity, stop, emit, prepare=lambda: None):
    validate_request(request)
    started = time.monotonic()
    result = dict(
        schemaVersion=1,
        runId=request["runId"],
        operation=VERSION,
        sourceSha256=request["sourceSha256"],
        target=request["target"],
        status="unavailable",
        code="not-started",
        loading=None,
        states=dict.fromkeys(STATES),
        observedUtcTicks=0,
        statesObservedUtcTicks=0,
        durationMs=0,
        cleanupConfirmed=True,
        hookRestored=False,
        loadingSamples=0,
        treeSamples=0,
        treeFailures=0,
        stableStates=dict.fromkeys(STATES),
        questContext=None,
        stableQuestContext=None,
        interactionTitle=None,
        stableInteractionTitle=None,
        combatContext=None,
        danceEntryContext=None,
    )
    client = None
    previous = None
    previous_quest = None
    previous_title = None
    previous_title_stamp = 0
    latest_loading = None
    latest_loading_at = float("-inf")
    try:
        if stop():
            result.update(status="cancelled", code="cancelled-before-open")
            return result
        if not identity(request["target"]):
            raise ValueError("Target changed")
        prepare()
        if (
            stop()
            or not identity(request["target"])
            or time.monotonic() - started >= request["timeoutSeconds"]
        ):
            raise ValueError("Preparation no longer current")
        result["cleanupConfirmed"] = False
        client = factory(request["target"])
        if request.get("includePoseContext", False):

            async def pose_window_ready():
                await emit(
                    dict(
                        kind="pose-window-ready",
                        observedUtcTicks=common.utc_ticks(),
                        safe=result.get("loading") is False
                        and all(
                            result.get("states", {}).get(name) is False
                            for name in ("nativeIdle", "menu", "dialogue")
                        ),
                    )
                )

            client.pose_window_ready = pose_window_ready
            client.moving_pose_seconds = request.get("movingPoseSeconds", 0)
            client.pose_stream_id = request.get("poseStreamId")
            client.pose_stream_emit = emit
            client.pose_stream_binding = dict(
                runId=request["runId"], target=dict(request["target"])
            )
            client.pose_stream_current = lambda: not stop() and identity(
                request["target"]
            )
        await client.open()
        if time.monotonic() - started >= request["timeoutSeconds"]:
            raise TimeoutError("Setup budget exceeded")
        await emit(
            dict(kind="ready", observedUtcTicks=common.utc_ticks(), inputEnabled=False)
        )
        schedule = ReadSchedule(time.monotonic())
        end = time.monotonic() + request["durationSeconds"]
        while time.monotonic() < end:
            if stop():
                result.update(status="cancelled", code="cancelled")
                break
            if not identity(request["target"]):
                raise ValueError("Target changed")
            now = time.monotonic()
            kind = schedule.take(now)
            if kind is None:
                await asyncio.sleep(0.025)
                continue
            if kind == "loading":
                try:
                    value = await asyncio.wait_for(client.loading(), 1.5)
                    if type(value) is not bool:
                        raise ValueError("Unknown loading")
                    latest_loading = value
                    latest_loading_at = time.monotonic()
                    result.update(loading=value, observedUtcTicks=common.utc_ticks())
                    result["loadingSamples"] += 1
                except Exception:
                    latest_loading = None
                    result["loading"] = None
                if latest_loading is not False:
                    previous = None
                    previous_quest = None
                    previous_title = None
                    previous_title_stamp = 0
                    result.update(
                        states=dict.fromkeys(STATES),
                        stableStates=dict.fromkeys(STATES),
                        statesObservedUtcTicks=0,
                        questContext=None,
                        stableQuestContext=None,
                        interactionTitle=None,
                        stableInteractionTitle=None,
                        combatContext=None,
                        danceEntryContext=None,
                    )
                await emit(
                    dict(
                        kind="loading",
                        observedUtcTicks=common.utc_ticks(),
                        loading=latest_loading,
                        **(
                            {"combatContext": None, "danceEntryContext": None}
                            if latest_loading is not False
                            else {}
                        ),
                    )
                )
                continue
            try:
                if latest_loading is not False or now - latest_loading_at > 0.5:
                    raise ValueError("Loading or unavailable")
                # Age evidence from acquisition START, not from the end of a slow
                # tree read or its loading check. Disconnect cannot renew facts.
                stamp = common.utc_ticks()
                tree = await asyncio.wait_for(client.tree(), 1.5)
                # A separate post-read check rejects a tree collected across loading.
                after = await asyncio.wait_for(client.loading(), 1.5)
                latest_loading, latest_loading_at = after, time.monotonic()
                if after is not False or not identity(request["target"]) or stop():
                    raise ValueError("UI state changed during read")
                values = classify(tree)
                stable = {
                    key: (
                        value
                        if previous is not None and previous[key] == value
                        else None
                    )
                    for key, value in values.items()
                }
                previous = values
                quest = (
                    quest_context(tree) if request.get("includeQuestContext") else None
                )
                stable_quest = (
                    quest if quest is not None and quest == previous_quest else None
                )
                previous_quest = quest
                title = interaction_title(tree)
                stable_title = (
                    title
                    if title is not None
                    and title == previous_title
                    and 0 < stamp - previous_title_stamp <= 20_000_000
                    else None
                )
                previous_title, previous_title_stamp = title, stamp
                combat = (
                    combat_context(tree)
                    if request.get("includeCombatContext")
                    else None
                )
                dance_entry = (
                    dance_entry_context(tree, stamp)
                    if request.get("includeDanceEntryContext")
                    else None
                )
                result.update(
                    states=values,
                    stableStates=stable,
                    observedUtcTicks=common.utc_ticks(),
                    statesObservedUtcTicks=stamp,
                    loading=False,
                    questContext=quest,
                    stableQuestContext=stable_quest,
                    interactionTitle=title,
                    stableInteractionTitle=stable_title,
                    combatContext=combat,
                    danceEntryContext=dance_entry,
                )
                result["treeSamples"] += 1
                await emit(
                    dict(
                        kind="tree",
                        observedUtcTicks=result["observedUtcTicks"],
                        statesObservedUtcTicks=stamp,
                        states=values,
                        stableStates=stable,
                        questContext=quest,
                        stableQuestContext=stable_quest,
                        interactionTitle=title,
                        stableInteractionTitle=stable_title,
                        combatContext=combat,
                        danceEntryContext=dance_entry,
                        **tree,
                    )
                )
                # Owned snapshots need two independently sampled trees, not the
                # unused tail of a watch. Keep acquisition timestamps, disagreement
                # and cleanup unchanged; duration remains the maximum budget.
                if (
                    request.get("stopAfterTwoTrees", False)
                    or request.get("includeResourceContext", False)
                ) and result["treeSamples"] >= 2:
                    result.update(
                        status="observed", code="bounded-ui-snapshot-finished"
                    )
                    break
            except Exception as tree_error:
                previous = None
                previous_quest = None
                previous_title = None
                previous_title_stamp = 0
                result["treeFailures"] += 1
                result.update(
                    states=dict.fromkeys(STATES),
                    stableStates=dict.fromkeys(STATES),
                    statesObservedUtcTicks=0,
                    questContext=None,
                    stableQuestContext=None,
                    interactionTitle=None,
                    stableInteractionTitle=None,
                    combatContext=None,
                    danceEntryContext=None,
                )
                await emit(
                    dict(
                        kind="tree-unavailable",
                        failureCode=(
                            str(tree_error)
                            if isinstance(tree_error, ValueError)
                            and str(tree_error)
                            in {
                                "UI node budget exceeded",
                                "UI byte budget exceeded",
                                "UI cycle or depth limit",
                                "UI name limit",
                                "UI children vector invalid",
                                "UI changed during observation",
                                "Invalid UI name",
                                "Short UI read",
                            }
                            else type(tree_error).__name__
                        ),
                        observedUtcTicks=common.utc_ticks(),
                        states=result["states"],
                        combatContext=None,
                        danceEntryContext=None,
                    )
                )
        else:
            result.update(
                status="observed" if result["treeSamples"] else "unavailable",
                code="bounded-ui-watch-finished",
            )
    except Exception as error:
        result.update(
            status="unavailable",
            code=(
                "dependency-unavailable"
                if isinstance(error, common.DependencyUnavailable)
                else (
                    error.code
                    if isinstance(error, UiCompatibilityError)
                    else "ui-observation-unavailable"
                )
            ),
            loading=None,
            states=dict.fromkeys(STATES),
            stableStates=dict.fromkeys(STATES),
            statesObservedUtcTicks=0,
            questContext=None,
            stableQuestContext=None,
            interactionTitle=None,
            stableInteractionTitle=None,
            combatContext=None,
            danceEntryContext=None,
        )
    finally:
        if client is not None:
            try:
                if (
                    request.get("includePoseContext", False)
                    and result["status"] == "observed"
                    and result.get("loading") is False
                    and not stop()
                    and identity(request["target"])
                ):
                    try:
                        result["poseCandidate"] = await asyncio.wait_for(
                            client.pose(), 3 + request.get("movingPoseSeconds", 0)
                        )
                    except Exception as error:
                        result["poseCandidate"] = None
                        result["poseFailure"] = {
                            "stage": getattr(client, "pose_stage", "unknown"),
                            "kind": (
                                type(error).__name__
                                if type(error).__name__
                                in {
                                    "ValueError",
                                    "TimeoutError",
                                    "ModuleNotFoundError",
                                    "AttributeError",
                                }
                                else "observation-failed"
                            ),
                        }
                if (
                    request.get("includeResourceContext", False)
                    and result["status"] == "observed"
                    and result.get("loading") is False
                    and not stop()
                    and identity(request["target"])
                ):
                    try:
                        result["resourceCandidate"] = await asyncio.wait_for(
                            client.resources(), 3
                        )
                        if (
                            await asyncio.wait_for(client.loading(), 1) is False
                            and not stop()
                            and identity(request["target"])
                        ):
                            result["resources"] = qualify_resources(
                                result["resourceCandidate"],
                                request["target"]["imageSha256"],
                                result.get("statesObservedUtcTicks", 0),
                            )
                        else:
                            result.update(
                                status="unavailable",
                                code="resource-context-changed",
                                resources=None,
                                loading=None,
                                states=dict.fromkeys(STATES),
                                stableStates=dict.fromkeys(STATES),
                                statesObservedUtcTicks=0,
                                questContext=None,
                                stableQuestContext=None,
                                interactionTitle=None,
                                stableInteractionTitle=None,
                                combatContext=None,
                                danceEntryContext=None,
                            )
                    except Exception:
                        result.update(
                            status="unavailable",
                            code="resource-observation-unavailable",
                            resources=None,
                        )
                        result["resourceCandidate"] = dict(
                            status="unknown", reason="stats-candidate-unavailable"
                        )
                await asyncio.wait_for(client.close(), 3)
                result.update(cleanupConfirmed=True, hookRestored=True)
                if stop():
                    result.update(status="cancelled", code="cancelled", resources=None)
            except Exception:
                result.update(
                    status="cleanup_failed",
                    code="ui-restoration-unconfirmed",
                    loading=None,
                    states=dict.fromkeys(STATES),
                    stableStates=dict.fromkeys(STATES),
                    statesObservedUtcTicks=0,
                    questContext=None,
                    stableQuestContext=None,
                    interactionTitle=None,
                    stableInteractionTitle=None,
                    combatContext=None,
                    danceEntryContext=None,
                )
        result["durationMs"] = int((time.monotonic() - started) * 1000)
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--directory", required=True)
    directory = Path(parser.parse_args().directory).resolve()
    raw = sys.stdin.buffer.readline(32769)
    if len(raw) > 32768:
        raise ValueError("Request exceeds budget")
    request = validate_request(json.loads(raw))
    with (directory / "ui-observations.jsonl").open("x", encoding="utf-8") as stream:
        written = 0

        async def emit(value):
            nonlocal written
            line = json.dumps(value, allow_nan=False) + "\n"
            written += len(line.encode("utf-8"))
            if written > MAX_TRACE_BYTES:
                raise ValueError("Trace budget exceeded")
            stream.write(line)
            stream.flush()

        async def run():
            asyncio.get_running_loop().set_default_executor(
                ThreadPoolExecutor(max_workers=1)
            )
            return await observe(
                request,
                lambda target: UiClient(
                    target,
                    request.get("includeQuestContext", False),
                    request.get("includeCombatContext", False),
                    request.get("includeResourceContext", False),
                    request.get("includeDanceEntryContext", False),
                ),
                common.current_target,
                lambda: (directory / "stop.request").exists(),
                emit,
                lambda: common.prepare_backend(request, directory),
            )

        result = asyncio.run(run())
    with (directory / "result.json").open("x", encoding="utf-8") as stream:
        json.dump(result, stream, allow_nan=False)


if __name__ == "__main__":
    main()
