# Evidence ledger

The validation harness is read-only. It normalizes JSONL into the kernel event
model, computes replay grade, evaluates every fork cut, and reports unclassified
source event types.

## Checked-in fixtures

The following files are regression inputs for the harness. They were authored
for this repository from the expected source shapes and contain no production
payloads.

| Fixture | Profile | Intended grade | Provenance |
|---|---|---:|---|
| evidence/fixtures/bridge-sanitized.jsonl | bridge | boundary | synthetic, sanitized, shape-conformant |
| evidence/fixtures/activegraph-sanitized.jsonl | activegraph | native | synthetic, sanitized, shape-conformant |

evidence/manifest.json binds each fixture by SHA-256 and rejects any entry not
marked public or sanitized.

These fixtures establish harness behavior only. They are not evidence for a
claim about production ActiveGraph runs.

## Required real-log record

Every empirical run added below must record:

- source repository and immutable revision or capsule identifier;
- exact path, without copying private content into this repository;
- public/sanitized/private classification;
- parser profile;
- source and normalized event counts;
- replay-grade distribution;
- fork-cut verdict counts;
- unclassified event types;
- command and tool version;
- whether the result is reproducible by an external reviewer.

## ActiveGraph

Status: pending source audit.

Target checks:

- whether runtime scheduling imposes a deterministic order;
- whether the derived non-interference condition holds in real behavior sets;
- whether effect request/outcome boundaries are complete;
- grade distribution and unsafe cuts.

## synthetic-players

Status: pending source audit.

Research brief inventory: 4,916 confirmatory runs and 30,397 response IDs, with
a public verification capsule. Those numbers remain intake claims until the
artifact path, revision, and verification output are recorded here.

## activegraph-bridge synthetic-executive-demo-v1

Status: pending source audit.

Research brief inventory: 12 cases by two strategies, 24 boundary-verified
runs, 66.7 percent decision agreement, and zero exact-path agreement. Those
numbers remain intake claims until independently recovered from source truth.

This study is expected to motivate the difference between:

- exact trace replay;
- equivalent projected state;
- equivalent decision outcome;
- a counterfactual external world.

## Reproduction commands

Validate one source:

~~~bash
dotnet run --project apps/AgentRuntimeLaws.Cli -- \
  validate activegraph path/to/log.jsonl
~~~

Validate a hash-bound manifest:

~~~bash
dotnet run --project apps/AgentRuntimeLaws.Cli -- \
  manifest evidence/manifest.json
~~~
