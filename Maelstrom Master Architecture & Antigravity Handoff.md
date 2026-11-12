# **Maelstrom Master Architecture & Antigravity Handoff Specification**

# **1\. Executive Vision & System Identity**

Maelstrom is a pure-Rust, local-first automation IDE and dual-loop control engine designed around a **Deckbuilder / Spellbook metaphor** (initially themed for *Wizard101* via `AutoWizard101`, extensible to arbitrary game workspaces).

Instead of relying on brittle monolithic scripts or uncoordinated Python/C\# glue, Maelstrom unifies:

* **A Visual Card-IDE Workspace (`maelstrom-ide`):** Users pick a game profile (`Wizard101.toml`), attach to a target client window (`WizardGraphicalClient.exe`), and compose automation workflows by stacking configurable **Macro Cards** into playable **Decks** or triggering cards instantaneously as **One-Shots**.  
* **A Dual-Loop Subsumption Control Engine (`maelstrom-core` \+ `maelstrom-reflex` \+ `maelstrom-fallback`):**  
  * **Reflex & Smoothing Layer:** A fast local inference model that bridges "backend blind spots" (camera drift, variable loading delays, UI jitter, smooth mouse trajectories) between high-level user/deck intent and raw client actuation.  
  * **Deterministic Mechanical Fallback:** A rock-solid, invariant-checked Card Deck runner (plus an escape-hatch bridge for legacy Python/C\# scripts) that immediately takes over whenever reflex confidence drops, post-conditions fail, or anomalies occur—logging the takeover window to self-train the reflex model via behavioral cloning and DAgger.

# **2\. Rust Workspace Crate Topology & Invariants**

Organize the repository as a strict Cargo workspace with hard dependency boundaries so UI rendering, OS actuation, deterministic card execution, and ML reflex inference never leak into one another.

| Crate | Path | Responsibility | Strict Architectural Invariants |
| :---- | :---- | :---- | :---- |
| **`maelstrom-core`** | `crates/maelstrom-core` | Domain types (`MacroCard`, `Deck`, `Intent`, `ObservationFrame`, `ControlAction`), Arbiter confidence gate, and async command channel protocols. | **Zero OS/UI dependencies.** Must compile cleanly without Win32, `egui`, or ML runtime crates. Pure state transitions and traits. |
| **`maelstrom-platform`** | `crates/maelstrom-platform` | Windows client attachment (`FindWindowW`, DPI/rect tracking), zero-copy DXGI screen capture (`xcap` / `windows-capture`), WASAPI audio triggers, and background/foreground input injection (`SendInput` / `PostMessage`). | All `unsafe` Win32 FFI calls are isolated here behind safe trait implementations (`ScreenCapturer`, `InputActuator`). |
| **`maelstrom-fallback`** | `crates/maelstrom-fallback` | Deterministic `DeckRunner` state machine, One-Shot express executor, `SubDeck` recursive stacker, native vision/OCR matchers (`imageproc`, `ocrs`), and legacy external script bridge. | Must guarantee clean cancellation (`Panic Stop / F12`) and deterministic post-condition verification on every card step. |
| **`maelstrom-reflex`** | `crates/maelstrom-reflex` | Lightweight local policy inference (`ort` / `burn`), trajectory spline smoother, and zero-overhead ring-buffer telemetry recorder for DAgger takeover datasets. | Bounded inference latency (\< 5 ms per tick). Never blocks the fallback runner if no trained model weights are loaded. |
| **`maelstrom-ide`** | `crates/maelstrom-ide` | Dockable GPU workspace (`eframe` \+ `egui` \+ `egui_tiles`) with dynamic game-profile theming (e.g., *Wizard101* Spiral Spellbook theme), custom card canvas painter, and live Arbiter confidence telemetry. | Communicates with the engine strictly via `tokio`/`crossbeam` message passing. Zero blocking I/O on the render thread. |

# **3\. Core Data Model (`maelstrom-core`)**

Separate reusable **Card Definitions** from **Card Instances** inside a Deck so cards can be stacked, nested (`SubDeck`), played as One-Shots, or modified non-destructively via **Enchantments**.

```
use serde::{Deserialize, Serialize};
use uuid::Uuid;

/// Determines card frame styling, syntax accents, and IDE library filtering
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
pub enum AutomationSchool {
    Combat,      // Fire/Storm: Duel circle detection, card selection, pass/flee
    Navigation,  // Balance: Waypoint pathing, realm hopping, teleport marks
    Economy,     // Myth/Death: Bazaar sniping, quick-selling, gardening
    Maintenance, // Life/Ice: Potion refill, pet snack minigames, anti-AFK
    MetaLogic,   // Astral (Sun/Star/Moon): Loops, vision branches, nested Sub-Decks
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub enum ExecutionMode {
    OneShot,                             // Fires immediately on click/hotkey and exits
    SequentialStep,                      // Runs inside a Deck queue; yields CardResult
    ContinuousAura { interval_ms: u64 }, // Passive background watcher (e.g., popup dismissal)
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub enum FailPolicy {
    Retry { max_attempts: u8, backoff_ms: u64 },
    SkipCard,
    AbortDeck,
    JumpToSlot(usize),
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct CardEnchantment {
    pub repeat_count: u32,
    pub pre_delay_ms: u64,
    pub post_delay_ms: u64,
    pub jitter_px: u32,
    pub on_fail: FailPolicy,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub enum CardPayload {
    InputSequence(Vec<InputEvent>),
    VisionWait {
        template_id: String,
        confidence: f32,
        timeout_ms: u64,
        click_on_match: bool,
    },
    RegionCheck {
        region: (u32, u32, u32, u32),
        expected_pattern: String,
    },
    SubDeck {
        deck_id: Uuid, // Enables recursive macro composition
    },
    LegacyExternal {
        command: String,
        args: Vec<String>,
        timeout_ms: u64,
    },
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct MacroCard {
    pub id: Uuid,
    pub name: String,
    pub school: AutomationSchool,
    pub pip_cost: u8,
    pub description: String,
    pub mode: ExecutionMode,
    pub payload: CardPayload,
    pub default_enchantment: CardEnchantment,
}
```

# **4\. Dual-Loop Arbiter & Self-Training Pipeline**

## **Execution Flow**

1. **Intent Dispatch:** Active Deck cursor (or One-Shot trigger) emits current `Intent` \+ target post-condition to the **Arbiter**.  
2. **Passive Aura Pre-Check:** Active `ContinuousAura` cards inspect the latest `ObservationFrame` for global interrupts (e.g., connection lost, Crowns shop popup, low mana globe).  
3. **Confidence Gate:**  
   * If `ReflexPolicy` returns `confidence >= threshold` and no invariant is violated, the Arbiter executes the smoothed continuous `ControlAction`.  
   * If `confidence < threshold`, a deadline expires, or an unknown UI state appears, the Arbiter seamlessly hands control to `maelstrom-fallback` to execute the deterministic `CardPayload` step.  
4. **DAgger Telemetry Capture:** Every tick during human manual play or `maelstrom-fallback` recovery writes `(intent_id, downsampled_obs, action_taken, card_outcome)` to a lock-free ring buffer (`TakeoverReplayBuffer`) for offline/background policy updates.

## **Arbiter Trait Contract**

```
pub enum ControlSource {
    ReflexModel { confidence: f32 },
    MechanicalFallback { card_id: Uuid, step: usize },
}

pub struct ArbiterDecision {
    pub source: ControlSource,
    pub action: ControlAction,
    pub record_for_training: bool,
}

pub trait PolicyController: Send {
    fn propose(&mut self, intent: &Intent, obs: &ObservationFrame) -> (ControlAction, f32);
    fn on_outcome(&mut self, intent: &Intent, succeeded: bool);
}
```

# **5\. Workspace IDE Layout (`maelstrom-ide` — *Wizard101* Theme)**

When `Wizard101.toml` is loaded, `egui_tiles` configures five docked panes styled with dark arcane parchment (\#1A162B), gold filigree borders (\#D4AF37), and School-coded card frames:

1. **Left Activity Bar & Game Switcher:** Workspace profile selector \+ client process attachment (`WizardGraphicalClient.exe` PID, window handle, resolution lock).  
2. **Center-Top (The Duel Circle / Active Hand):** Horizontal/timeline stack of queued cards for the active Deck. Supports drag-and-drop reordering, loop wrapping, live glow on the currently executing card, and instant "Play One-Shot" buttons.  
3. **Center-Bottom (The Spellbook / Card Library):** Searchable drawer of atomic cards, saved `SubDeck` combos, and legacy script cards filtered by `AutomationSchool`.  
4. **Right Dock (Enchantment & Property Inspector):** Live editor for the selected card's coordinates, vision template confidence, `CardEnchantment` modifiers, and failure policies.  
5. **Bottom Dock (Health/Mana Telemetry & Arbiter Log):** Simulated/OCR client state globes, bounding-box vision preview, live Reflex vs. Fallback confidence meter, and global Panic Stop (`F12`).

# **6\. Antigravity Execution Plan (Phased Implementation Prompts)**

Feed the following phases sequentially into your local Antigravity session to scaffold, verify, and build Maelstrom cleanly:

## **Phase 1: Workspace Skeleton & `maelstrom-core`**

* **Goal:** Initialize the 5-crate Cargo workspace (`maelstrom-core`, `maelstrom-platform`, `maelstrom-fallback`, `maelstrom-reflex`, `maelstrom-ide`) and implement the full serializable `MacroCard`, `Deck`, `CardEnchantment`, `Arbiter`, and `PolicyController` types in `maelstrom-core`.  
* **Verification:** `cargo check --workspace` and `cargo test -p maelstrom-core` (round-trip TOML serialization tests for nested `SubDeck` cards and unit tests for Arbiter fallback switching when reflex confidence drops below threshold).

## **Phase 2: Deterministic Runner & Legacy Bridge (`maelstrom-fallback`)**

* **Goal:** Implement the async `DeckRunner` state machine with `OneShot` preemption, `ContinuousAura` background watchers, `SubDeck` stack frame pushing/popping, `PanicStop` cancellation token, and `CardPayload::LegacyExternal` subprocess runner so existing Python/C\# scripts run immediately as Cards.  
* **Verification:** `cargo test -p maelstrom-fallback` using mock `ScreenCapturer` and `InputActuator` implementations to verify pause, resume, panic abort, and `SubDeck` recursion.

## **Phase 3: Windows Platform Layer (`maelstrom-platform`)**

* **Goal:** Implement Win32 window attachment (`WizardGraphicalClient.exe`), client-area coordinate translation, DXGI frame capture (`xcap` or `windows-capture`), and safe input dispatch (`SendInput` / `PostMessage`) behind `maelstrom-core` traits.  
* **Verification:** `cargo build -p maelstrom-platform` on Windows \+ standalone smoke binary verifying window handle detection and frame capture latency.

## **Phase 4: `egui_tiles` Card-IDE Workspace (`maelstrom-ide`)**

* **Goal:** Build the `eframe` \+ `egui_tiles` 5-pane dockable IDE with the *Wizard101* Spellbook theme, custom `egui::Painter` spell card widgets (school borders, pip badges, progress bars), drag-and-drop Deck stacking, and live Arbiter telemetry pane.  
* **Verification:** `cargo run -p maelstrom-ide` launches cleanly at 60+ FPS, loads sample `.toml` decks (e.g., a 4-card *Halfang Farming Deck*), and fires mock One-Shot and Deck runs with live visual card highlighting.

## **Phase 5: Reflex Policy & DAgger Replay Buffer (`maelstrom-reflex`)**

* **Goal:** Implement `TakeoverReplayBuffer` (lock-free SPSC ring buffer serializing takeover windows to disk) and `OnnxReflexPolicy` via `ort` (with a safe stub fallback when no `.onnx` model is present).  
* **Verification:** `cargo test -p maelstrom-reflex` verifying zero-allocation tick recording and seamless Arbiter handoff between stubbed reflex predictions and mechanical fallback cards.

