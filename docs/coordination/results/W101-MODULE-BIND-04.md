# W101 module semantic bindings

2026-11-11 source-only. Pure bindings compile existing sigil/menu/native Idle
modules and map bounded UI-tree evidence plus trusted contextual recognition.
The UI tree alone supplies no specific sigil identity or menu click geometry.
Missing context therefore waits; no arbitrary UI node is promoted to a control.

Known modes: sigil X interaction to a verified interaction menu; open game menu;
native Idle entry. Dialogue advancement, arbitrary menus, Idle exit, navigation
and purchases are outside these bindings. Target identity, source/version,
acquisition freshness, loading and stable state agreement are checked.

The visual context adapter reuses existing W101MenuRecognition results with exact
binding/geometry and300ms freshness, accepting only local-windows-ocr provenance.
Saved/manual/injected OCR origins are rejected. Outside readiness and interaction
identity still require independent trusted evidence. The fixture test explicitly
uses a synthetic local-producer tag after verifying rejection of saved provenance;
this does not claim an actual live OCR run or qualification. The dispatcher must
independently bind current target/control geometry before an input. Context flags
are an internal trusted adapter contract, not serialized permission.
The existing visual interaction-menu classifier recognizes Dance specifically;
other interactables need independently reviewed semantic adapters.

Tests include the existing hashed graphics-menu fixture, its saved OCR through
classification→context→bindings→ModuleRunner, plus unknown prompt identity,
dialogue/loading/stale/target rejection. Saved capture clocks were absent, so
test timing is explicitly synthetic. Outcome bindings are never fabricated.

No native build, gameplay, process reads or desktop use performed by this agent.
Coordinator owns formatting, regression checks and readiness promotion.
