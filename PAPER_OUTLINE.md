# Confluence and Fork-Safety in Event-Sourced Agent Runtimes

Working arXiv outline, cs.AI / cs.MA. The findings ledger, not this outline, is
the claim authority.

## Claim discipline

The paper should say:

> This paper formalizes and empirically checks the replay and fork properties
> claimed on architectural grounds in Nakajima (2026).

It should not claim mechanized proof, a new reducer primitive, an irreducible
agent model, a minimal confluence condition, capability security, information
flow, or performance results. Property-based tests are executable
falsification and bounded evidence.

## 1. Introduction

- ActiveGraph claims deterministic replay, cheap forking, and lineage by
  construction.
- “Deterministic replay modulo the oracle” is underspecified: it matters where
  oracle results live and which external actions have already occurred.
- Business event sourcing can reconstruct state but cannot un-send, un-charge,
  or un-publish.
- Contributions:
  1. explicit schedule semantics and counterexamples to termination and
     confluence;
  2. property-relative fork-safety conditions over effect footprint, replay
     source, and lifecycle;
  3. a fail-closed replay-grade computation from retained evidence;
  4. validation over 5,564 run logs plus 121 observed forks.

## 2. Executable runtime model

- Ordered facts, pure projection, behavior trigger/fire boundary.
- Effects remain data and are executed only by an external interpreter.
- One-step activation relation, scheduler policy, quiescence, bounded
  divergence, and property-indexed projection equivalence.
- Four explicit scheduler policies: canonical, event-reverse,
  activation-reverse, and both reversed.
- ActiveGraph ProductionOrder is FIFO plus registration order: one schedule,
  not schedule independence.

## 3. Confluence and quiescence

### Proposition A1: termination

General termination is false. Preserve the self-triggering signal
counterexample. Report StepBoundReached, not a guessed cycle proof.

Candidate future conditions: a well-founded ranking over internal emissions or
an acyclic trigger relation.

### Proposition A2: unique quiescent projection

General confluence is false. Present two minimized counterexamples:

1. overlapping last-writers produce winner 2 versus winner 1;
2. declared writes are disjoint, but an observer reads whether another emitted
   signal has arrived, producing 0 versus 1.

Positive bounded result: isolated single-trigger, single-write behaviors with
pairwise-disjoint keys converge across the four schedulers for the generated
family. Do not elevate that restricted result into a general theorem.

Open problem: state a general non-interference contract over reads, writes,
emitted triggers, and activation eligibility.

## 4. Fork safety

Define three orthogonal effect dimensions:

- footprint: pure, idempotent, compensatable, one-shot, unknown;
- replay source: deterministic, recorded, uncaptured;
- lifecycle: requested, committed, failed, unknown.

State fork soundness relative to a property, not globally:

- retained-prefix projection;
- strict execution replay;
- external continuation;
- counterfactual external world.

Laws and findings:

- identity fork and nested-prefix collapse hold for trace prefixes;
- projection commutes with retaining the same prefix;
- a cut through an unresolved request is unsound for strict replay or
  continuation;
- a retained committed one-shot is conditional on zero re-execution;
- a one-shot in the discarded suffix invalidates the counterfactual-world
  claim even though trace projection remains exact.

## 5. Replayability grades

- Model each grade as a set of licensed questions; the current five grades form
  a chain by license-set inclusion.
- Native requires all lower evidence prerequisites; profile labels cannot grant
  a grade.
- Grade is not monotone under log extension: a later hazard can downgrade it.
- The grade of a shared prefix makes the fork precondition observable.
- External capsule verification cannot raise a log-alone grade unless the
  attestation is retained in the log.

## 6. Method

- F# executable specification; pure reducer boundary with ordinary .NET outside
  it.
- FsCheck laws over generated logs, behaviors, and schedules.
- Named minimized failures retained as regression fixtures.
- Every-cut enumeration for real logs.
- JSON conformance vectors and schema for independent implementations.
- Threats: bounded generators, adapter classification, projection equivalence,
  and missing production behavior inventory.

## 7. Validation

### Synthetic Players public capsule

| Measure | Result |
|---|---:|
| Runs / events | 5,540 / 311,756 |
| Log-alone grade | Observed: 5,540 |
| Projection cuts | 317,296 sound; 0 unsound |
| External-continuation cuts | 160,076 sound; 120,969 conditional; 36,251 unsound |
| Counterfactual cuts | 150,230 sound; 24,172 conditional; 142,894 unsound |
| Observed forks | 121 |
| Structural prefix comparison | 16,625 events; 0 mismatches modulo run ID and global sequence |

The observed forks all cut at `round.played` and retain no classified external
request. They validate cheap trace branching for that cut family, not arbitrary
oracle/effect boundaries.

### activegraph-bridge offline study

| Measure | Result |
|---|---:|
| Runs / source events | 24 / 2,592 |
| Log-alone grade | Boundary: 24 |
| Decision / ordered-path agreement | 66.6667% / 0% |
| Projection cuts | 2,664 sound; 0 unsound |
| Counterfactual cuts | 0 sound; 288 conditional; 2,376 unsound |

Use the 66.7% decision agreement with 0% path agreement to motivate distinct
equivalence relations. State prominently that this is a deterministic offline
mock instrumentation study and its containing source directory is not yet
committed or externally reproducible.

## 8. Related work

Cover every item in `docs/RELATED_WORK.md`: Decider, Elm, Mealy machines,
lambda_A, LLMbda, MCP process calculus, Agent libOS, Effect-Transparent
Governance, AgentSpec, ActiveGraph, and the prior Agent Algebra name collision.

## 9. Discussion

- “Cheap fork” is semantic before it is a performance claim.
- Exact replay, projected-state equivalence, decision equivalence, and an
  unchanged external world are different assertions.
- Deterministic production ordering can mask non-confluence rather than solve
  it.
- A log can be externally verified yet self-license only Observed replay.
- Runtime contracts should record canonical ordering, effect footprint,
  request/outcome linkage, receipt provenance, and verification attestations.

## 10. Limitations and future work

- No mechanized proof or minimal side condition.
- No real-model inference in the bridge study.
- No complete production read/write/trigger inventory.
- No actual fork in the public capsule crosses an external-effect boundary.
- Bridge source provenance must be committed before paper submission.
- Future work: mechanize selected laws after the empirical contracts stabilize,
  add production behavior dependency extraction, and validate a fork containing
  a recorded oracle boundary without re-execution.
