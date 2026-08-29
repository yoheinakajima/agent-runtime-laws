# Replay Is a Family of Assertions: Fork-Safety in Event-Sourced Agent Runtimes

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
- Cross-map Byte, Trace, Projection, Path, Decision, Execution, and Environment
  to the four fork properties, three equivalences, replay grades, and explicit
  non-operationalized scope boundaries.
- Contributions:
  1. explicit schedule semantics and counterexamples to termination and
     confluence;
  2. property-relative fork-safety conditions over effect footprint, replay
     source, and lifecycle;
  3. a fail-closed replay-grade computation from retained evidence;
  4. exhaustive cut assessment over 5,540 public store runs plus 121 observed
     forks; and
  5. a public post-oracle conformance fork whose generator observes no second
     fixture-oracle call and whose verifier checks a hash-bound receipt plus a
     fork-bound caller assertion.

## 2. Executable runtime model

- Ordered facts, pure projection, behavior trigger/fire boundary.
- Effects remain data and are executed only by an external interpreter.
- One-step activation relation, scheduler policy, quiescence, bounded
  divergence, and property-indexed projection equivalence.
- Four explicit scheduler policies: canonical, event-reverse,
  activation-reverse, and both reversed.
- ActiveGraph ProductionOrder is FIFO plus registration order: one schedule,
  not schedule independence.
- State-read timing differs across the executable and production semantics:
  the kernel snapshots state for one trigger's activations, while ActiveGraph
  fixes the match set once but rebuilds each handler view after prior
  synchronous projections. State this as a source-audited generalization
  boundary.

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

Checked identities and findings:

- identity fork and nested-prefix collapse hold for trace prefixes;
- projection commutes with retaining the same prefix;
- a cut through an unresolved request is unsound for strict replay or
  continuation;
- a retained committed one-shot is conditional on zero re-execution;
- a retained committed recorded effect with unknown footprint is conditional,
  while a discarded unknown footprint still blocks an in-scope world claim;
- idempotent footprint does not reconstruct an uncaptured result;
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
- FsCheck properties over generated logs, behaviors, and schedules.
- Named minimized failures retained as regression fixtures.
- Every-normalized-position enumeration plus source-event-boundary reporting
  when one source event expands to multiple normalized facts.
- JSON conformance vectors and schema for independent implementations.
- Report 15 live FsCheck properties and 35 facts, per-property MaxTest 100--250,
  3,300 top-level generated trials, and the absence of a fixed global seed.
- Treat the seven language-neutral vectors as a mapped portability baseline,
  not an exhaustive verdict/obligation cross-product.
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
| Structural prefix comparison | 16,625 events; 0 mismatches modulo run ID and database-global `events.seq` |

The observed forks all cut at `round.played` and retain no classified external
request. They validate cheap trace branching for that cut family, not arbitrary
oracle/effect boundaries.

Arithmetic identities: continuation Conditional is the sum of
`n + 1 - first_request_ordinal - request_count` over request-bearing runs.
Counterfactual Unsound is 141,545 final-response-ordinal cuts over closed runs
plus all 1,349 cuts in 24 open runs, totaling 142,894.

### activegraph-bridge public post-oracle conformance fork

- Pin the fixture to bridge revision
  `843824a44d48d816779fc0c08580ae06108fe7b6`.
- Report the parent, child, receipt, environment-attestation, manifest, prefix,
  and canonical-receipt hashes from `EVIDENCE.md`.
- The child inherits a committed recorded offline oracle outcome; the generator
  observes that it is served from the record with no second fixture-oracle call.
- The verifier checks receipt/log consistency and an HMAC-SHA256 caller
  assertion bound to the child, cut, and retained prefix under a public fixture
  trust root. It does not inspect the asserted environment contents.
- Bound the claim: this validates the mechanism for a deterministic offline
  oracle. The published fixture key is not a production credential and does
  not establish provider identity, model quality, or production-environment
  fidelity.

## 8. Related work

Cover every item in `docs/RELATED_WORK.md`: Decider, Elm, Mealy machines, event
sourcing, active-database ECA rules, Newman, CRDTs, sagas, durable workflow
engines, LangGraph, lambda_A, LLMbda, MCP process calculus, Agent libOS,
Effect-Transparent Governance, AgentSpec, ActiveGraph, and the prior Agent
Algebra name collision.

## 9. Discussion

- “Cheap fork” is semantic before it is a performance claim.
- Exact replay, projected-state equivalence, decision equivalence, and an
  unchanged external world are different assertions.
- Deterministic production ordering can mask non-confluence rather than solve
  it.
- A log can be externally verified yet self-license only Observed replay.
- The language-neutral F# assessor remains verifier-agnostic; the companion
  bridge implements one bounded attestation-discharge mechanism and emits a
  hash-bound, zero-reexecution receipt.
- Runtime contracts should record canonical ordering, effect footprint,
  request/outcome linkage, receipt provenance, and verification attestations.

## 10. Limitations and future work

- No mechanized proof or minimal side condition.
- No real-model inference in the public bridge fixture.
- No complete production read/write/trigger inventory.
- The kernel does not model ActiveGraph's per-invocation same-trigger view
  refresh; no production behavior inventory establishes its observed impact.
- The 121 observed production forks do not cross an external-effect boundary;
  the public post-oracle fork is a deterministic offline conformance case.
- The conformance trust root is deliberately published and therefore does not
  authenticate a real provider, production attestor, or production
  environment.
- Every verdict is snapshot-relative; delayed hazards require a bound source
  high-water mark, an explicit closure/quiescence/freshness policy, and a final
  pre-continuation recheck.
- Seven language-neutral vectors are baseline coverage; four typed Conditional
  obligations still lack dedicated portable vectors.
- Future work: mechanize selected properties after the empirical contracts stabilize,
  add production behavior dependency extraction, and validate post-oracle forks
  against production provider and attestor identities.
