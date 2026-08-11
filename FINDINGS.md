# Findings log

This file is written alongside the artifact. Failed laws are retained; they are
not converted into passing tests by narrowing generators after the fact.

Status labels:

- observed — witnessed by an executable fixture
- checked — exercised by FsCheck over the stated generator family
- pending — requires real-log validation or a stronger generator

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
The scheduler never reaches quiescence. The kernel reports CycleDetected rather
than Quiescent. A separate step-bound result remains available for cycles that
do not repeat a finite fingerprint.

Restoring condition: pending. Candidate contracts include a well-founded
ranking over emitted events or an acyclic trigger relation. Neither is claimed
minimal yet.

Fixture: tests/fixtures/counterexamples.json, key cycle.

## A2 — Quiescent state is independent of firing order

Status: falsified, observed.

Counterexample: alpha and beta are enabled by the same event and both write the
fact “winner.” Canonical activation order yields winner = 2; reversed activation
order yields winner = 1.

Restoring condition checked so far: pairwise-disjoint declared writes for a
restricted generated family of single-trigger, single-write behaviors. This
condition is sufficient for that family only. Behaviors that read another
behavior’s region, emit cross-triggering events, or under-declare writes require
a stronger interference contract.

Fixture: tests/fixtures/counterexamples.json, key writeConflict.

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

## Production validation

Status: pending.

No production distribution or production conformance result is claimed in this
file until EVIDENCE.md records a source path or immutable artifact identity,
profile, count, hash where publishable, command, and result.
