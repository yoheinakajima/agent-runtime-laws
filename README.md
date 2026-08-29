# Agent Runtime Laws

An executable specification for replay, confluence, and fork safety in
event-sourced agent runtimes.

This project asks a narrower question than “what is the smallest agent
runtime?” The reducer shape is already well known from Mealy machines, the Elm
architecture, and the event-sourcing Decider pattern. The open problem is:

> Under which observable conditions are deterministic replay, cheap forking,
> and end-to-end lineage sound claims?

The companion paper’s working title is “Replay Is a Family of Assertions:
Fork-Safety in Event-Sourced Agent Runtimes.” The artifact is written in F# as an executable
specification. FsCheck explores generated logs and schedules; named
counterexamples are retained as regression fixtures.

## Current result

The general settlement loop is neither terminating nor confluent without
additional conditions.

- A cyclic behavior set diverges and must not be reported as quiescent.
- Two behaviors that write the same state region can reach different
  quiescent states under different activation schedules.
- Isolated, pairwise-disjoint writers converge for the deliberately restricted
  generated family under all four schedulers.
- Declared writes alone are not a general restoring condition: a preserved
  counterexample has disjoint writes but schedule-sensitive reads and emitted
  triggers. A minimal non-interference condition remains open.

Fork safety is property-relative:

- Retained-prefix projection is exact by construction.
- Strict execution replay additionally requires captured effect results and a
  cut that does not split a request from its outcome.
- External continuation inherits committed one-shot effects and must not
  execute them again.
- A committed recorded result with unknown footprint is Conditional rather
  than automatically refused; it can be served without a live call, but cannot
  license a world-state claim until its footprint is resolved.
- A branch that discards an irreversible effect is not a counterfactual world
  in which that effect never happened.

That last distinction is the central seam: a discarded suffix is irrelevant to
trace replay, but not necessarily to the already-mutated external world.

## Kernel

The kernel separates four concerns:

1. Event projection: pure evolve and project functions.
2. Reactive settlement: behaviors plus an explicit, permutable scheduler.
3. Fork assessment: soundness relative to a named observation or world claim.
4. Replay grade: evidence in the log determines which questions it licenses.

Effects are not classified by one flat enum. Three orthogonal dimensions are
recorded:

| Dimension | Values | Question answered |
|---|---|---|
| External footprint | pure, idempotent, compensatable, one-shot, unknown | What happened to the world? |
| Replay source | deterministic, recorded, uncaptured | Can replay reproduce or serve the result? |
| Lifecycle | requested, committed, failed, unknown | Is the effect boundary complete? |

The kernel never performs an effect. Interpreters remain outside the artifact.

The transition and settlement boundary is intentionally separated from fork,
grade, validation, and conformance analyzers. Readability and explicit lifecycle
integrity take priority over a line-count claim.

## Replay grades

The five grades form an ordered chain:

observed → envelope → boundary → checkpointed → native

Each grade licenses a larger class of questions. The grade is computed from
log evidence and effect completeness; it is not a user-supplied label. Grades
are intentionally not monotone under log extension: a later hazard can
downgrade a previously clean prefix.

## Run it

Requires .NET SDK 8.0.423 or a compatible 8.0 patch.

~~~bash
dotnet restore AgentRuntimeLaws.sln
dotnet build AgentRuntimeLaws.sln --no-restore
dotnet test tests/AgentRuntimeLaws.Properties/AgentRuntimeLaws.Properties.fsproj \
  --no-build --no-restore

dotnet run --project apps/AgentRuntimeLaws.Cli -- demo
dotnet run --project apps/AgentRuntimeLaws.Cli -- \
  conformance conformance/vectors/v1.json
dotnet run --project apps/AgentRuntimeLaws.Cli -- \
  manifest evidence/manifest.json
~~~

Or run the complete local gate:

~~~bash
./scripts/verify.sh
~~~

## Layout

- src/AgentRuntimeLaws — the readable kernel and validation adapters
- tests/AgentRuntimeLaws.Properties — FsCheck properties and named regressions
- tests/fixtures — preserved counterexamples
- conformance/vectors — language-neutral JSON vectors
- conformance/schema — machine-readable JSON Schema for those vectors
- evidence — hash-bound sanitized harness fixtures and provenance notes
- apps/AgentRuntimeLaws.Cli — demo, conformance, and validation commands
- FINDINGS.md — the living record of failed properties and restoring conditions
- docs/RELATED_WORK.md — explicit novelty boundary
- reviews/2026-08-28 — preserved reviewer reports and disposition ledger

## Evidence status

The public Synthetic Players capsule was verified and all 5,540 store runs were
checked at every cut. Its public verifier covers 4,919 of those runs; the exact
reconciliation is 4,919 verifier-covered, 586 other completed runs, and 35
incomplete runs. The 121 observed forks have zero structural mismatches across
16,625 retained-prefix events after excluding child run identity and the
store's database-global `events.seq`. The public activegraph-bridge v0.2.0
release records an actual fork after a committed recorded
fixture-oracle effect. Its generator observes one source call and no second
fixture-oracle call in the child; its verifier checks receipt/log consistency
and a fork-bound caller assertion under a public conformance key. No private
trace payloads were copied into this repository. See EVIDENCE.md for exact
revisions, hashes, counts, commands, and limitations.

## Scope

This artifact does not claim:

- a new irreducible reducer primitive;
- mechanized proof;
- type safety or termination of agent composition;
- information-flow noninterference;
- capability confinement;
- performance results.

The intended contribution is executable contracts, honest counterexamples, and
validation against deployed-runtime traces. A passing property suite is
evidence over its generators and fixtures, not a universal proof.

## Status

Research v0. Kernel contracts, 50 tests, conformance vectors, and deployed-trace
validation are implemented. Claude, Gemini, Grok, and a later independent Codex
review are archived with provenance and dispositions; the subsequent
ExploreScience review is preserved separately. The public post-oracle
conformance mechanism test is complete under the bounded claim stated above.
The owner excluded the unpublished local executive study from the submission
and authorized the public release and submission workflow on 2026-08-28.
