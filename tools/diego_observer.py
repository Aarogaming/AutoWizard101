"""Offline Dueling Diego observer: open frames, teach examples and inspect suggestions."""

import argparse
from pathlib import Path
import tempfile
import tkinter as tk
from tkinter import filedialog, messagebox, ttk

from PIL import Image, ImageDraw, ImageTk

from diego_vision import Examples, LABELS, SceneTracker, demo, report
from offline_report_store import save_report as publish_report

ROOT = Path(__file__).resolve().parents[1]


class Observer(tk.Tk):
    def __init__(self, pack=None, frames=None, demo_mode=False):
        super().__init__()
        self.title("AutoWizard101 · Diego observer · OFFLINE")
        self.geometry("1120x820")
        self.minsize(900, 700)
        self.configure(bg="#172038")
        self.pack = pack or Examples(ROOT / ".local/diego-observer/examples")
        self.tracker = SceneTracker()
        self.frames = frames or []
        self.index = 0
        self.image = None
        self.selection = None
        self.drag_start = None
        self.scale = 1
        self.offset = (0, 0)
        self.demo_mode = demo_mode
        self.replaying = False
        self.after_id = None
        self.photo = None
        self.matches = []
        style = ttk.Style(self)
        style.theme_use("clam")
        style.configure("TFrame", background="#172038")
        style.configure("TLabel", background="#172038", foreground="#f2f4fa")
        style.configure("TButton", padding=7)
        base = ttk.Frame(self, padding=16)
        base.pack(fill="both", expand=True)
        ttk.Label(
            base, text="Diego observer — offline review", font=("Segoe UI", 20, "bold")
        ).pack(anchor="w")
        ttk.Label(
            base,
            text="Reads saved images only. No game connection, recording, keys or clicks to the game.",
        ).pack(anchor="w", pady=(5, 12))
        top = ttk.Frame(base)
        top.pack(fill="x")
        for text, command in (
            ("Open frames…", self.open_frames),
            ("Open example pack…", self.open_pack),
            ("New example pack…", self.new_pack),
            ("Tutorial starter", lambda: self.guarded(self.open_tutorial)),
            ("Try demo", self.try_demo),
        ):
            ttk.Button(top, text=text, command=command).pack(side="left", padx=(0, 8))
        self.status = tk.StringVar(
            value="Open saved game frames, or try the synthetic demo."
        )
        ttk.Label(base, textvariable=self.status, wraplength=1040).pack(
            anchor="w", pady=10
        )
        self.canvas = tk.Canvas(
            base,
            bg="#0c1120",
            highlightthickness=1,
            highlightbackground="#52627c",
            height=370,
        )
        self.canvas.pack(fill="both", expand=True)
        self.canvas.bind("<Configure>", lambda _: self.draw())
        self.canvas.bind("<ButtonPress-1>", self.begin_selection)
        self.canvas.bind("<B1-Motion>", self.move_selection)
        self.canvas.bind("<ButtonRelease-1>", self.end_selection)
        ttk.Label(
            base,
            text="1. Drag around the play area → Mark play area.   2. Drag tightly around a subject → choose its label → Add example.",
            wraplength=1040,
        ).pack(anchor="w", pady=(10, 5))
        teaching = ttk.Frame(base)
        teaching.pack(fill="x")
        ttk.Button(
            teaching,
            text="Mark play area",
            command=lambda: self.guarded(self.mark_area),
        ).pack(side="left", padx=(0, 8))
        self.label = tk.StringVar(value=LABELS[0])
        ttk.Combobox(
            teaching, textvariable=self.label, values=LABELS, state="readonly", width=23
        ).pack(side="left", padx=(0, 8))
        ttk.Button(
            teaching, text="Add example", command=lambda: self.guarded(self.add_example)
        ).pack(side="left", padx=(0, 8))
        ttk.Button(
            teaching, text="Inspect frame", command=lambda: self.guarded(self.inspect)
        ).pack(side="left", padx=(0, 8))
        ttk.Button(
            teaching,
            text="Save report…",
            command=lambda: self.guarded(self.save_report),
        ).pack(side="left")
        navigation = ttk.Frame(base)
        navigation.pack(fill="x", pady=8)
        ttk.Button(
            navigation, text="Previous frame", command=lambda: self.change_frame(-1)
        ).pack(side="left", padx=(0, 8))
        ttk.Button(
            navigation, text="Next frame", command=lambda: self.change_frame(1)
        ).pack(side="left", padx=(0, 8))
        self.replay_button = ttk.Button(
            navigation, text="Replay frames", command=self.toggle_replay
        )
        self.replay_button.pack(side="left", padx=(0, 8))
        ttk.Button(navigation, text="Stop review", command=self.stop).pack(side="left")
        self.output = tk.Text(
            base,
            height=6,
            wrap="word",
            bg="#0c1120",
            fg="#eff3fa",
            font=("Segoe UI", 10),
            state="disabled",
        )
        self.output.pack(fill="x")
        self.protocol("WM_DELETE_WINDOW", self.close)
        self.bind("<Escape>", lambda _: self.stop())
        if self.frames:
            self.show_frame()

    def guarded(self, callback):
        try:
            callback()
        except (ValueError, OSError, KeyError, TypeError) as error:
            self.stop()
            self.write(str(error))
            messagebox.showerror("Observer needs attention", str(error), parent=self)

    def write(self, text):
        self.output.configure(state="normal")
        self.output.delete("1.0", "end")
        self.output.insert("1.0", text)
        self.output.configure(state="disabled")

    def open_frames(self):
        files = filedialog.askopenfilenames(
            parent=self,
            title="Choose saved frames in playback order",
            filetypes=[("Images", "*.png *.jpg *.jpeg *.bmp")],
        )
        if files:
            self.stop()
            self.tracker = SceneTracker()
            self.frames = sorted(files)
            self.index = 0
            self.demo_mode = False
            self.guarded(self.show_frame)

    def open_pack(self):
        folder = filedialog.askdirectory(
            parent=self, title="Choose an example pack folder"
        )
        if folder:
            self.stop()
            self.guarded(lambda: self.set_pack(Examples(folder)))

    def new_pack(self):
        folder = filedialog.askdirectory(
            parent=self, title="Choose an empty folder for the new pack"
        )
        if folder:

            def create():
                if any(Path(folder).iterdir()):
                    raise ValueError(
                        "Choose an empty folder so existing examples remain intact."
                    )
                self.set_pack(Examples(folder))

            self.stop()
            self.guarded(create)

    def set_pack(self, pack):
        self.pack = pack
        self.tracker = SceneTracker()
        self.matches = []
        self.selection = None
        self.draw()
        self.write(
            f"Loaded {len(pack.entries)} examples. Open a frame with the matching source size."
        )

    def try_demo(self):
        self.stop()
        self.pack, self.frames = demo(Path(tempfile.mkdtemp(prefix="diego-demo-")))
        self.tracker = SceneTracker()
        self.index = 0
        self.demo_mode = True
        self.show_frame()
        self.inspect()

    def open_tutorial(self):
        folder = ROOT / ".local/diego-observer/tutorial"
        pack_folder = ROOT / ".local/diego-observer/tutorial-pack"
        if not (pack_folder / "examples.json").is_file():
            raise ValueError(
                "The local tutorial reference pack is missing. See docs/DIEGO_OBSERVER.md for the builder."
            )
        frames = sorted(folder.glob("frame-*.jpg"))
        if not frames:
            raise ValueError("The local tutorial reference frames are missing.")
        self.stop()
        self.pack = Examples(pack_folder)
        self.frames = [str(path) for path in frames]
        self.tracker = SceneTracker()
        self.index = 0
        self.demo_mode = False
        self.show_frame()
        self.status.set(
            "HISTORICAL TUTORIAL REFERENCES — not validated on your game · 19 examples"
        )
        self.inspect()

    def show_frame(self):
        frame = self.frames[self.index]
        if isinstance(frame, Image.Image):
            self.image = frame.copy().convert("RGB")
        else:
            with Image.open(frame) as source:
                self.image = source.convert("RGB")
        self.selection = None
        self.matches = []
        name = (
            "SYNTHETIC DEMO — not game recognition"
            if self.demo_mode
            else Path(frame).name
        )
        self.status.set(
            f"{name} · Frame {self.index + 1}/{len(self.frames)} · {self.image.width}×{self.image.height} · {len(self.pack.entries)} examples"
        )
        self.draw()
        self.write(
            "Inspect this frame. Suggestions are review prompts; timing and motion are not calibrated."
        )

    def draw(self, detections=None):
        if detections is not None:
            self.matches = detections
        self.canvas.delete("all")
        if self.image is None:
            self.canvas.create_text(
                25,
                25,
                text="Open saved frames or try the demo.",
                fill="white",
                anchor="nw",
                font=("Segoe UI", 14),
            )
            return
        width, height = max(1, self.canvas.winfo_width()), max(
            1, self.canvas.winfo_height()
        )
        self.scale = min(width / self.image.width, height / self.image.height)
        display = self.image.copy()
        painter = ImageDraw.Draw(display)
        if self.pack.roi and self.image.size == self.pack.size:
            x, y, w, h = self.pack.roi
            painter.rectangle((x, y, x + w - 1, y + h - 1), outline="#59d6bc", width=2)
        for item in self.matches:
            x, y, w, h = item["box"]
            painter.rectangle((x, y, x + w - 1, y + h - 1), outline="#ffd460", width=2)
            painter.text(
                (x, max(0, y - 13)),
                item["label"] + f" {item['similarity']:.3f}",
                fill="#ffd460",
            )
        resized = display.resize(
            (
                max(1, round(display.width * self.scale)),
                max(1, round(display.height * self.scale)),
            )
        )
        self.offset = ((width - resized.width) // 2, (height - resized.height) // 2)
        self.photo = ImageTk.PhotoImage(resized)
        self.canvas.create_image(*self.offset, anchor="nw", image=self.photo)

    def point(self, event):
        x = int((event.x - self.offset[0]) / self.scale)
        y = int((event.y - self.offset[1]) / self.scale)
        return max(0, min(x, self.image.width)), max(0, min(y, self.image.height))

    def begin_selection(self, event):
        if self.image is not None:
            self.stop()
            self.drag_start = self.point(event)
            self.selection = None

    def move_selection(self, event):
        if self.drag_start is not None:
            end = self.point(event)
            x, y = self.drag_start
            self.selection = (
                min(x, end[0]),
                min(y, end[1]),
                abs(x - end[0]),
                abs(y - end[1]),
            )
            self.canvas.delete("selection")
            sx, sy, w, h = self.selection
            ox, oy = self.offset
            self.canvas.create_rectangle(
                ox + sx * self.scale,
                oy + sy * self.scale,
                ox + (sx + w) * self.scale,
                oy + (sy + h) * self.scale,
                outline="white",
                width=2,
                tags="selection",
            )

    def end_selection(self, event):
        self.move_selection(event)
        self.drag_start = None

    def require_selection(self):
        if self.image is None or self.selection is None:
            raise ValueError("Open a frame, then drag to select an area.")

    def mark_area(self):
        self.require_selection()
        self.pack.configure(self.image.size, self.selection)
        self.pack.save()
        self.tracker = SceneTracker()
        self.draw()
        self.write(
            "Play area saved. Now select a recognizable subject and add its label."
        )

    def add_example(self):
        self.require_selection()
        self.pack.add(self.image, self.selection, self.label.get())
        self.tracker = SceneTracker()
        self.status.set(
            f"Example saved · {len(self.pack.entries)} examples · {self.pack.folder}"
        )
        self.inspect()

    def inspect(self):
        if self.image is None:
            raise ValueError("Open a saved frame first.")
        result = report(
            self.image, self.pack, tracker=self.tracker, frame_id=self.index
        )
        self.draw(result["detections"])
        matches = (
            ", ".join(
                f"{item['label']} ({item['similarity']:.3f})"
                for item in result["detections"]
            )
            or "none"
        )
        self.write(
            f"Matches: {matches}\n"
            + "\n".join(result["suggestions"])
            + "\nSimilarity is not a probability. Unmatched subjects remain unknown."
        )
        return result

    def save_report(self):
        result = self.inspect()
        path = filedialog.asksaveasfilename(
            parent=self,
            title="Save detection report",
            defaultextension=".json",
            filetypes=[("JSON", "*.json")],
        )
        if path:
            publish_report(path, result)

    def change_frame(self, direction):
        self.stop()
        if self.frames:
            self.index = max(0, min(self.index + direction, len(self.frames) - 1))
            self.guarded(self.show_frame)
            if self.pack.roi:
                self.guarded(self.inspect)

    def toggle_replay(self):
        if self.replaying:
            self.stop()
        elif self.frames:
            self.replaying = True
            self.replay_button.configure(text="Pause replay")
            self.replay()

    def replay(self):
        self.after_id = None
        if not self.replaying:
            return
        try:
            self.show_frame()
            self.inspect()
            if self.index == len(self.frames) - 1:
                self.stop()
                return
            self.after_id = self.after(900, self.advance_replay)
        except (ValueError, OSError) as error:
            self.stop()
            self.write(str(error))

    def advance_replay(self):
        self.after_id = None
        if self.replaying:
            self.index += 1
            self.replay()

    def stop(self):
        self.replaying = False
        if self.after_id is not None:
            self.after_cancel(self.after_id)
            self.after_id = None
        self.replay_button.configure(text="Replay frames")

    def close(self):
        self.stop()
        self.destroy()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pack", type=Path)
    parser.add_argument("--frame", type=Path)
    parser.add_argument(
        "--output",
        type=Path,
        help="Write an offline JSON report instead of opening a window.",
    )
    parser.add_argument("--demo", action="store_true")
    args = parser.parse_args()
    if args.demo:
        pack, frames = demo(Path(tempfile.mkdtemp(prefix="diego-demo-")))
    else:
        pack = Examples(args.pack or ROOT / ".local/diego-observer/examples")
        frames = [args.frame] if args.frame else []
    if args.output:
        if not frames:
            parser.error("--output requires --frame or --demo")
        source = frames[0]
        if isinstance(source, Image.Image):
            result = report(source, pack)
        else:
            with Image.open(source) as image:
                result = report(image, pack)
        result["syntheticDemo"] = args.demo
        publish_report(args.output, result)
    else:
        Observer(pack, frames, args.demo).mainloop()


if __name__ == "__main__":
    main()
