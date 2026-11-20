# Gaming model trials

Trial date: 2026-11-03. These are isolated, observe-only research experiments,
not Maelstrom features or game-control qualification. No inputs were sent to a
game. User recordings and manually reviewed labels were not modified.

## Test material and hardware

Both experiments used the same previously recorded Dodge the Creeps session.
A three-second clip beginning at 20 seconds was extracted from the original
capture; NitroGen was given six separately sampled images at 20.0, 20.6, 21.2,
21.8, 22.4 and 23.0 seconds. The recording contains no synchronized input
events. Visual player displacement was previously inspected, but it is not
ground truth for the controls or for a different policy's preferred actions.

The trials used an NVIDIA GeForce RTX 5070 Ti with 16,303 MiB reported memory,
driver 617.42, Python 3.12 and PyTorch 2.11.0+cu128. Other processes and normal
desktop activity were not controlled. Timings are observations, not guarantees.

## NitroGen: action proposals from individual frames

[NitroGen](https://github.com/MineDojo/NitroGen) code revision
`32608444660950ffda95e1e57c79632ad65bea10` and
[model](https://huggingface.co/nvidia/NitroGen) revision
`584c8dded734d032f07a4bcc0ccb330e703298c4` were used. The research license
limits use to non-commercial research; reassess the actual terms before any
distribution or commercial use. NitroGen predicts gamepad action chunks, not
native keyboard events, and its authors explicitly describe limited planning.

An isolated Transformers 4.57.3 / Diffusers 0.35.2 compatibility overlay was
needed. The cloned loader was changed from `weights_only=False` to
`weights_only=True`, which successfully loaded the official checkpoint. Its
SigLIP2 backbone was cached locally. The trial invoked the inference session
directly; it did not launch the upstream game-control script or network server.
The upstream server binds to all interfaces and deserializes pickle requests;
do not expose it as an integration endpoint.

Model loading took 2.32 seconds. The first prediction took 15.83 seconds and
the following five took 183–233 ms each (median 214 ms), measured around the
model call with GPU synchronization. Peak PyTorch-allocated GPU memory was
about 1,912 MiB. These six predictions are far too few to characterize tail
latency or quality, and do not measure capture-to-input latency. Results included
directional-pad and face-button proposals. Their suitability for this game was
not established; the proposals are neither recorded user actions nor proof of
successful play. No automatic mapping or execution was enabled.

## Generalist-IDM: inferred actions from a recorded clip

[D2E](https://github.com/worv-ai/D2E) code revision
`1adbc09f81810412413ff046a80e8333df80647d` and
[Generalist-IDM-1B](https://huggingface.co/open-world-agents/Generalist-IDM-1B)
revision `eae16486df3169aafe4a1d74fb2375185b5dc641` were used. The model
card states Apache 2.0; dependency and redistribution terms still require
review before product integration.

The upstream Windows trial initially substituted blank images because its
default media decoder was unavailable. The isolated copy was changed to use
PyAV through the media reference and fail on decoding errors instead. The valid
run processed a three-second clip in roughly 53 seconds from preprocessing to
completion, including model loading. It emitted 30 mouse button events and no
keyboard events, despite visible movement; the mouse suggestions followed a
highly repetitive click pattern. With no recorded inputs, we cannot score them
against the user's actual actions, but this output is not a useful movement
annotation for this clip. The run also logged 57 invalid generated events; no
blank-frame fallback occurred after the decoder change. No model suggestion
was accepted as a label or used to train a pack.

The upstream MCAP contains temporary video references, so the experiment also
exports action-only JSON with clip times and source-video offsets. That JSON is
marked as unverified model output and is never a replay source.

## Implications and next gate

Keep both as optional research candidates. Neither result establishes a generic
game player, reliable action labels, or readiness for live game control. Before
choosing or integrating a model, collect an opt-in demonstration with
synchronized, target-scoped inputs, video and focus timestamps; reserve a
separate session for evaluation. Compare keyboard-event precision and recall,
timing error, incorrect/unknown outputs, inference latency and resource use
against the existing visual tracker. Test NitroGen on a gamepad-compatible test
game with an explicit action mapping and measured outcomes. Never train or
execute raw model guesses as if they were verified actions.

Local experiment assets and scripts are under `.local/model-research/` and are
not part of the installer. They include upstream repositories and third-party
model weights; see their licenses before copying or distributing them.

## Automated Dodge input test

After a normal Windows activation attempt failed, the target window's
accessibility **Raise** action brought Dodge forward. A 40-second recording
captured a test click and two complete automated arrow-key presses. All were
marked injected. The input log identifies the verified video by SHA-256 and its
first-frame timestamp agrees with the capture report. A visual check found the
start menu before the click and the Get Ready screen afterward. This is evidence
of event collection and coarse temporal correspondence, not a calibrated
input-to-photon test or a human demonstration. The game capture averaged about
17.3 fps and the first-frame callback offset was about 113 ms in this run.

Generalist-IDM was run on the 16–19 second segment containing the recorded
Start click. The model produced no valid action events and logged 58 apparent
invalid event decodes. Subsequent inspection identified these as normal
next-screen markers incorrectly classified as invalid by the trial runner. A
short 0.8-second rerun classified 14 boundaries correctly, with no invalid or
valid action events. A complete comparison after that correction remains
pending. The original run supplied no usable prediction for the recorded click;
do not report an accuracy percentage from it. The clip was
resampled for the model, and no game input was sent by the model itself.
