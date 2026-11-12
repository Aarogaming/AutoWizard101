"""Synthetic UI memory and hook lifecycle only. Never attach to a real process."""

import asyncio
import copy
import hashlib
import json
from pathlib import Path
import struct
import sys
import unittest
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import w101_ui_tree as ui


def request():
    return dict(
        schemaVersion=1,
        runId="a" * 32,
        operation=ui.VERSION,
        enabled=True,
        networkAllowed=False,
        uiTreeEnabled=True,
        durationSeconds=1,
        sourceRoot="unused",
        sourceSha256="b" * 64,
        timeoutSeconds=8,
        target=dict(
            handle=42,
            processId=123,
            startedUtcTicks=1,
            processName="WizardGraphicalClient",
            imagePath="unused",
            imageSha256="c" * 64,
        ),
    )


class Memory:
    def __init__(self):
        self.bytes = {}
        self.next = 0x10000
        self.reads = []

    def allocate(self, size=1024):
        address = self.next
        self.next += (size + 15) // 16 * 16
        self.put(address, bytes(size))
        return address

    def put(self, address, value):
        self.bytes.update({address + i: b for i, b in enumerate(value)})

    async def read(self, address, size):
        self.reads.append((address, size))
        return bytes(self.bytes[address + i] for i in range(size))

    def node(
        self, name, children=(), flags=1, text=None, text_offset=584, rectangle=None
    ):
        addr = self.allocate()
        encoded = name.encode()
        self.put(addr + 96, struct.pack("<Q", len(encoded)))
        if len(encoded) < 16:
            self.put(addr + 80, encoded)
        else:
            data = self.allocate(len(encoded))
            self.put(data, encoded)
            self.put(addr + 80, struct.pack("<Q", data))
        self.put(addr + 156, struct.pack("<I", flags))
        if rectangle is not None:
            self.put(addr + 160, struct.pack("<iiii", *rectangle))
        if children:
            data = self.allocate(len(children) * 16)
            self.put(data, b"".join(struct.pack("<QQ", c, 0) for c in children))
            self.put(addr + 112, struct.pack("<QQ", data, data + len(children) * 16))
        if text is not None:
            encoded = text.encode("utf-16-le")
            self.put(addr + text_offset + 16, struct.pack("<Q", len(text)))
            if len(encoded) < 8:
                self.put(addr + text_offset, encoded)
            else:
                data = self.allocate(len(encoded))
                self.put(data, encoded)
                self.put(addr + text_offset, struct.pack("<Q", data))
        return addr


def quest_memory(
    *, dialogue="Go speak to the guard.", objective="Talk to Quinn", flags=1
):
    m = Memory()

    def text(name, value):
        node = m.node(name, text=value, text_offset=712)
        function, vtable, control_type = m.allocate(), m.allocate(), m.allocate()
        m.put(control_type, b"ControlText\0")
        m.put(
            function + 63,
            bytes.fromhex("48 8d 0d") + struct.pack("<i", control_type - function - 70),
        )
        m.put(vtable, struct.pack("<Q", function))
        m.put(node, struct.pack("<Q", vtable))
        return node

    body, goal = text("txtMessage", dialogue), text("txtGoalName", objective)
    button = m.node("btnRight")
    secret = text("txtMessage", "Unrelated text must not be read")
    root = m.node(
        "Root",
        [
            m.node(
                "WorldView",
                [
                    m.node(
                        "wndDialogMain",
                        [m.node("txtArea", [body]), button],
                        flags=flags,
                    ),
                    m.node(
                        "windowHUD",
                        [
                            m.node(
                                "QuestHelperHud",
                                [m.node("ElementWindow", [m.node("", [goal])])],
                            ),
                            m.node("OtherHUD", [secret]),
                        ],
                    ),
                    m.node("ChatWindow", [text("txtMessage", "Private content")]),
                ],
            )
        ],
    )
    return m, root, body, goal, button, secret


def combat_memory(*, hand_flags=1, card_count=1, rectangle=(10, 20, 30, 60)):
    m = Memory()
    cards = [m.node(f"Card{i}", rectangle=rectangle) for i in range(card_count)]
    hand = m.node("Hand", cards, flags=hand_flags, rectangle=(1, 2, 300, 90))
    selection = m.node(
        "SpellSelection", [hand, m.node("Focus", rectangle=(5, 5, 15, 15))]
    )
    planning = m.node(
        "PlanningPhase",
        [m.node("Alignment", [m.node("PlanningPhaseSubWindow", [selection])])],
    )
    name = m.node("Name", text="This text must not be read")
    health = m.node("Health", text="111/222")
    combatant = m.node(
        "CombatantControl", [name, health], rectangle=(100, 100, 250, 160)
    )
    root = m.node(
        "Root",
        [
            m.node(
                "WorldView",
                [
                    planning,
                    combatant,
                    m.node("DoneWindow", [m.node("DefeatedPassButton")]),
                    m.node("OtherMenu", [m.node("Hand"), m.node("Focus")]),
                    m.node(
                        "ChatWindow",
                        [
                            m.node(
                                "CombatantControl", [m.node("Health", text="Private")]
                            )
                        ],
                    ),
                ],
            )
        ],
    )
    return m, root, hand, cards, name, health


class CombatTreeTests(unittest.IsolatedAsyncioTestCase):
    async def test_rooted_fields_are_opt_in_typed_and_never_expose_bindings(self):
        m, root, _, cards, *_ = combat_memory()
        # Type only the real hand card; lookalikes/private controls have no type.
        function, vtable, name = m.allocate(), m.allocate(), m.allocate()
        m.put(name, b"SpellCheckBox\0")
        m.put(
            function + 63,
            bytes.fromhex("48 8d 0d") + struct.pack("<i", name - function - 70),
        )
        m.put(vtable, struct.pack("<Q", function))
        m.put(cards[0], struct.pack("<Q", vtable))
        calls = []

        async def fields(read, bindings, *, layout_id):
            calls.append((bindings, layout_id))
            return {"qualification": "source-layout-only", "metadataQualified": False}

        fake = SimpleNamespace(
            ControlBinding=lambda *args: args, read_combat_fields=fields
        )
        await ui.snapshot(m.read, root, current_layout=True, combat_reader=fake)
        self.assertEqual([], calls)
        tree = await ui.snapshot(
            m.read,
            root,
            current_layout=True,
            include_combat_context=True,
            combat_reader=fake,
        )
        self.assertEqual(1, len(calls))
        self.assertEqual("deimos-development-df2283f", calls[0][1])
        self.assertEqual(1, len(calls[0][0]))
        self.assertEqual((cards[0], "card"), calls[0][0][0][1:])
        self.assertEqual(
            False, ui.combat_context(tree)["fieldCandidates"]["metadataQualified"]
        )
        self.assertNotIn(str(cards[0]), json.dumps(tree))

    async def test_duel_binding_requires_exact_image_size_and_instruction(self):
        base = 0x10000000
        reads = []

        async def read(address, count):
            reads.append((address, count))
            return (
                bytes.fromhex("48 8b 0d 47 63 55 02")
                if count == 7
                else struct.pack("<Q", 0x12340000)
            )

        self.assertEqual(
            (base + 53626784, 0x12340000),
            await ui.current_duel_manager(read, base, 57729024),
        )
        self.assertEqual(2, len(reads))
        with self.assertRaises(ValueError):
            await ui.current_duel_manager(read, base, 57729025)

        async def changed(address, count):
            return b"changed"

        with self.assertRaises(ValueError):
            await ui.current_duel_manager(changed, base, 57729024)

    def test_combat_decoder_files_match_reviewed_pins(self):
        self.assertEqual("w101-combat-fields-v1", ui.combat_reader_module().VERSION)
        self.assertEqual(
            "deimos-development-df2283f", ui.duel_reader_module().LAYOUT_ID
        )

    async def tree(self, **changes):
        m, root, *rest = combat_memory(**changes)
        value = await ui.snapshot(
            m.read, root, current_layout=True, include_combat_context=True
        )
        return m, value, rest

    async def test_opt_in_reuses_header_without_reading_combat_text_or_metadata(self):
        m, root, _, _, name, health = combat_memory()
        default = await ui.snapshot(m.read, root, current_layout=True)
        default_reads = m.reads.copy()
        self.assertIsNone(ui.combat_context(default))
        self.assertFalse(any("rawRectangle" in n for n in default["nodes"]))
        m.reads.clear()
        tree = await ui.snapshot(
            m.read, root, current_layout=True, include_combat_context=True
        )
        self.assertEqual(default_reads, m.reads)
        self.assertEqual(default["readBytes"], tree["readBytes"])
        self.assertNotIn((name + 584, 24), m.reads)
        self.assertNotIn((health + 584, 24), m.reads)
        context = ui.combat_context(tree)
        self.assertEqual("structural-unqualified", context["qualification"])
        self.assertEqual("raw-layout-unqualified", context["geometrySpace"])
        self.assertFalse(context["metadataQualified"])
        self.assertFalse(context["castabilityKnown"])
        self.assertTrue(context["planningVisible"])
        self.assertTrue(context["handVisible"])
        self.assertEqual(
            [10, 20, 30, 60],
            next(c for c in context["controls"] if c["name"] == "Card0")[
                "rawRectangle"
            ],
        )
        self.assertNotIn("This text", json.dumps(context))
        self.assertNotIn("Private", json.dumps(context))
        self.assertEqual(1, sum(c["name"] == "Hand" for c in context["controls"]))
        self.assertEqual(1, sum(c["name"] == "Focus" for c in context["controls"]))
        self.assertNotIn("text", context["controls"][0])

    async def test_hidden_and_disabled_ancestors_do_not_establish_actionability(self):
        _, hidden, _ = await self.tree(hand_flags=0)
        self.assertFalse(ui.combat_context(hidden)["handVisible"])
        self.assertFalse(
            any(c["name"] == "Card0" for c in ui.combat_context(hidden)["controls"])
        )
        _, disabled, _ = await self.tree(hand_flags=0x80000001)
        card = next(
            c for c in ui.combat_context(disabled)["controls"] if c["name"] == "Card0"
        )
        self.assertFalse(card["enabled"])
        self.assertFalse(ui.combat_context(disabled)["castabilityKnown"])

    async def test_invalid_geometry_is_unknown_not_clipped_or_defaulted(self):
        for rectangle in ((0, 0, 0, 0), (10, 20, 5, 30), (-70000, 0, 10, 30)):
            _, tree, _ = await self.tree(rectangle=rectangle)
            card = next(
                c for c in ui.combat_context(tree)["controls"] if c["name"] == "Card0"
            )
            self.assertIsNone(card["rawRectangle"])

    async def test_unqualified_partial_ambiguous_or_malformed_projection_is_unknown(
        self,
    ):
        _, tree, _ = await self.tree()
        for change in (
            dict(complete=False),
            dict(layout="legacy-unqualified"),
            dict(combatContextIncluded=False),
        ):
            self.assertIsNone(ui.combat_context(tree | change))
        for fault in (
            "duplicate",
            "orphan",
            "cycle",
            "name",
            "visible",
            "ambiguous",
            "missing-parent",
            "skipped-world",
        ):
            altered = copy.deepcopy(tree)
            nodes = altered["nodes"]
            if fault == "duplicate":
                nodes.append(nodes[-1].copy())
            elif fault == "orphan":
                nodes[-1]["parent"] = 999
            elif fault == "cycle":
                nodes[0]["parent"] = nodes[-1]["index"]
            elif fault == "name":
                nodes[-1]["name"] = "x" * 129
            elif fault == "visible":
                nodes[-1]["visible"] = 1
            elif fault == "missing-parent":
                nodes[-1].pop("parent")
            elif fault == "skipped-world":
                next(n for n in nodes if n["name"] == "WorldView")[
                    "childrenSkipped"
                ] = True
            else:
                duplicate = next(
                    n for n in nodes if n["name"] == "PlanningPhase"
                ).copy()
                duplicate["index"] = len(nodes)
                nodes.append(duplicate)
            self.assertIsNone(ui.combat_context(altered), fault)

    async def test_control_budget_does_not_return_truncated_hand(self):
        _, tree, _ = await self.tree(card_count=64)
        self.assertIsNone(ui.combat_context(tree))

    async def test_no_world_view_is_unknown_and_controls_are_not_pointer_tokens(self):
        m = Memory()
        root = m.node("Root", [m.node("PlanningPhase")])
        tree = await ui.snapshot(
            m.read, root, current_layout=True, include_combat_context=True
        )
        self.assertIsNone(ui.combat_context(tree))
        _, valid, _ = await self.tree()
        self.assertTrue(
            all(c["index"] < ui.MAX_NODES for c in ui.combat_context(valid)["controls"])
        )


class QuestTreeTests(unittest.IsolatedAsyncioTestCase):
    def test_live_quinn_opening_replays_from_frozen_source_evidence(self):
        fixture = json.loads(
            (
                Path(__file__).resolve().parents[1]
                / "fixtures/w101_quest_context_live_20261110.json"
            ).read_text()
        )
        self.assertEqual(64, len(fixture["sourceTraceSha256"]))
        self.assertEqual(fixture["expected"], ui.quest_context(fixture["tree"]))
        self.assertTrue(ui.classify(fixture["tree"])["dialogue"])

    async def test_default_does_not_read_quest_text_and_opt_in_reads_only_exact_paths(
        self,
    ):
        m, root, body, goal, _, secret = quest_memory(
            dialogue="<center>Go &amp; speak</center>"
        )
        default = await ui.snapshot(m.read, root, current_layout=True)
        self.assertIsNone(ui.quest_context(default))
        self.assertNotIn((body + 712, 24), m.reads)
        self.assertNotIn((goal + 712, 24), m.reads)
        tree = await ui.snapshot(
            m.read, root, current_layout=True, include_quest_context=True
        )
        self.assertEqual(
            dict(
                objectiveText="Talk to Quinn",
                dialogueText="Go & speak",
                advanceEnabled=True,
            ),
            ui.quest_context(tree),
        )
        self.assertNotIn((secret + 712, 24), m.reads)
        self.assertNotIn("Private content", json.dumps(tree))
        self.assertNotIn("Unrelated text", json.dumps(tree))
        self.assertLess(tree["readBytes"], 10000)

    async def test_text_over_budget_is_unknown_not_a_truncated_match(self):
        m, root, *_ = quest_memory(dialogue="D" * 2049, objective="O" * 257)
        tree = await ui.snapshot(
            m.read, root, current_layout=True, include_quest_context=True
        )
        self.assertEqual(
            dict(objectiveText=None, dialogueText=None, advanceEnabled=True),
            ui.quest_context(tree),
        )

    async def test_hidden_or_disabled_dialogue_cannot_offer_advance(self):
        for flags, expected in ((0, None), (0x80000001, False)):
            m, root, *_ = quest_memory(flags=flags)
            tree = await ui.snapshot(
                m.read, root, current_layout=True, include_quest_context=True
            )
            self.assertEqual(expected, ui.quest_context(tree)["advanceEnabled"])

    async def test_ambiguous_control_and_unqualified_layout_are_unknown(self):
        m, root, *_ = quest_memory()
        tree = await ui.snapshot(
            m.read, root, current_layout=True, include_quest_context=True
        )
        button = next(n for n in tree["nodes"] if n["name"] == "btnRight")
        tree["nodes"].append(button | {"index": 100})
        self.assertIsNone(ui.quest_context(tree)["advanceEnabled"])
        tree["layout"] = "unreviewed"
        self.assertIsNone(ui.quest_context(tree))


def title_tree(text="Dance Game"):
    return dict(
        complete=True,
        layout=ui.CURRENT_IMAGE_SHA256,
        nodes=[
            dict(
                index=i,
                parent=i - 1 if i else None,
                name=name,
                visible=True,
                enabled=True,
                text=text if i == 4 else None,
            )
            for i, name in enumerate(ui.INTERACTION_TITLE)
        ],
    )


class InteractionTitleTests(unittest.IsolatedAsyncioTestCase):
    def test_exact_visible_title_is_normalized_without_authorizing_action(self):
        self.assertEqual(
            "Dance & Game",
            ui.interaction_title(title_tree("<center>Dance &amp; Game</center>")),
        )

    def test_wrong_path_hidden_disabled_duplicate_and_unreviewed_are_unknown(self):
        for change in (
            "path",
            "hidden",
            "disabled",
            "duplicate",
            "layout",
            "incomplete",
            "cycle",
        ):
            with self.subTest(change=change):
                tree = title_tree()
                if change == "path":
                    tree["nodes"][3]["name"] = "Other"
                if change == "hidden":
                    tree["nodes"][1]["visible"] = False
                if change == "disabled":
                    tree["nodes"][2]["enabled"] = False
                if change == "duplicate":
                    tree["nodes"].append(tree["nodes"][-1] | {"index": 6})
                if change == "layout":
                    tree["layout"] = "unknown"
                if change == "incomplete":
                    tree["complete"] = False
                if change == "cycle":
                    tree["nodes"][0]["parent"] = 4
                self.assertIsNone(ui.interaction_title(tree))

    def test_unbounded_empty_or_control_text_is_unknown(self):
        for text in (None, "", " ", "x" * 161, "Dance\nGame", "Dance&#10;Game"):
            self.assertIsNone(ui.interaction_title(title_tree(text)))

    async def test_text_read_does_not_follow_a_similar_noncanonical_branch(self):
        m = Memory()
        title = m.node("NPCRangeTxtTitle", text="Do not read")
        root = m.node(
            "Root",
            [m.node("WorldView", [m.node("NPCRangeWin", [m.node("Other", [title])])])],
        )
        await ui.snapshot(m.read, root)
        self.assertNotIn((title + 584, 24), m.reads)

    async def test_stability_requires_successive_matching_current_titles(self):
        client = Client()
        events = []

        async def tree():
            return title_tree()

        async def emit(event):
            events.append(event)

        client.tree = tree
        result = await ui.observe(
            request() | {"durationSeconds": 2},
            lambda _: client,
            lambda _: True,
            lambda: False,
            emit,
        )
        readings = [e for e in events if e["kind"] == "tree"]
        self.assertIsNone(readings[0]["stableInteractionTitle"])
        self.assertEqual("Dance Game", result["interactionTitle"])
        self.assertEqual("Dance Game", result["stableInteractionTitle"])

    async def test_changed_title_or_failed_tree_cannot_retain_stability(self):
        for fails in (False, True):
            client = Client()
            reads = 0

            async def tree():
                nonlocal reads
                reads += 1
                if reads > 1 and fails:
                    raise ValueError("Synthetic unavailable tree")
                return title_tree("Dance Game" if reads == 1 else "Milo Barker")

            async def emit(event):
                pass

            client.tree = tree
            result = await ui.observe(
                request() | {"durationSeconds": 2},
                lambda _: client,
                lambda _: True,
                lambda: False,
                emit,
            )
            self.assertEqual(
                None if fails else "Milo Barker", result["interactionTitle"]
            )
            self.assertIsNone(result["stableInteractionTitle"])

    async def test_loading_or_cleanup_failure_clears_title(self):
        for cleanup in (False, True):
            client = Client(fail_close=cleanup)

            async def tree():
                return title_tree()

            async def emit(event):
                if event["kind"] == "tree" and not cleanup:
                    client.value = True

            client.tree = tree
            result = await ui.observe(
                request(), lambda _: client, lambda _: True, lambda: False, emit
            )
            self.assertIsNone(result["interactionTitle"])
            self.assertIsNone(result["stableInteractionTitle"])


class TreeTests(unittest.IsolatedAsyncioTestCase):
    async def test_selection_banner_text_is_path_scoped(self):
        m, _, name, _, _, _ = quest_memory(dialogue="Luke SkyWalker")
        m.put(name + 80, b"txtName")
        m.put(name + 96, struct.pack("<Q", 7))
        root = m.node(
            "Root",
            [
                m.node(
                    "WorldView", [m.node("mainWindow", [m.node("sprBanner", [name])])]
                )
            ],
        )
        tree = await ui.snapshot(m.read, root, archived_profile=True)
        self.assertEqual(
            "Luke SkyWalker",
            next(n for n in tree["nodes"] if n["name"] == "txtName")["text"],
        )
        self.assertIn((name + 712, 24), m.reads)
        root = m.node("Root", [name])
        tree = await ui.snapshot(m.read, root, archived_profile=True)
        self.assertIsNone(
            next(n for n in tree["nodes"] if n["name"] == "txtName")["text"]
        )

    async def test_r15_profile_uses_typed_idle_text_without_qualifying_quest_or_combat(
        self,
    ):
        m, _, idle, _, _, _ = quest_memory(dialogue="YOU ARE IDLE")
        raw = b"idleText"
        m.put(idle + 80, raw)
        m.put(idle + 96, struct.pack("<Q", len(raw)))
        root = m.node(
            "Root",
            [
                m.node("WorldView"),
                m.node("idleMessage", [m.node("idleBackground", [idle])]),
            ],
        )
        tree = await ui.snapshot(m.read, root, archived_profile=True)
        self.assertTrue(ui.classify(tree)["nativeIdle"])
        self.assertIn((idle + 712, 24), m.reads)
        self.assertNotIn((idle + 584, 24), m.reads)
        self.assertIsNone(ui.quest_context(tree))
        self.assertIsNone(ui.combat_context(tree))
        m.put(idle, bytes(8))
        tree = await ui.snapshot(m.read, root, archived_profile=True)
        self.assertIsNone(ui.classify(tree)["nativeIdle"])

    async def test_r15_complete_world_without_idle_overlay_is_awake(self):
        m = Memory()
        root = m.node("Root", [m.node("WorldView")])
        self.assertFalse(
            ui.classify(await ui.snapshot(m.read, root, archived_profile=True))[
                "nativeIdle"
            ]
        )

    def test_saved_current_client_states_replay_with_source_provenance(self):
        path = (
            Path(__file__).resolve().parents[1]
            / "fixtures/w101_ui_tree_live_20261110.json"
        )
        fixture = json.loads(path.read_text())
        self.assertGreaterEqual(len(fixture["cases"]), 7)
        for case in fixture["cases"]:
            with self.subTest(case=case["id"]):
                self.assertEqual(64, len(case["sourceTraceSha256"]))
                self.assertEqual(case["expected"], ui.classify(case["tree"]))

    async def test_visible_tree_recognizes_named_states_and_static_idle_text(self):
        m = Memory()
        root = m.node(
            "Root",
            [
                m.node(
                    "WorldView",
                    [
                        m.node("NPCRangeWin"),
                        m.node("wndDialogMain"),
                        m.node("DeckConfiguration", [m.node("SettingPage")]),
                    ],
                ),
                m.node(
                    "idleMessage",
                    [
                        m.node(
                            "idleBackground",
                            [m.node("idleText", text="<center>YOU ARE IDLE</center>")],
                        )
                    ],
                ),
            ],
        )
        tree = await ui.snapshot(m.read, root)
        self.assertTrue(tree["complete"])
        self.assertEqual(dict.fromkeys(ui.STATES, True), ui.classify(tree))

    async def test_hidden_parent_private_branch_and_arbitrary_text_are_not_read(self):
        m = Memory()
        private = m.node("ChatWindow", [m.node("IdleText", text="private phrase")])
        body = m.node("txtMessage", text="NPC body is unnecessary here")
        root = m.node(
            "Root",
            [
                m.node(
                    "WorldView",
                    [private, body, m.node("Hidden", [m.node("NPCRangeWin")], flags=0)],
                )
            ],
        )
        tree = await ui.snapshot(m.read, root)
        serialized = json.dumps(tree)
        self.assertNotIn("private phrase", serialized)
        self.assertNotIn("NPC body", serialized)
        self.assertNotIn((body + 584, 24), m.reads)
        self.assertFalse(ui.classify(tree)["interactionPrompt"])
        self.assertIsNone(ui.classify(tree)["nativeIdle"])

    async def test_absent_world_view_is_unknown_not_four_false_values(self):
        m = Memory()
        root = m.node("Root")
        self.assertEqual(
            dict.fromkeys(ui.STATES), ui.classify(await ui.snapshot(m.read, root))
        )

    async def test_names_in_other_branches_do_not_establish_world_states(self):
        m = Memory()
        root = m.node(
            "Root",
            [
                m.node("WorldView", [m.node("DeckConfiguration")]),
                m.node("NPCRangeWin"),
                m.node("wndDialogMain"),
                m.node("SettingPage"),
            ],
        )
        values = ui.classify(await ui.snapshot(m.read, root))
        self.assertFalse(values["interactionPrompt"])
        self.assertFalse(values["dialogue"])
        self.assertFalse(values["menu"])

    async def test_unreadable_optional_text_retains_tree_but_cannot_prove_idle(self):
        m = Memory()
        idle = m.node("idleText", text="YOU ARE IDLE")
        root = m.node(
            "Root",
            [
                m.node("WorldView"),
                m.node("idleMessage", [m.node("idleBackground", [idle])]),
            ],
        )
        m.put(idle + 584, bytes(8))
        tree = await ui.snapshot(m.read, root)
        self.assertTrue(tree["complete"])
        self.assertIsNone(ui.classify(tree)["nativeIdle"])

    async def test_current_idle_absence_and_service_menus_are_distinct_from_dialogue(
        self,
    ):
        for menu in ("shopGUI", "PayToPlay"):
            m = Memory()
            root = m.node("Root", [m.node("WorldView", [m.node(menu)])])
            tree = await ui.snapshot(m.read, root, current_layout=True)
            values = ui.classify(tree)
            self.assertTrue(values["menu"])
            self.assertFalse(values["dialogue"])
            self.assertFalse(values["nativeIdle"])
            tree["layout"] = "unknown"
            self.assertIsNone(ui.classify(tree)["nativeIdle"])

    async def test_current_text_layout_requires_verified_control_type(self):
        for valid_type in (True, False):
            m = Memory()
            idle = m.node("idleText", text="YOU ARE IDLE", text_offset=712)
            function, vtable, name = m.allocate(), m.allocate(), m.allocate()
            m.put(name, b"ControlText\0" if valid_type else b"UnrelatedControl\0")
            m.put(
                function + 63,
                bytes.fromhex("48 8d 0d") + struct.pack("<i", name - function - 70),
            )
            m.put(vtable, struct.pack("<Q", function))
            m.put(idle, struct.pack("<Q", vtable))
            root = m.node(
                "Root",
                [
                    m.node("WorldView"),
                    m.node("idleMessage", [m.node("idleBackground", [idle])]),
                ],
            )
            tree = await ui.snapshot(m.read, root, current_layout=True)
            self.assertEqual(
                True if valid_type else None, ui.classify(tree)["nativeIdle"]
            )

    async def test_decorative_hud_is_skipped_and_idle_homonym_cannot_match(self):
        m = Memory()
        root = m.node(
            "Root",
            [
                m.node(
                    "WorldView",
                    [
                        m.node(
                            "windowHUD", [m.node("NPCRangeWin") for _ in range(300)]
                        ),
                        m.node("idleText", text="YOU ARE IDLE"),
                    ],
                )
            ],
        )
        tree = await ui.snapshot(m.read, root)
        self.assertEqual(4, len(tree["nodes"]))
        self.assertTrue(tree["nodes"][2]["childrenSkipped"])
        self.assertFalse(ui.classify(tree)["interactionPrompt"])
        self.assertIsNone(ui.classify(tree)["nativeIdle"])

    def test_review_pins_match_native_host_and_workers(self):
        repo = Path(__file__).resolve().parents[2]
        review = json.loads(
            (repo / "tools/fixtures/w101_ui_tree_review.json").read_text()
        )
        worker = repo / "tools/w101_ui_tree.py"
        digest = hashlib.sha256(worker.read_bytes()).hexdigest()
        self.assertEqual(digest, review["workerSha256"])
        native = (
            repo / "ProjectMaelstrom/ProjectMaelstrom/Utilities/W101UiTreeOperation.cs"
        ).read_text()
        self.assertIn(f'WorkerSha256 = "{digest}";', native)
        self.assertEqual(ui.COMMON_SHA256, review["commonHelperSha256"])
        self.assertEqual(
            ui.COMMON_SHA256,
            hashlib.sha256(
                worker.with_name("wizwalker_adapter.py").read_bytes()
            ).hexdigest(),
        )

    async def test_cycles_excessive_vectors_and_name_lengths_fail_closed(self):
        for fault in ("cycle", "vector", "name"):
            m = Memory()
            root = m.node("Root")
            if fault == "cycle":
                data = m.allocate(16)
                m.put(data, struct.pack("<QQ", root, 0))
                m.put(root + 112, struct.pack("<QQ", data, data + 16))
            elif fault == "vector":
                m.put(root + 112, struct.pack("<QQ", 0x10000, 0x20000))
            else:
                m.put(root + 96, struct.pack("<Q", 2**32))
            with self.assertRaises(ValueError):
                await ui.snapshot(m.read, root)

    async def test_changed_child_vector_is_not_a_complete_tree(self):
        m = Memory()
        root = m.node("Root", [m.node("WorldView")])

        async def read(address, count):
            data = await m.read(address, count)
            return bytes(count) if address == root + 112 else data

        with self.assertRaisesRegex(ValueError, "changed"):
            await ui.snapshot(read, root)


class Handler:
    AUTOBOT_SIZE = 3900
    AUTOBOT_PATTERN = b"scratch"

    def __init__(self):
        self.memory = Memory()
        self.entry = self.memory.allocate(16)
        self.memory.allocate(self.AUTOBOT_SIZE)
        self.scratch = self.memory.allocate(self.AUTOBOT_SIZE)
        self.memory.put(self.entry, bytes.fromhex("49 8b 8f d8 00 00 00"))
        self.baseline = bytes(i % 251 for i in range(self.AUTOBOT_SIZE))
        self.memory.put(self.scratch, self.baseline)
        self.freed = []
        self.fail_restore = False
        self.fail_setup = False

    async def pattern_scan(self, pattern, module=None):
        return self.entry if pattern == b"root" else self.scratch

    async def read_bytes(self, address, count):
        return await self.memory.read(address, count)

    async def write_bytes(self, address, data):
        if self.fail_restore and address == self.entry and data[0] == 0x49:
            raise OSError("inert restore fault")
        self.memory.put(address, data)

    async def free(self, address):
        self.freed.append(address)


class Hook:
    pattern = b"root"

    def __init__(self, handler):
        self.h = handler

    async def hook(self):
        self.jump_address = await self.get_jump_address(self.pattern)
        self.hook_address = self.h._autobot_address
        self.current_root_window_addr = self.h.memory.allocate(8)
        self.jump_original_bytecode = await self.h.read_bytes(self.jump_address, 7)
        self.jump_bytecode = b"\xe9" + bytes(4) + b"\x90\x90"
        self.hook_bytecode = b"synthetic hook body"
        await self.h.write_bytes(self.hook_address, self.hook_bytecode)
        await self.h.write_bytes(self.jump_address, self.jump_bytecode)
        if self.h.fail_setup:
            raise OSError("inert partial setup failure")


class HookTests(unittest.IsolatedAsyncioTestCase):
    async def test_five_byte_player_entry_restores_scratch_and_export(self):
        class Player(Hook):
            async def hook(self):
                self.jump_address = await self.get_jump_address(self.pattern)
                self.hook_address = self.h._autobot_address
                self.player_struct = self.h.memory.allocate(8)
                self.jump_original_bytecode = await self.h.read_bytes(
                    self.jump_address, 5
                )
                self.jump_bytecode = b"\xe9" + bytes(4)
                self.hook_bytecode = b"synthetic player body"
                await self.h.write_bytes(self.hook_address, self.hook_bytecode)
                await self.h.write_bytes(self.jump_address, self.jump_bytecode)

        h = Handler()
        entry = bytes.fromhex("f2 0f 10 40 58")
        h.memory.put(h.entry, entry)
        scope = ui.RootScope(
            h,
            Player,
            lambda _: asyncio.sleep(0),
            expected_entry=entry,
            export_name="player_struct",
        )
        await scope.open()
        export = scope.hook.player_struct
        await scope.close()
        self.assertEqual(entry, await h.read_bytes(h.entry, 5))
        self.assertEqual(h.baseline, await h.read_bytes(h.scratch, h.AUTOBOT_SIZE))
        self.assertEqual([export], h.freed)

    async def test_current_reviewed_hook_preserves_r13_instruction_and_full_scratch(
        self,
    ):
        class CurrentHandler(Handler):
            AUTOBOT_SIZE = 4100

        h = CurrentHandler()
        h.memory.put(h.entry, ui.CURRENT_ROOT_ENTRY)
        scope = ui.RootScope(
            h, Hook, lambda _: asyncio.sleep(0), expected_entry=ui.CURRENT_ROOT_ENTRY
        )
        await scope.open()
        await scope.close()
        self.assertEqual(ui.CURRENT_ROOT_ENTRY, await h.read_bytes(h.entry, 7))
        self.assertEqual(h.baseline, await h.read_bytes(h.scratch, 4100))
        hook = ui.current_root_hook(Hook)(h)
        body = await hook.bytecode_generator(
            [("current_root_window_addr", struct.pack("<Q", 0x12340000))]
        )
        self.assertEqual(b"\x50" + bytes.fromhex("49 8b 85 d8 00 00 00"), body[:8])
        self.assertTrue(body.endswith(b"\x58" + ui.CURRENT_ROOT_ENTRY))
        self.assertEqual(26, len(body))

    async def test_whole_scratch_and_entry_restored_and_export_freed_after_settle(self):
        h = Handler()

        async def settle(_):
            self.assertEqual(
                bytes.fromhex("49 8b 8f d8 00 00 00"), await h.read_bytes(h.entry, 7)
            )
            self.assertEqual([], h.freed)

        scope = ui.RootScope(h, Hook, settle)
        await scope.open()
        await scope.close()
        await scope.close()
        self.assertTrue(scope.restored)
        self.assertEqual(1, len(h.freed))
        self.assertEqual(h.baseline, await h.read_bytes(h.scratch, 3900))

    async def test_partial_setup_restores_complete_baseline(self):
        h = Handler()
        h.fail_setup = True
        scope = ui.RootScope(h, Hook, lambda _: asyncio.sleep(0))
        with self.assertRaises(OSError):
            await scope.open()
        await scope.close()
        self.assertTrue(scope.restored)
        self.assertEqual(h.baseline, await h.read_bytes(h.scratch, 3900))

    async def test_failed_entry_restoration_never_frees_executable_support(self):
        h = Handler()
        scope = ui.RootScope(h, Hook, lambda _: asyncio.sleep(0))
        await scope.open()
        h.fail_restore = True
        with self.assertRaises(OSError):
            await scope.close()
        self.assertFalse(scope.restored)
        self.assertEqual([], h.freed)

    async def test_unknown_instruction_rejects_without_writes(self):
        h = Handler()
        h.memory.put(h.entry, b"changed")
        scope = ui.RootScope(h, Hook)
        with self.assertRaisesRegex(
            ui.UiCompatibilityError, "root-instruction-changed"
        ):
            await scope.open()
        await scope.close()
        self.assertFalse(scope.touched)
        self.assertEqual(h.baseline, await h.read_bytes(h.scratch, 3900))

    async def test_missing_signatures_report_fixed_codes_without_writing(self):
        for failed_pattern, expected in (
            (b"root", "root-signature-unavailable"),
            (b"scratch", "scratch-signature-unavailable"),
        ):
            h = Handler()
            original_scan = h.pattern_scan

            async def scan(pattern, module=None):
                if pattern == failed_pattern:
                    raise OSError("private address or diagnostic")
                return await original_scan(pattern, module)

            h.pattern_scan = scan
            scope = ui.RootScope(h, Hook)
            with self.assertRaisesRegex(ui.UiCompatibilityError, expected):
                await scope.open()
            await scope.close()
            self.assertFalse(scope.touched)
            self.assertEqual([], h.freed)
            self.assertEqual(h.baseline, await h.read_bytes(h.scratch, 3900))


class Client:
    def __init__(self, *, fail_open=False, fail_close=False, loading=False):
        self.fail_open, self.fail_close, self.value = fail_open, fail_close, loading
        self.closed = False

    async def open(self):
        if self.fail_open:
            raise OSError("private debug text")

    async def loading(self):
        return self.value

    async def tree(self):
        return dict(
            complete=True,
            nodes=[
                dict(
                    index=0,
                    parent=None,
                    name="WorldView",
                    visible=True,
                    enabled=True,
                    text=None,
                )
            ],
            readBytes=96,
        )

    async def close(self):
        self.closed = True
        if self.fail_close:
            raise OSError("private cleanup debug")


class ObserveTests(unittest.IsolatedAsyncioTestCase):
    async def test_two_tree_snapshot_stops_with_original_timestamps_and_cleanup(self):
        client = Client()
        events = []

        async def emit(value):
            events.append(value)

        result = await ui.observe(
            request() | dict(durationSeconds=3, stopAfterTwoTrees=True),
            lambda _: client,
            lambda _: True,
            lambda: False,
            emit,
        )
        trees = [event for event in events if event["kind"] == "tree"]
        self.assertEqual(2, len(trees))
        self.assertEqual("bounded-ui-snapshot-finished", result["code"])
        self.assertEqual(
            trees[-1]["statesObservedUtcTicks"], result["statesObservedUtcTicks"]
        )
        self.assertTrue(
            result["cleanupConfirmed"] and result["hookRestored"] and client.closed
        )

    async def test_two_tree_snapshot_disagreement_is_not_stable(self):
        client = Client()
        count = 0

        async def tree():
            nonlocal count
            count += 1
            return title_tree("Dance Game" if count == 1 else "Milo Barker")

        async def emit(value):
            pass

        client.tree = tree
        result = await ui.observe(
            request() | dict(durationSeconds=3, stopAfterTwoTrees=True),
            lambda _: client,
            lambda _: True,
            lambda: False,
            emit,
        )
        self.assertEqual(2, result["treeSamples"])
        self.assertEqual("Milo Barker", result["interactionTitle"])
        self.assertIsNone(result["stableInteractionTitle"])

    async def test_two_tree_snapshot_never_bypasses_cleanup_failure(self):
        client = Client(fail_close=True)

        async def emit(value):
            pass

        result = await ui.observe(
            request() | dict(durationSeconds=3, stopAfterTwoTrees=True),
            lambda _: client,
            lambda _: True,
            lambda: False,
            emit,
        )
        self.assertEqual("cleanup_failed", result["status"])
        self.assertFalse(result["cleanupConfirmed"])
        self.assertEqual(0, result["statesObservedUtcTicks"])

    async def test_two_tree_snapshot_failure_respects_duration_and_clears_stability(
        self,
    ):
        client = Client()
        count = 0

        async def tree():
            nonlocal count
            count += 1
            if count > 1:
                raise ValueError("Synthetic read failure")
            return title_tree()

        async def emit(value):
            pass

        client.tree = tree
        result = await ui.observe(
            request() | dict(durationSeconds=2, stopAfterTwoTrees=True),
            lambda _: client,
            lambda _: True,
            lambda: False,
            emit,
        )
        self.assertEqual("bounded-ui-watch-finished", result["code"])
        self.assertEqual(1, result["treeSamples"])
        self.assertGreater(result["treeFailures"], 0)
        self.assertEqual(0, result["statesObservedUtcTicks"])
        self.assertIsNone(result["stableInteractionTitle"])
        self.assertTrue(client.closed)

    async def test_tree_timestamp_does_not_renew_slow_acquisition(self):
        client = Client()
        original = client.tree
        starts = []

        async def delayed_tree():
            starts.append(ui.common.utc_ticks())
            await asyncio.sleep(0.04)
            return await original()

        client.tree = delayed_tree
        result, _ = await self.run_observer(client)
        self.assertLessEqual(result["statesObservedUtcTicks"], starts[-1])
        self.assertGreaterEqual(
            result["observedUtcTicks"] - result["statesObservedUtcTicks"], 300000
        )

    async def test_combat_context_is_opt_in_and_loading_clears_result_and_trace(self):
        m, root, *_ = combat_memory()
        tree = await ui.snapshot(
            m.read, root, current_layout=True, include_combat_context=True
        )
        for opt_in in (False, True):
            client = Client()
            events = []

            async def read_tree():
                return tree

            async def emit(value):
                events.append(value)
                if value["kind"] == "tree":
                    client.value = True

            client.tree = read_tree
            result = await ui.observe(
                request() | dict(includeCombatContext=opt_in),
                lambda _: client,
                lambda _: True,
                lambda: False,
                emit,
            )
            first = next(e for e in events if e["kind"] == "tree")
            self.assertEqual(
                ui.combat_context(tree) if opt_in else None, first["combatContext"]
            )
            self.assertGreater(first["observedUtcTicks"], 0)
            self.assertIsNone(result["combatContext"])
            self.assertEqual(0, result["statesObservedUtcTicks"])
            self.assertTrue(client.closed)
            self.assertTrue(
                any(
                    e["kind"] == "loading"
                    and e["loading"] is True
                    and e.get("combatContext", "missing") is None
                    for e in events
                )
            )

    async def test_combat_context_is_cleared_after_tree_error_or_failed_cleanup(self):
        m, root, *_ = combat_memory()
        tree = await ui.snapshot(
            m.read, root, current_layout=True, include_combat_context=True
        )
        for cleanup_fails in (False, True):
            client = Client(fail_close=cleanup_fails)
            events = []
            reads = 0

            async def read_tree():
                nonlocal reads
                reads += 1
                if reads > 1 and not cleanup_fails:
                    raise ValueError("Synthetic unavailability")
                return tree

            async def emit(value):
                events.append(value)

            client.tree = read_tree
            result = await ui.observe(
                request() | dict(includeCombatContext=True, durationSeconds=2),
                lambda _: client,
                lambda _: True,
                lambda: False,
                emit,
            )
            self.assertIsNotNone(
                next(e for e in events if e["kind"] == "tree")["combatContext"]
            )
            self.assertIsNone(result["combatContext"])
            if cleanup_fails:
                self.assertEqual("cleanup_failed", result["status"])
            else:
                self.assertTrue(
                    any(
                        e["kind"] == "tree-unavailable" and e["combatContext"] is None
                        for e in events
                    )
                )

    async def test_quest_context_needs_two_equal_reads_and_is_cleared_by_loading(self):
        m, root, *_ = quest_memory()
        tree = await ui.snapshot(
            m.read, root, current_layout=True, include_quest_context=True
        )
        client = Client()
        count = 0
        events = []

        async def read_tree():
            return tree

        async def emit(value):
            nonlocal count
            events.append(value)
            if value["kind"] == "tree":
                count += 1
                if count == 2:
                    client.value = True

        client.tree = read_tree
        result = await ui.observe(
            request() | dict(durationSeconds=2, includeQuestContext=True),
            lambda _: client,
            lambda _: True,
            lambda: False,
            emit,
        )
        trees = [e for e in events if e["kind"] == "tree"]
        self.assertEqual(2, len(trees))
        self.assertIsNone(trees[0]["stableQuestContext"])
        self.assertEqual(trees[1]["questContext"], trees[1]["stableQuestContext"])
        self.assertIsNone(result["questContext"])
        self.assertIsNone(result["stableQuestContext"])
        self.assertEqual(0, result["statesObservedUtcTicks"])
        self.assertTrue(client.closed)

    async def run_observer(self, client, stop=lambda: False, identity=lambda _: True):
        events = []

        async def emit(value):
            events.append(value)

        return (
            await ui.observe(request(), lambda _: client, identity, stop, emit),
            events,
        )

    async def test_success_uses_staggered_single_connection_and_closes(self):
        client = Client()
        result, events = await self.run_observer(client)
        self.assertEqual("observed", result["status"])
        self.assertTrue(
            result["cleanupConfirmed"] and result["hookRestored"] and client.closed
        )
        self.assertGreater(result["loadingSamples"], result["treeSamples"])
        self.assertGreater(result["statesObservedUtcTicks"], 0)
        self.assertEqual("ready", events[0]["kind"])

    async def test_partial_open_is_cleaned_and_failed_cleanup_stays_failed(self):
        for fail_close in (False, True):
            client = Client(fail_open=True, fail_close=fail_close)
            result, _ = await self.run_observer(client)
            self.assertTrue(client.closed)
            self.assertEqual(not fail_close, result["cleanupConfirmed"])
            self.assertNotIn("private", json.dumps(result))

    async def test_compatibility_rejection_is_diagnostic_but_not_ready(self):
        client = Client()

        async def open_incompatible():
            raise ui.UiCompatibilityError("root-instruction-changed")

        client.open = open_incompatible
        result, events = await self.run_observer(client)
        self.assertEqual("unavailable", result["status"])
        self.assertEqual("root-instruction-changed", result["code"])
        self.assertTrue(result["cleanupConfirmed"])
        self.assertEqual(dict.fromkeys(ui.STATES), result["states"])
        self.assertEqual([], events)

    async def test_stop_before_open_or_wrong_identity_never_connects(self):
        client = Client()
        result, events = await self.run_observer(client, stop=lambda: True)
        self.assertEqual("cancelled", result["status"])
        self.assertFalse(client.closed)
        self.assertEqual([], events)
        result, _ = await self.run_observer(client, identity=lambda _: False)
        self.assertEqual("unavailable", result["status"])
        self.assertFalse(client.closed)

    async def test_loading_never_grants_states_or_tree_success(self):
        result, _ = await self.run_observer(Client(loading=True))
        self.assertEqual("unavailable", result["status"])
        self.assertEqual(0, result["treeSamples"])
        self.assertEqual(dict.fromkeys(ui.STATES), result["states"])


class ContractTests(unittest.TestCase):
    def test_exact_build_selects_register_and_scratch_profile(self):
        self.assertEqual(
            ("r13-current", ui.CURRENT_ROOT_ENTRY, 4100),
            ui.profile_for(ui.CURRENT_IMAGE_SHA256),
        )
        self.assertEqual(
            ("r15-archived", bytes.fromhex("49 8b 8f d8 00 00 00"), 3900),
            ui.profile_for(ui.ARCHIVED_LAYOUT_IMAGE_SHA256),
        )
        with self.assertRaises(ui.UiCompatibilityError):
            ui.profile_for("f" * 64)

    def test_request_rejects_default_off_unknown_network_and_unbounded_duration(self):
        for change in (
            dict(enabled=False),
            dict(uiTreeEnabled=False),
            dict(networkAllowed=True),
            dict(durationSeconds=0),
            dict(durationSeconds=61),
            dict(durationSeconds=True),
            dict(command="move"),
            dict(includeQuestContext="true"),
            dict(includeQuestContext=1),
            dict(includeCombatContext="true"),
            dict(includeCombatContext=1),
            dict(stopAfterTwoTrees="true"),
            dict(stopAfterTwoTrees=1),
        ):
            with self.assertRaises(ValueError):
                ui.validate_request(request() | change)

    def test_schedule_offsets_reads_and_skips_missed_work(self):
        schedule = ui.ReadSchedule(0)
        self.assertEqual("loading", schedule.take(0))
        self.assertIsNone(schedule.take(0.1))
        self.assertEqual("tree", schedule.take(0.125))
        self.assertEqual("loading", schedule.take(50))
        self.assertEqual("tree", schedule.take(50))
        self.assertIsNone(schedule.take(50))


if __name__ == "__main__":
    unittest.main()
