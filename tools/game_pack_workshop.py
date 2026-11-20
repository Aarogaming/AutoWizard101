"""First offline Workshop: edit draft rules, save versions, inspect replay evidence."""

from concurrent.futures import ThreadPoolExecutor
from copy import deepcopy
from pathlib import Path
import tkinter as tk
from tkinter import filedialog, messagebox, ttk
from uuid import uuid4

from evaluate_game_pack import evaluate, write_report
from compare_pack_versions import compare, compare_runs
from compare_wsl_dodge_runs import compare as compare_wsl_runs
from game_data import decode, read_bytes
from game_packs import load_pack, new_pack, save_pack, validate_pack

ROOT = Path(__file__).resolve().parents[1]
SAMPLES = ROOT / "tools/fixtures/game_packs"


def context_summary(pack):
    context = pack.get("context")
    if context is None:
        return "No game context in this pack."
    identity = context["identity"]
    return (
        f"Goal: {context['goal']}  ·  Action states: {', '.join(context['action_states'])}"
        f"  ·  Identification threshold: {identity['minimum_score']}/{sum(identity['signals'].values())}"
        f"  ·  Policy: {context.get('avoidance_policy', 'motion-v1')}"
    )


def compare_test_reports(first_path, second_path):
    first = decode(read_bytes(Path(first_path)))
    second = decode(read_bytes(Path(second_path)))
    if (
        isinstance(first, dict)
        and isinstance(second, dict)
        and first.get("mode") == second.get("mode") == "wsl-private-visual"
    ):
        return compare_wsl_runs([first_path, second_path])
    return compare_runs(first_path, second_path)


class Workshop(tk.Tk):
    def __init__(self, library=None):
        super().__init__()
        self.title("Maelstrom • Game Pack Workshop")
        self.geometry("1000x760")
        self.minsize(880, 700)
        self.library = Path(library or ROOT / ".local/game-packs")
        self.pack = new_pack("my-pack", "My game pack", "my-game")
        self.saved_path = None
        self.saved_data = None
        self.future = None
        self.worker = ThreadPoolExecutor(max_workers=1)
        self.poll_id = None
        self.protocol("WM_DELETE_WINDOW", self.close)
        self.fields = {
            name: tk.StringVar() for name in ("pack_id", "title", "game_id", "version")
        }
        self.rule_fields = {
            name: tk.StringVar()
            for name in (
                "id",
                "state",
                "type",
                "key",
                "x",
                "y",
                "duration_ms",
                "action_id",
            )
        }
        self.action_fields = {
            name: tk.StringVar() for name in ("id", "title", "key", "duration_ms")
        }
        self.session = tk.StringVar()
        self.expected = tk.StringVar()
        self.context_status = tk.StringVar()
        self.status = tk.StringVar(
            value="Draft • offline only. Create a rule or explore a sample."
        )
        self._build()
        self.show_pack(self.pack)

    def _build(self):
        base = ttk.Frame(self, padding=18)
        base.pack(fill="both", expand=True)
        ttk.Label(base, text="Game Pack Workshop", font=("Segoe UI", 20, "bold")).pack(
            anchor="w"
        )
        ttk.Label(
            base,
            text="Create rules, save a version, and review what it would propose in a recorded session.",
        ).pack(anchor="w", pady=(3, 12))
        bar = ttk.Frame(base)
        bar.pack(fill="x")
        for label, command in (
            ("New draft", self.new),
            ("Open pack…", self.open),
            ("Keyboard sample", lambda: self.sample("pet-dance")),
            ("Pointer sample", lambda: self.sample("potion-motion")),
            ("Dodge sample", lambda: self.sample("dodge")),
            ("Save version", self.save),
        ):
            ttk.Button(bar, text=label, command=command).pack(side="left", padx=(0, 8))
        details = ttk.LabelFrame(base, text="Pack details", padding=10)
        details.pack(fill="x", pady=12)
        for i, (name, label) in enumerate(
            (
                ("title", "Title"),
                ("pack_id", "Pack ID"),
                ("game_id", "Game ID"),
                ("version", "Version"),
            )
        ):
            ttk.Label(details, text=label).grid(row=0, column=i, sticky="w")
            ttk.Entry(details, textvariable=self.fields[name], width=23).grid(
                row=1, column=i, sticky="ew", padx=(0, 10)
            )
            details.columnconfigure(i, weight=1)
        context = ttk.Frame(base)
        context.pack(fill="x", pady=(0, 6))
        ttk.Button(
            context,
            text="Dodge context example",
            command=lambda: self.sample("dodge-context"),
        ).pack(side="right")
        ttk.Button(
            context,
            text="Recovery experiment",
            command=lambda: self.sample("dodge-context-v4"),
        ).pack(side="right", padx=(0, 6))
        ttk.Label(context, textvariable=self.context_status, wraplength=700).pack(
            side="left", anchor="w"
        )
        rules = ttk.LabelFrame(
            base, text="Rules • exact state labels from a recording", padding=10
        )
        rules.pack(fill="both", expand=True)
        self.tree = ttk.Treeview(rules, columns=("state", "proposal"), height=6)
        self.tree.heading("#0", text="Rule")
        self.tree.heading("state", text="When state equals")
        self.tree.heading("proposal", text="Propose")
        self.tree.column("#0", width=150)
        self.tree.pack(fill="both", expand=True)
        self.tree.bind("<<TreeviewSelect>>", self.select_rule)
        editor = ttk.Frame(rules)
        editor.pack(fill="x", pady=(10, 0))
        for i, (name, label) in enumerate(
            (
                ("id", "Rule ID"),
                ("state", "State label"),
                ("type", "Action"),
                ("key", "Key"),
                ("x", "X (0–1)"),
                ("y", "Y (0–1)"),
                ("duration_ms", "Wait (ms)"),
                ("action_id", "Action ID"),
            )
        ):
            ttk.Label(editor, text=label).grid(row=0, column=i, sticky="w")
            if name == "type":
                control = ttk.Combobox(
                    editor,
                    textvariable=self.rule_fields[name],
                    values=("key", "click", "wait", "action"),
                    state="readonly",
                    width=8,
                )
            else:
                control = ttk.Entry(
                    editor,
                    textvariable=self.rule_fields[name],
                    width=15 if i < 2 else 8,
                )
            control.grid(row=1, column=i, sticky="ew", padx=(0, 6))
            editor.columnconfigure(i, weight=1)
        self.rule_fields["type"].set("key")
        actions = ttk.Frame(rules)
        actions.pack(fill="x", pady=(8, 0))
        ttk.Button(actions, text="Add / update rule", command=self.add_rule).pack(
            side="left"
        )
        ttk.Button(actions, text="Remove selected", command=self.remove_rule).pack(
            side="left", padx=8
        )
        ttk.Label(
            actions,
            text="Click coordinates are relative to the game area; 0.5 is halfway.",
        ).pack(side="left")
        registry = ttk.LabelFrame(
            base, text="Named key actions (draft registry)", padding=6
        )
        registry.pack(fill="x", pady=(0, 8))
        for i, (field, label) in enumerate(
            (
                ("id", "Action ID"),
                ("title", "Title"),
                ("key", "Key"),
                ("duration_ms", "Hold ms (1–1000)"),
            )
        ):
            ttk.Label(registry, text=label).grid(row=0, column=i, sticky="w")
            ttk.Entry(
                registry,
                textvariable=self.action_fields[field],
                width=20 if field == "title" else 14,
            ).grid(row=1, column=i, sticky="ew", padx=(0, 6))
            registry.columnconfigure(i, weight=1)
        ttk.Button(registry, text="Add / update action", command=self.add_action).grid(
            row=1, column=4
        )
        replay = ttk.LabelFrame(base, text="Offline replay", padding=10)
        replay.pack(fill="x", pady=12)
        for row, (label, variable) in enumerate(
            (
                ("Session", self.session),
                ("Expected proposals (optional)", self.expected),
            )
        ):
            ttk.Label(replay, text=label).grid(row=row, column=0, sticky="w")
            ttk.Entry(replay, textvariable=variable).grid(
                row=row, column=1, sticky="ew", padx=8, pady=3
            )
            ttk.Button(
                replay, text="Browse…", command=lambda v=variable: self.browse(v)
            ).grid(row=row, column=2)
        replay.columnconfigure(1, weight=1)
        self.run_button = ttk.Button(
            replay, text="Evaluate saved version", command=self.run
        )
        self.run_button.grid(row=2, column=0, pady=8, sticky="w")
        self.compare_button = ttk.Button(
            replay, text="Compare another version…", command=self.compare_version
        )
        self.compare_button.grid(row=2, column=1, pady=8, sticky="w")
        self.compare_runs_button = ttk.Button(
            replay, text="Compare Dodge test runs…", command=self.compare_test_runs
        )
        self.compare_runs_button.grid(row=2, column=2, pady=8, sticky="w")
        self.result = tk.Text(replay, height=5, wrap="word", state="disabled")
        self.result.grid(row=3, column=0, columnspan=3, sticky="ew")
        ttk.Label(base, textvariable=self.status, wraplength=920).pack(anchor="w")

    def draft(self):
        data = deepcopy(self.pack)
        data.update(
            {name: variable.get().strip() for name, variable in self.fields.items()}
        )
        data["status"] = "draft"
        return validate_pack(data)

    def show_pack(self, pack):
        self.pack = deepcopy(pack)
        self.context_status.set(context_summary(pack))
        for name, variable in self.fields.items():
            variable.set(pack[name])
        self.refresh_rules()

    def reset_view(self):
        for variable in self.rule_fields.values():
            variable.set("")
        self.rule_fields["type"].set("key")
        self.session.set("")
        self.expected.set("")
        self.result.configure(state="normal")
        self.result.delete("1.0", "end")
        self.result.configure(state="disabled")

    def refresh_rules(self):
        self.tree.delete(*self.tree.get_children())
        for rule in self.pack["skills"]:
            self.tree.insert(
                "",
                "end",
                iid=rule["id"],
                text=rule["id"],
                values=(rule["when"]["state"], str(rule["propose"])),
            )

    def discard_ok(self):
        # Preserve unsaved rule edits as well as pack metadata when switching files.
        try:
            dirty = self.draft() != (
                self.saved_data or new_pack("my-pack", "My game pack", "my-game")
            )
        except ValueError:
            dirty = True
        return not dirty or messagebox.askyesno(
            "Unsaved draft", "Discard the current unsaved draft?", parent=self
        )

    def new(self):
        if not self.discard_ok():
            return
        self.saved_path = self.saved_data = None
        self.show_pack(new_pack("my-pack", "My game pack", "my-game"))
        self.reset_view()
        self.status.set("New draft. Add a rule, then save a version.")

    def open(self):
        path = filedialog.askopenfilename(
            parent=self, title="Open pack.json", filetypes=[("Pack", "*.json")]
        )
        if path:
            self.open_path(path)

    def open_path(self, path, confirm=True):
        if confirm and not self.discard_ok():
            return
        try:
            data = load_pack(path)
            self.show_pack(data)
            self.reset_view()
            self.saved_path = Path(path)
            self.saved_data = deepcopy(data)
            if data["fixtures"] and isinstance(data["fixtures"][0], str):
                fixture = (self.saved_path.parent / data["fixtures"][0]).resolve()
                if (
                    fixture.is_relative_to(self.saved_path.parent.resolve())
                    and fixture.is_file()
                ):
                    self.session.set(str(fixture))
            self.status.set(
                f"Opened {data['title']} • {data['version']}. Change the version before saving edits."
            )
        except (ValueError, OSError) as exc:
            self.error(exc)

    def sample(self, name):
        if not self.discard_ok():
            return
        self.open_path(SAMPLES / name / "pack.json", confirm=False)
        self.session.set("" if name == "dodge" else str(SAMPLES / "session.json"))
        self.expected.set(
            "" if name == "dodge" else str(SAMPLES / name / "expected.json")
        )

    def add_action(self):
        try:
            values = {
                name: value.get().strip() for name, value in self.action_fields.items()
            }
            action = {
                "id": values["id"],
                "title": values["title"],
                "key": values["key"].upper(),
                "duration_ms": int(values["duration_ms"]),
            }
            data = self.draft()
            data["schema_version"] = 2
            data["actions"] = [
                a for a in data.get("actions", []) if a["id"] != action["id"]
            ] + [action]
            validate_pack(data)
            self.show_pack(data)
            self.status.set(
                "Named action saved to the draft. Save a new pack version before evaluating."
            )
        except (ValueError, OSError) as exc:
            self.error(exc)

    def select_rule(self, _event=None):
        selected = self.tree.selection()
        if not selected:
            return
        rule = next(rule for rule in self.pack["skills"] if rule["id"] == selected[0])
        for variable in self.rule_fields.values():
            variable.set("")
        for name, value in {
            "id": rule["id"],
            "state": rule["when"]["state"],
            "type": rule["propose"]["type"],
            **rule["propose"]["parameters"],
        }.items():
            self.rule_fields[name].set(str(value))
        if rule["propose"]["type"] == "action":
            action_id = rule["propose"]["parameters"]["action_id"]
            action = next(a for a in self.pack["actions"] if a["id"] == action_id)
            for name, value in action.items():
                self.action_fields[name].set(str(value))

    def add_rule(self):
        try:
            values = {
                name: variable.get().strip()
                for name, variable in self.rule_fields.items()
            }
            kind = values["type"]
            if kind == "key":
                parameters = {"key": values["key"]}
            elif kind == "click":
                parameters = {"x": float(values["x"]), "y": float(values["y"])}
            elif kind == "action":
                parameters = {"action_id": values["action_id"]}
            else:
                parameters = {"duration_ms": int(values["duration_ms"])}
            rule = {
                "id": values["id"],
                "when": {"state": values["state"]},
                "propose": {"type": kind, "parameters": parameters},
            }
            data = self.draft()
            data["skills"] = [r for r in data["skills"] if r["id"] != rule["id"]] + [
                rule
            ]
            validate_pack(data)
            self.show_pack(data)
            self.status.set(
                "Rule applied to draft. Save a new version before evaluating."
            )
        except (ValueError, OSError) as exc:
            self.error(exc)

    def remove_rule(self):
        selected = set(self.tree.selection())
        self.pack["skills"] = [
            rule for rule in self.pack["skills"] if rule["id"] not in selected
        ]
        self.refresh_rules()
        self.status.set("Draft changed. Save a new version before evaluating.")

    def save(self):
        try:
            data = self.draft()
            path = save_pack(
                data, self.library, self.saved_path.parent if self.saved_path else None
            )
            self.saved_path = path / "pack.json"
            self.saved_data = deepcopy(data)
            self.show_pack(data)
            self.status.set(f"Saved version {data['version']} in {path}")
            if data["fixtures"]:
                self.session.set(str(path / data["fixtures"][0]))
        except (ValueError, OSError) as exc:
            self.error(exc)

    def browse(self, variable):
        path = filedialog.askopenfilename(parent=self, filetypes=[("JSON", "*.json")])
        if path:
            variable.set(path)

    def run(self):
        if self.future is not None:
            return
        try:
            if self.saved_path is None or self.draft() != self.saved_data:
                raise ValueError(
                    "Save the current draft as a new version before replay."
                )
            if not self.session.get().strip():
                raise ValueError("Choose a session or open a sample first.")
            args = (
                self.saved_path,
                self.session.get().strip(),
                self.expected.get().strip() or None,
            )
            report_path = (
                self.library.parent / "reports" / f"workshop-{uuid4().hex}.json"
            )

            def work():
                report = evaluate(*args)
                write_report(report_path, report)
                return report, report_path

            self.future = self.worker.submit(work)
            self.run_button.configure(state="disabled")
            self.compare_button.configure(state="disabled")
            self.compare_runs_button.configure(state="disabled")
            self.status.set("Evaluating saved version offline…")
            self.poll_id = self.after(50, self.poll)
        except (ValueError, OSError) as exc:
            self.error(exc)

    def compare_version(self, other_path=None):
        if self.future is not None:
            return
        try:
            if self.saved_path is None or self.draft() != self.saved_data:
                raise ValueError(
                    "Save the current draft as a new version before comparing."
                )
            if not self.session.get().strip():
                raise ValueError("Choose a session before comparing versions.")
            other_path = other_path or filedialog.askopenfilename(
                parent=self,
                title="Choose another version's pack.json",
                filetypes=[("Pack", "*.json")],
            )
            if not other_path:
                return
            report_path = (
                self.library.parent / "reports" / f"pack-comparison-{uuid4().hex}.json"
            )
            session = self.session.get().strip()
            saved = self.saved_path

            def work():
                report = compare(saved, other_path, session)
                write_report(report_path, report)
                return report, report_path

            self.future = self.worker.submit(work)
            self.run_button.configure(state="disabled")
            self.compare_button.configure(state="disabled")
            self.compare_runs_button.configure(state="disabled")
            self.status.set("Comparing both versions on the same saved session…")
            self.poll_id = self.after(50, self.poll)
        except (ValueError, OSError) as exc:
            self.error(exc)

    def compare_test_runs(self, first_path=None, second_path=None):
        if self.future is not None:
            return
        try:
            first_path = first_path or filedialog.askopenfilename(
                parent=self,
                title="Choose first Dodge test report",
                filetypes=[("Report", "*.json")],
            )
            if not first_path:
                return
            second_path = second_path or filedialog.askopenfilename(
                parent=self,
                title="Choose second Dodge test report",
                filetypes=[("Report", "*.json")],
            )
            if not second_path:
                return
            report_path = (
                self.library.parent
                / "reports"
                / f"dodge-run-comparison-{uuid4().hex}.json"
            )

            def work():
                report = compare_test_reports(first_path, second_path)
                write_report(report_path, report)
                return report, report_path

            self.future = self.worker.submit(work)
            self.run_button.configure(state="disabled")
            self.compare_button.configure(state="disabled")
            self.compare_runs_button.configure(state="disabled")
            self.status.set("Checking and comparing test-run evidence…")
            self.poll_id = self.after(50, self.poll)
        except (ValueError, OSError) as exc:
            self.error(exc)

    def poll(self):
        self.poll_id = None
        if not self.future.done():
            self.poll_id = self.after(50, self.poll)
            return
        try:
            report, path = self.future.result()
            if report.get("mode") == "paired-wsl-dodge-comparison":
                pair = report["pairs"][0]
                left, right = pair["baseline"], pair["candidate"]
                summary = (
                    f"Seed {pair['seed']} · baseline: {left['seconds']} s ({left['observation']})\n"
                    f"Candidate: {right['seconds']} s ({right['observation']})\n"
                    f"Same seed does not guarantee identical play; censored runs are not wins.\nReport: {path}"
                )
                self.result.configure(state="normal")
                self.result.delete("1.0", "end")
                self.result.insert("1.0", summary)
                self.result.configure(state="disabled")
                self.status.set(
                    "Private-display comparison complete; inspect the report and frames."
                )
                return
            if report.get("mode") == "dodge_run_comparison":
                first, second = report["first"], report["second"]
                summary = (
                    f"{first['pack']['Version']}: {first['action_count']} actions, {first['outcomes']}\n"
                    f"{second['pack']['Version']}: {second['action_count']} actions, {second['outcomes']}\n"
                    f"Movement alone does not establish better gameplay.\nReport: {path}"
                )
                self.result.configure(state="normal")
                self.result.delete("1.0", "end")
                self.result.insert("1.0", summary)
                self.result.configure(state="disabled")
                self.status.set(
                    "Comparison complete; saved evidence was checked where movement was reported."
                )
                return
            if report.get("mode") == "offline_pack_comparison":
                summary = (
                    f"{report['first']['version']} compared with {report['second']['version']}\n"
                    f"{report['observations']} observations; {report['different']} differing proposals; {report['unchanged']} unchanged.\n"
                    f"This does not measure gameplay success. No desktop input.\nReport: {path}"
                )
                self.result.configure(state="normal")
                self.result.delete("1.0", "end")
                self.result.insert("1.0", summary)
                self.result.configure(state="disabled")
                self.status.set(
                    "Comparison complete; inspect the report for examples and pack hashes."
                )
                return
            check = report.get("expectations")
            verdict = (
                "Expected proposals match"
                if check and check["passed"]
                else (
                    "Expected proposals differ"
                    if check
                    else "No expected results supplied"
                )
            )
            summary = (
                f"{report['pack']['id']} • {report['pack']['version']}: {verdict}\n"
                f"{report['observations']} observations; {sum(report['proposals_by_skill'].values())} proposals; "
                f"{report['unmatched_observations']} unmatched. No desktop input.\nReport: {path}"
            )
            examples = [
                example
                for example in report["proposal_examples"]
                if "resolved_action" in example
            ]
            if examples:
                bindings = list(
                    dict.fromkeys(
                        f"{e['resolved_action']['id']} → {e['resolved_action']['key']} ({e['resolved_action']['duration_ms']} ms)"
                        for e in examples
                    )
                )
                summary += "\nExample bindings: " + ", ".join(bindings[:4])
            self.result.configure(state="normal")
            self.result.delete("1.0", "end")
            self.result.insert("1.0", summary)
            self.result.configure(state="disabled")
            self.status.set(
                "Replay complete. This checks labeled-state rules, not vision or gameplay."
            )
        except Exception as exc:
            self.error(exc)
        finally:
            self.future = None
            self.run_button.configure(state="normal")
            self.compare_button.configure(state="normal")
            self.compare_runs_button.configure(state="normal")

    def error(self, exc):
        self.status.set(str(exc))
        messagebox.showerror("Workshop", str(exc), parent=self)

    def close(self):
        if not self.discard_ok():
            return
        if self.poll_id:
            self.after_cancel(self.poll_id)
        self.worker.shutdown(wait=False, cancel_futures=True)
        self.destroy()


if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--library",
        type=Path,
        help="Writable local pack library supplied by the Windows host",
    )
    parser.add_argument(
        "--pack", type=Path, help="Open a teaching pack from the Observe workspace"
    )
    parser.add_argument(
        "--dodge-session",
        type=Path,
        help="Open the Dodge baseline with a prepared replay session",
    )
    args = parser.parse_args()
    app = Workshop(args.library)
    if args.pack:
        app.open_path(args.pack, confirm=False)
    if args.dodge_session:
        app.open_path(SAMPLES / "dodge/pack.json", confirm=False)
        app.session.set(str(args.dodge_session))
        app.expected.set("")
    app.mainloop()
