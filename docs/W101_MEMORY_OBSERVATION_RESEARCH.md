# Wizard101 memory observation: evidence and boundaries

**Current decision, 2026-11-10:** the owner expanded the W101 exception to
reviewed client-side functionality, including necessary hooks, injection and
memory reads/writes. Network manipulation remains excluded; installed game files
stay read-only. See [the current decision](W101_CLIENT_SIDE_EXCEPTION_20261110.md).
The earlier scope below is historical; its technical findings remain evidence,
not a restriction overriding the owner's expanded approval. No adapter is made
operational merely by this documentation update.

Research date: **2026-11-09 UTC**. This is documentation-only work.
No installed game files, game processes, process memory or protection mechanisms
were inspected or tested. No attachment, injection, input or network-control
operation was performed. See the
[structured evidence ledger](../research/w101-memory-observation-20261109.json).

**Reading process memory is technically distinct from observing pixels or
sending UI input. Read-only does not guarantee that access is permitted,
invisible, or unrestricted.** Public evidence reviewed here does not establish
Wizard101's current memory-read detection mechanism, protected-process status,
driver use or response to an external reader.

## Four capabilities that must remain separate

This table is a proposed Maelstrom capability taxonomy, not a claim that any
particular legacy repository implements a pure version of a capability.

| Capability | What it observes or changes | What it does not establish |
| --- | --- | --- |
| Read-only installed files | On-disk resources or configuration; an offline copy may supply names, artwork or structural hints | Current runtime values, permission to read process memory, or correctness of an old resource in the current client |
| Screen/UI observation | Rendered pixels and supported exposed UI information; recording/OCR can describe what is visible | Hidden game state or authority to send inputs; UI observation and UI actions need separate capability flags |
| Read-only external process memory | Data from another running process's address space, subject to operating-system access controls | Permission to modify that process, invisibility, publisher approval, or that a particular address/value is correctly interpreted |
| Injection, hooks, writes or network control | Instrumentation or control that can change execution, memory or communications | Equivalence to a read-only observer merely because the final API returns observations |

A mouse click or key press is a UI action; a screenshot is an observation.
A process name or window title is not automatically a memory read. The precise
method matters. Conversely, a legacy “read state” helper may initialize hooks
or another backend before returning data. Classify its complete dependency and
startup path, not its function name. These are review criteria, not findings
that a particular imported project has passed.

## What Windows documentation actually supports

Microsoft distinguishes memory-read rights from memory-write and address-space
operation rights. Opening a process is subject to its access controls; protected
process restrictions include memory reads. This documents an OS mechanism,
**not evidence that Wizard101 is a protected process**.
[Process security and access rights](https://learn.microsoft.com/en-us/windows/win32/procthread/process-security-and-access-rights)

The documented memory-read function copies bytes from another process into the
caller and requires readable access to the requested region. This is a different
interface from clicking a game window; access failure is possible even when the
operation is intended only to read.
[ReadProcessMemory reference](https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-readprocessmemory)

Windows also documents callbacks receiving information when process/thread
handles are opened, including requested access. The cited structure allows
restriction of listed modifiable rights. Its published list does **not** include
`PROCESS_VM_READ`; do not use it as proof that every read request can be stripped
through that particular documented flag list. Handle-open visibility is also
not the same as an audit event for every subsequent memory read.
[Handle callback structure](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/wdm/ns-wdm-_ob_pre_create_handle_information)

The resulting inference is limited: external access can encounter protection,
and handle acquisition is not inherently invisible to suitable system software.
There is no basis here to promise that “read-only means undetected,” nor to claim
that Windows necessarily alerts Wizard101 after every read.

## Wizard101-specific evidence

Two additional focused searches of the official Wizard101 domain covered
anti-cheat/security/software and explicit process-memory terminology. They
found no official technical explanation identifying a current memory-reader
detector. This is a bounded search result, not proof that no such documentation
or protection exists.

The publisher's privacy policy describes game-service logs, gameplay data and
protection programs for fraud/manipulation prevention. Section 7 explicitly says
practices depend on the game/service; section 7.2.4 appears in the
EU/EEA/UK/Brazil portion. It names no memory-read API detector or particular
driver and does not establish this Windows client's implementation.
[Privacy policy, sections 7 and 7.2](https://www.wizard101.com/patchClient/privacypolicy)

The retrieved Terms of Use primarily defines and governs the company's Site,
whose definition can include games and downloadable software. It is not
technical detection documentation, and this research does not turn it into
a definitive gameplay-specific permission ruling.
[Retrieved Terms of Use](https://www.wizard101.com/patchClient/termsofuse)

A publisher account-security announcement, updated February 23, 2023, warns
about malicious cheat/generator downloads. That concerns account security and
does not document the game's current memory protections.
[Account-security announcement](https://www.wizard101.com/forum/headmasters-announcements/warning-protecting-your-account-8ad6a42b6cb33647016cd4968f4b461e)

Old player forum assertions about cheating were not treated as publisher
technical evidence. A recent crawl date does not make a 2010 or 2014 discussion
a current implementation description.

## Evidence levels and chronology

| Evidence | Source date | Confidence and limit |
| --- | --- | --- |
| Distinct process rights and protected-process restrictions | Microsoft page updated 2025-07-14 | Primary OS documentation; no W101 applicability test |
| Memory-read function contract | Microsoft page updated 2022-05-14 | Primary API documentation; no local API invocation |
| Handle callback information and listed modifiable rights | Microsoft page updated 2022-02-25 | Primary driver-interface documentation; not a per-read auditing guarantee |
| Publisher privacy and Site terms | Revision dates not established from retrieved content | Primary policy statements; regional/product scope matters; no detector identified |
| Publisher account-security warning | Updated 2023-02-23 | Historical security advice; not a technical anti-cheat description |
| Current W101 memory access/detection behavior | Not observed | Unknown; no protection probe performed |

Source publication/update dates, retrieval time, client build, region, platform
and Live/Test Realm status are independent. None of the sources supplies a
verified build fingerprint for the user's running client.

## Consequences for Maelstrom and legacy review

The user explicitly approved future opt-in, read-only process observation for
W101 on 2026-11-10. It remains a **distinct, default-off capability**;
[the implementation review](W101_TELEMETRY_REVIEW_20261110.md) records the decision
and provider qualification still needed. Existing permission for Observe recording, UI
actions or legacy source review does not silently select this backend. This
research introduces no automatic fallback from failed visual recognition to
process attachment.

Each proposed adapter should declare its observation/control class, startup
side effects, required game access, data fields, provenance and whether it can
operate without hooks or writes. A wrapper does not erase upstream behavior.
A future runtime value should retain its evidence source: visible UI, installed
resource, supplied fixture, or an explicitly selected process-memory source.

Read-only installed resources can help build labels and catalogues, while
screen evidence can confirm the current client. Those paths can progress without
resolving the memory decision. Dynamic values still need freshness, identity,
version and ambiguity checks; changing the observation source does not remove
those requirements.

If memory observation is chosen later, scope and implementation need a separate
review. No current task should attempt access escalation, protection bypass,
hidden fallback, memory dumping or automatic attachment to discover whether it
works. An unsupported or denied operation remains a reported limitation.

Open questions are narrowly stated: the active client's actual access
restrictions; whether and how this specific build observes external readers;
publisher guidance for the intended observation use; and whether a candidate
legacy adapter is truly read-only from initialization through cleanup.
None is answered by the absence of an error during a UI test.

