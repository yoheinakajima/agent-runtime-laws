# Review synthesis and disposition

Status: incorporated into the review branch on 2026-08-28. Publication remains
gated.

## Consensus

All three reports agree that the strongest contribution is property-indexed
fork safety across separate effect footprint, replay-source, and lifecycle
dimensions. They also agree that the paper should be narrower and more precise:
confluence is supporting evidence, FsCheck supplies checked properties rather
than proofs, and the bridge result cannot carry a headline claim without public
provenance.

## Accepted and implemented

| Issue | Disposition |
|---|---|
| Bridge result was over-promoted relative to its provenance | Removed from the abstract; retained as a clearly local, illustrative study. A deliberate redacted release bundle was created and verified in the separate migration-lab repository, but no public remote was created. |
| 36,251 unsound-cut arithmetic was unexplained | Added the direct accounting: 36,227 request/response pairs create one open-boundary cut each, and 24 failed runs end with one unmatched request, also one cut each. |
| 5,540 store runs versus 4,919 verifier runs was unreconciled | Added the exact partition: 4,919 verifier-covered runs, 586 other completed runs outside that contract, and 35 incomplete runs. |
| Related work was too local | Added active-database ECA rules, Newman, CRDTs, sagas, durable workflow engines, event sourcing, and LangGraph time travel, while bounding the delta to per-cut, property-indexed assessment over archived agent traces. |
| Confluence dominated the title despite thinner production evidence | Retitled to “Replay Is a Family of Assertions: Fork-Safety in Event-Sourced Agent Runtimes” and treated confluence as a supporting qualification. |
| “Law” vocabulary overstated the method | Shifted running prose toward executable contracts, checked properties, identities, and regression fixtures. |
| Replay hierarchy and predecessor-paper status were buried | Moved the hierarchy to the introduction and added a claim-status table against “The Log is the Agent.” |
| Operational semantics were described but not shown | Added small-step process-event and fire-activation rules and explicit terminal outcomes. |
| Interference and effect-boundary mechanisms lacked figures | Added an interference-path figure and a cut-through-request figure. |
| Counterfactual-world equivalence and target environment were underspecified | Indexed world equivalence by declared observables and made the target-environment evidence premise explicit. |
| Unclassified source events could be mistaken for a fail-closed footprint result | Made adapter coverage an explicit profile premise; unclassified events block a complete cross-runtime safety claim. |
| Replay-grade lattice section was over-weighted | Compressed it to supporting machinery. |
| Paper retained internal process notes | Removed the line-budget confession and minimality/research-brief genealogy. |
| Runtime guidance was abstract | Added a property-parameterized fork API with `Ok`, `Refuse`, and `Conditional` outcomes. |

## Accepted, but not completed in this round

| Issue | Reason and next evidence required |
|---|---|
| Publish the bridge study | The source and redacted bundle are now commit-bound locally and the bundle verifier passes. Creating a public repository or remote is an owner publication decision. |
| Public hard effect-boundary fork | The 121 public forks are domain-only cuts. A hash-pinned fork after a committed recorded oracle call, with a zero-reexecution receipt, is still required. |
| Production confluence inventory | ActiveGraph ordering was source-audited, but no complete production read/write/trigger dependency inventory exists. |
| External runtime corpus | Both evaluated corpora are ActiveGraph-family. A LangGraph, Temporal, or other independent trace remains future validation. |
| Randomized scheduler fuzzing | The current four explicit schedules and generated families remain bounded evidence. Randomized and exhaustive finite-state exploration are queued. |
| Classification sensitivity table | Useful, but secondary to obtaining a real effect-boundary fork and external corpus. |
| ActiveGraph runtime enforcement change | The paper now specifies the API contract, but this round does not modify or release the production ActiveGraph runtime. |

## Not adopted

- The bridge source was not added to the dirty public `activegraph-bridge`
  checkout. The study belongs to its separate `activegraph-model-migration-lab`
  repository, whose contribution rules require a deliberate redacted release
  bundle.
- No mechanized proof, performance benchmark, or minimal confluence theorem was
  added; these remain explicit non-goals.
- No public repository, preprint, or Explore Science submission was created.

## Release gate after this round

The manuscript may advance to another internal review round after code, paper,
and PDF QA pass. It is not publication-ready until at least the critical bridge
provenance decision is resolved. The established four-model review bundle also
lacks a preserved fresh Codex review, and the empirical hard cases above remain
open rather than silently promoted to claims.

## Post-review hardening addendum — 2026-08-28

The “public hard effect-boundary fork” item above is now discharged for a
bounded conformance case by public activegraph-bridge revision
`8855d3a9e779362f713b08bceb58d7d5db671c7d` (PR #2). The fixture records an
actual child fork after one committed `one_shot + recorded` offline oracle
effect. Its generator observes no second fixture-oracle call. The verifier
checks the inherited recorded outcome, receipt/log consistency, and a
fork-bound caller assertion under a configured public conformance key. It does
not validate the asserted environment contents or establish provider
authenticity or production attestor identity.

This addendum does not rewrite the historical review. The separate 24-run
migration-lab study still needs an owner publish-or-cut decision. A fresh
independent Codex report is now preserved as `codex.md`; its disposition appears
in the later addendum below.

## Independent Codex disposition addendum — 2026-08-28

The independent Codex review targeted exact commit
`768a30542b61951a047b80f0c8186a099634b3a4`. It found no Critical defect or
numerical invalidation, but identified two Major claim-discipline problems and
three Minor reporting inconsistencies. The raw report remains unchanged in
`codex.md`.

| Finding | Disposition in the submission candidate |
|---|---|
| Kernel snapshot semantics contradicted the production-source account in the authoritative ledgers | Accepted. `EVIDENCE.md`, `FINDINGS.md`, the confluence section, the limitations, and the outline now distinguish fixed same-trigger eligibility from per-handler state refresh, and kernel semantics from production semantics. |
| Bridge headline treated a signed caller assertion as verified environment truth | Accepted. The abstract, contributions, validation, discussion, limitations, conclusion, ledgers, and READMEs now attribute the zero-call observation to the fixture generator and describe the verifier as checking receipt/log consistency plus a fork-bound caller assertion. |
| Bridge artifact could read as released v0.2 | Accepted. The manuscript and ledgers now identify the implementation as an unreleased open draft-PR revision. |
| Local-study source and redacted-release hashes were conflated | Accepted. The two checksum namespaces are labeled separately in the evidence ledger and manuscript. |
| Fork-audit SQL vocabulary differed from the normalizer | Accepted. The SQL now mirrors the normalizer's request/response vocabulary and the method states that contract. |
| Private paper repository and unpublished local study | Open owner gates. The repository must be made public and frozen; the 24-run study must be published or removed before submission. |

Disposition QA passed on the corrected candidate: the F# build has zero
warnings and errors; 50/50 tests, conformance vectors, evidence manifest,
public-corpus analysis, 121-fork SQL audit, 24-run local analysis, public bridge
fixture verifier, and local release-bundle verifier all pass with unchanged
reported counts. Tectonic emits no warnings, all fonts are embedded, and all 27
PDF pages passed visual inspection. The resulting PDF SHA-256 is
`4b6ac0f2e2e40f2dd1c5c3dcb4093de259c568652f40f0e19f88346888c75e8b`.
The independent reviewer then checked the disposition delta at exact commit
`59796ee00f0e4d2a71b9961208f61f818216282d` and found all five scoped technical
findings closed with no residual technical blocker; that closure report is
preserved as `codex-closure.md`.

## Owner disposition addendum — 2026-08-28

The owner chose the cut option for the unpublished 24-run migration-lab study.
It has been removed from the submission manuscript, current evidence ledger,
findings claims, outline, and release-facing README. The raw review reports and
their historical disposition text remain unchanged. The local source and
redacted bundle were not deleted; they are simply outside the submitted claim
set.

## Submission-freeze QA addendum — 2026-08-28

The companion bridge hardening was squash-merged to public `main` at
`843824a44d48d816779fc0c08580ae06108fe7b6`, passed its post-merge Python
3.11--3.13 matrix, and was frozen as the public `v0.2.0` source release. The
manuscript and evidence ledger pin that exact revision.

After the owner-directed cut, the release candidate passed the complete local
gate: the F# solution built with zero warnings and errors; 50/50 tests and all
language-neutral vectors passed; the sealed capsule verifier passed 4,919
archived runs; all 5,540 public logs and 317,296 cuts reproduced the reported
tables; and the 121-fork audit again found zero mismatches across 16,625 retained
events. The public bridge receipt verifier also passed. Tectonic emitted no
warnings, every font is embedded, and all 26 pages passed visual inspection.
The final PDF SHA-256 is
`551fbf3932d7e50becfcf4672ad0377fb0367b664119ccfae97566ea7dfb36e7`.
