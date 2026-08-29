# Findings log

This file is written alongside the artifact. Failed properties are retained; they are
not converted into passing tests by narrowing generators after the fact.

Status labels:

- observed — witnessed by an executable fixture
- checked — exercised by FsCheck over the stated generator family
- pending — requires a stronger generator, production behavior inventory, or
  an owner-supplied artifact

## A0 — Closed-log projection determinism

Status: checked.

For a fixed ordered event log, project returns the same state on repeated
evaluation. Duplicate event identities are idempotent at the reducer boundary.

Side conditions:

- the reducer is pure;
- event identity and ordering are fixed;
- nondeterministic oracle results enter as recorded events.

This is the trivial base case, not the paper’s primary claim.

## A1 — Settlement terminates for every behavior set

Status: falsified, observed.

Counterexample: a behavior triggered by signal “loop” emits the same signal.
The scheduler never reaches quiescence. The kernel reports StepBoundReached
rather than Quiescent. It intentionally does not infer a proved cycle from a
finite configuration fingerprint.

Restoring condition: pending. Candidate contracts include a well-founded
ranking over emitted events or an acyclic trigger relation. Neither is claimed
minimal yet.

Fixture: tests/fixtures/counterexamples.json, key cycle.

## A2 — Quiescent state is independent of firing order

Status: falsified, observed.

Counterexample: alpha and beta are enabled by the same event and both write the
fact “winner.” Canonical activation order yields winner = 2; reversed activation
order yields winner = 1.

Restoring condition checked so far: isolated, pairwise-disjoint writers converge
for the restricted generated family under all four schedulers. Declared writes
alone are not a sufficient general condition. A second preserved counterexample
has three behaviors with no declared write conflict: two emit distinct signals,
and an observer triggered by the first reads whether the second has arrived.
Reversing event order changes the observed value from 0 to 1.

The general restoring condition must constrain reads, writes, emitted triggers,
and activation eligibility. A minimal condition has not been established.

Scheduler-semantics clarification: in the executable F# kernel, activations
enabled by one triggering event read the same projected state, and emissions
become visible only after a later event-processing step. A dedicated regression
locks this kernel boundary; the read/trigger counterexample uses distinct later
events and is unchanged. ActiveGraph's pinned production source is different in
one relevant respect: it computes the match set once, but rebuilds each
handler's view against the current graph after prior handlers may have emitted
and synchronously projected events. The kernel result must therefore not be
reported as evidence that production same-trigger reads are snapshot-isolated.

Fixture: tests/fixtures/counterexamples.json, keys writeConflict and
readTriggerInterference.

## B1 — Identity fork

Status: checked.

Forking at the full log length preserves the exact retained trace.

## B2 — Nested fork collapse

Status: checked.

For j less than or equal to i, forking at i and then j retains the same exact
prefix as forking directly at j.

## B3 — Fork commutes with retained-prefix projection

Status: checked.

Projecting the fork prefix is equal to projecting the source prefix at the same
cut. This is a trace statement and does not imply that external effects were
undone.

## B4 — Discarded suffix irrelevance

Status: split by property.

- For retained-prefix projection: checked and sound.
- For a counterfactual-world claim: falsified.

Counterexample: a one-shot effect is committed in the discarded suffix. The
forked trace omits it, but the external action still happened. The assessment
reports discarded-one-shot-still-happened and is Unsound for
CounterfactualWorld.

This split replaces the over-broad statement that discarded effects are always
irrelevant to fork soundness.

Fixture: tests/fixtures/counterexamples.json, key discardedOneShot.

## B5 — Cut through an unresolved request

Status: observed.

A strict replay or continuation fork that retains a request without a terminal
outcome is Unsound. The external world may already have changed even when the
log has not recorded an outcome.

## B6 — Retained committed one-shot

Status: observed.

A retained committed one-shot is Conditional for external continuation. The
branch inherits the already-mutated world and must serve the recorded result
without re-executing the action.

## B7 — Retained committed recorded effect with unknown footprint

Status: checked; revised after external review.

The earlier fail-closed rule marked this combination Unsound for
ExternalContinuation. That conflated inability to make a world-state claim with
ability to serve an already recorded result. The revised verdict is Conditional
with a typed `ResolveUnknownInheritedFootprint` obligation. It remains Unsound
for a discarded-suffix CounterfactualWorld claim until the footprint is known.

An Idempotent plus Uncaptured result remains Unsound for ExternalContinuation:
idempotent world mutation does not guarantee reproduction of returned bytes.

## B8 — Failed effects may have partially committed

Status: checked by named regressions.

A Failed lifecycle is not evidence that the external world was unchanged.
Discarded failed Idempotent or Compensatable effects are Conditional on
reconciliation; discarded failed OneShot or Unknown effects are Unsound. A
failed Pure effect adds no world finding. Successful compensation also does not
erase temporal exposure or third-party secondary effects.

## G1 — Replay grades form a lattice

Status: checked.

The current five grades are a total chain and therefore a lattice. License sets
are monotone with grade rank.

## G2 — Grade is monotone under log extension

Status: falsified, observed.

A prefix with envelope, completion, mediated boundary, and clean
reconstruction grades as Boundary. Appending a hazard downgrades the whole run
to Envelope.

Interpretation: a grade certifies the evidence available for a particular log
or prefix. It is not an achievement badge that can only increase.

## V1 — Replay grades over deployed-runtime traces

Status: observed.

The public Synthetic Players capsule contains 5,540 run logs and 311,756 events.
All 5,540 grade Observed from the run log alone even though the capsule-level
verifier independently passes byte-exact checks. The verification attestation
is outside each run log, so a log-alone grading function must not infer a higher
grade.

The 5,540 store runs reconcile as 4,919 verifier-covered runs, 586 other
completed runs outside that verifier contract, and 35 incomplete runs. The
36,251 continuation-unsound cuts likewise reconcile exactly: 36,227 linked
request/response pairs contribute one open-boundary cut each, while 24 failed
runs end at an unmatched request and contribute one cut each.

The other two headline counts also have direct per-run identities. For a
request-bearing run with `n` events, first-request ordinal `q`, and `m`
requests, the ExternalContinuation Conditional count is `n + 1 - q - m`;
summing yields 120,969. For CounterfactualWorld, closed-response runs contribute
the ordinal of their final response (141,545 cuts in aggregate), while the 24
open runs contribute all of their `n + 1` cuts (1,349), yielding 142,894. The
4,923 affected runs are the union of 4,919 runs with a response and four open
runs with no prior response; the other 20 open runs are already in the first
set.

All 160,076 Sound ExternalContinuation cuts occur before the first classified
external request. They validate only the no-inherited-effect case. No public
trace in the Synthetic Players corpus validates post-oracle continuation, a
target-environment attestation, or a zero-reexecution receipt. The separate
public bridge conformance fixture described under V2 now exercises that case.

Finding: replay grade is a property of retained evidence, not a property of an
external claim about the artifact.

## V2 — Actual forks in the public Synthetic Players capsule

Status: observed.

The store contains 121 forks from 41 distinct parents. Across 16,625 retained
parent-prefix events there are zero structural mismatches after excluding child
run identity and `events.seq`, the store's database-wide autoincrement key. The
normalized per-run sequence is derived after export and is not a second
physical child field. Every observed cut is at
round.played, and every retained prefix contains zero classified external
requests. The actual forks therefore satisfy the current external-continuation
precondition, but only for this domain-only cut family.

A separate public activegraph-bridge fixture at revision
`843824a44d48d816779fc0c08580ae06108fe7b6` records an actual child fork after
one committed `one_shot + recorded` fixture-oracle effect. The generator
directly observes one source call and no second fixture-oracle call in the
child. Its hash-bound receipt records that inherited request `evt_008` was
served from outcome `evt_009`; the verifier checks receipt/log consistency and
signature-checks a caller assertion bound to the child, cut, prefix hash, and
target fingerprint under a public conformance trust root. It does not validate
the assertion's environmental contents. This closes the public mechanism-test
gap, but does not establish real-provider behavior or a production identity or
attestation system. The implementation is pinned on the public v0.2.0 release
and default branch.

The all-cuts counterfactual is much stricter: 4,923 of 5,540 runs contain at
least one cut assessed Unsound because an oracle call in the discarded suffix
already occurred.

## V3 — ActiveGraph production order

Status: source-audited; behavior-set conformance pending.

ActiveGraph v1.10.0 uses a FIFO queue, behavior registration order, and
sequential dispatch. This supplies one reproducible ProductionOrder but does
not establish schedule independence. Relation enumeration also lacks a
documented canonical tie-break. The executable tests therefore bypass the
production order and permute event and activation order.

No complete inventory of deployed behavior read/write/trigger dependencies was
available, so production satisfaction of a restoring non-interference condition
is not claimed.

Exact revisions, hashes, commands, counts, and limitations are in EVIDENCE.md.

## V4 — Review and release provenance

Status: review archives preserved; unpublished study excluded by owner decision.

Three supplied model reviews and the later ExploreScience review are preserved
under `reviews/2026-08-28`, with their consensus and dispositions recorded
separately from the raw reports. On 2026-08-28, the owner chose to exclude the
unpublished local executive study from the submission. It is not used as
evidence or cited by the manuscript; the historical review archive remains
unchanged.

ExploreScience reported 96/100 with 14 minor and zero major issues. A fresh
independent Codex report and closure review are also preserved. A production
behavior-dependency inventory and an external runtime corpus remain absent. A
public effect-boundary conformance fork is preserved at the bridge revision
above. These are stated evidence limits rather than accepted claims.

A second Explore Science review on 2026-08-29 scored the frozen manuscript
99/100 and reported 11 minor, zero major issues. Ten detailed issues appear in
the supplied PDF; the eleventh, a tonal-register comment, was available only in
the authenticated online review and is preserved separately. All 11 were
incorporated or explicitly scoped in the subsequent review branch. The
reviewer's suggested per-response explanation for the 120,969 Conditional cuts
was not copied verbatim because tail positions cannot be assigned uniquely to
individual responses; the exact per-run identity above replaces it.
