# ExploreScience review synthesis and disposition

Status: incorporated on branch `review/explore-science-round1-2026-08-28`.
The full local gate passes 50/50 tests, both corpora were revalidated, and the
25-page PDF passed full-page visual QA. Owner review remains required. This
review does not authorize merge or publication.

## Summary

ExploreScience rated the draft 96/100 (95% confidence interval 93–97), Platinum
tier, with 14 minor and zero major issues. The review is positive about the
paper's central move: separating replay into property-indexed claims and
classifying effect footprint, replay source, and lifecycle independently.

The most valuable feedback was not stylistic. It identified:

1. one over-restrictive continuation verdict for recorded committed effects
   with unknown footprint;
2. one empirical denominator that counted cuts between facts co-derived from a
   single atomic source event; and
3. one manuscript/artifact mismatch: the paper described an attestation
   discharge pathway that the v0 assessor does not implement.

The revision accepts those findings, preserves the strict zero-call meaning of
ExternalContinuation, and reports remaining gaps instead of manufacturing
evidence.

## Issue dispositions

| Issue | Disposition | Result |
|---|---|---|
| A1 — Abstract omits 120,969 Conditional cuts | Accepted | The abstract now reports both 36,251 Unsound cuts and 120,969 Conditional cuts, including the no-reexecution obligation. |
| B1 — Unknown + Recorded + Committed over-blocks continuation | Accepted | ExternalContinuation is now Conditional, with typed `ResolveUnknownInheritedFootprint(effectId)`. CounterfactualWorld still fails closed when an unknown discarded footprint is in scope. The language-neutral vector and generated verdict property were updated. |
| B2 — CounterfactualWorld blockers are not scoped to Omega | Accepted in definition; bounded in v0 | The definition now applies only to effects whose observables intersect Omega. Because v0 has no per-effect observable tags, the executable result instantiates `Omega_profile` as the adapter's full recognized external surface. Narrower claims remain future work and do not alter the reported counts. |
| B3 — Permit live reexecution of Idempotent + Uncaptured | Not adopted | Idempotence constrains world mutation, not returned bytes. A repeated PUT or model/tool call can return a new timestamp, receipt, identifier, or output. ExternalContinuation includes StrictExecutionReplay, so an uncaptured result remains Unsound. A different live-resumption property could permit reexecution under a provider-specific outcome-equivalence contract. |
| B4 — Failed external call may partially mutate | Accepted as clarification and regression | The implementation already conditioned failed Idempotent/Compensatable effects and blocked failed OneShot/Unknown effects; it did not treat all Failed outcomes as safe. Two named tests now preserve this behavior and the paper states the partial-commit case explicitly. |
| B5 — Compensation does not erase temporal exposure | Accepted | CounterfactualWorld is now explicitly relative to an observable set and reconciliation contract that accounts for temporal windows and secondary effects. The limitation is repeated in Threats to Validity. |
| B6 — FIRE-ACTIVATION contradicts same-trigger prose | Accepted | The prose and figure caption now match the small-step rule: activations for one trigger read the same state; emissions become visible after a later PROCESS-EVENT step. A regression test locks this boundary. Counterexample 3 is unchanged because it uses distinct later events. |
| B7 — Null divergence undefined | Accepted | The paper now defines it as a present `divergence` payload field whose JSON value is null, asserting that the bridge verifier found no unresolved reconstruction divergence. It is evidence, not proof. |
| B8 — Hazard event undefined | Accepted | The paper now names the existing closed-union representation `EvidenceRecorded(HazardDetected detail)` and links it to the grade blocker. No new event kind was invented. |
| B9 — Four-scheduler positive result lacks rationale | Accepted | The paper now explains that the restricted writers commute structurally because each key has one unconditional writer and there are no reads or cross-triggers. The four schedules are regression coverage, not exhaustive interleaving enumeration. |
| C1 — Bridge cuts include intra-source normalization positions | Accepted and remeasured | Validation now retains source-event boundaries. The primary bridge denominator is 2,616 source-boundary cuts; 48 intra-source positions remain diagnostic only. Primary source-boundary verdicts are Projection 2,616/0/0, ExternalContinuation 1,968/264/384, and CounterfactualWorld 0/264/2,352 for Sound/Conditional/Unsound. |
| C2 — Sound public cuts do not exercise attestation | Accepted; claim narrowed further | Independent SQL confirms all 160,076 Sound ExternalContinuation cuts occur before the first classified request; 617 runs have no classified request. The revision states that no public result validates a post-oracle continuation or zero-reexecution receipt. It also corrects the paper: target-environment attestation is not implemented in v0, so there is no discharge pathway to claim as tested. |
| C3 — Treatment of sequence in fork audit unclear | Accepted | The source schema was checked directly. `events.seq` is the database-wide autoincrement primary key, not a run-local physical envelope field. It orders traces and is excluded from equality; normalized run-local sequence is derived after export. |
| D1 — Conditional obligation payload untyped | Accepted in code and paper | `ForkObligation` is now a closed F# union with five cases, and each Conditional finding carries a typed obligation. The proposed API contract prints the same union and distinguishes it from the not-yet-implemented production receipt endpoint. |

## Synthesis

The review strengthens the paper's central distinction. ExternalContinuation
asks whether a recorded prefix can be reconstructed and safely continued under
a supplied environment premise; CounterfactualWorld asks whether omitted
effects are observationally absent. Unknown historical footprint can condition
the former without licensing the latter. Conversely, an idempotent footprint
cannot repair missing replay material.

The empirical revision is also material. One-to-many normalization is valid for
evidence projection, but a fork cannot occur halfway through one atomic source
event. Reporting source boundaries separately prevents the adapter from
creating artificial experimental units while preserving full normalized-cut
checks as a transformation diagnostic.

The deepest remaining gap is attestation. The prior manuscript wording implied
that a snapshot or fresh-environment receipt could be checked by the executable
assessor. It cannot. The current implementation assumes the target environment
premise and computes effect-related findings and obligations. A future receipt
API must authenticate that premise before any production claim should call it
discharged.

## Remaining evidence gates

- implement and test an authenticated target-environment receipt if the paper
  is to claim an attestation discharge pathway;
- add per-effect observable tags before reporting a narrow
  CounterfactualWorld(Omega) result;
- obtain a public fork after a recorded committed oracle call with a verified
  zero-reexecution receipt;
- publish or deliberately exclude the locally commit-bound bridge bundle;
- validate an independent non-ActiveGraph runtime corpus;
- preserve a fresh independent Codex review if the established four-model
  review protocol is still required.

## Post-review hardening addendum — 2026-08-28

The first and third implementation gaps above are now discharged for a bounded
public conformance case by activegraph-bridge revision
`8855d3a9e779362f713b08bceb58d7d5db671c7d` (PR #2). The bridge emits a
hash-bound fork receipt after a committed recorded offline oracle call. Its
generator observes no second fixture-oracle call; its verifier checks
receipt/log consistency and a fork-bound caller assertion with HMAC-SHA256 under
a configured public fixture trust root. Its tests reject tampered receipts,
assertions, logs, and assertions bound to another fork. The verifier does not
inspect the asserted environment contents.

The paper now reports this as mechanism-level conformance evidence and retains
the correct limit: the deliberately published fixture key does not authenticate
a real provider or production environment. The legacy corpus adapter still
lacks per-effect observable tags, the separate 24-run study is not public, and
the independent Codex review and its dispositions are preserved separately in
the parent review directory.

## Owner disposition addendum — 2026-08-28

The owner subsequently chose to exclude the unpublished 24-run study from the
submission. Its source was not deleted, and the historical review above remains
unaltered; the study is no longer part of the manuscript or current evidence
claim set.
