# Evidence ledger

The validation harness is read-only. It normalizes ordered JSONL logs into the
kernel event model, computes a replay grade, evaluates every cut from zero
through the normalized log length, and reports unclassified source event types.
External verification is not silently promoted into log evidence.

## Checked-in harness fixtures

These authored fixtures contain no production payloads. They test normalization
and fail-closed behavior only.

| Fixture | Profile | Purpose | Provenance |
|---|---|---|---|
| evidence/fixtures/bridge-sanitized.jsonl | bridge | boundary-grade adapter | synthetic, sanitized, shape-conformant |
| evidence/fixtures/activegraph-sanitized.jsonl | activegraph | effect lifecycle adapter | synthetic, sanitized, shape-conformant |
| tests/fixtures/activegraph-recorded.jsonl | activegraph | recorded-result regression | authored test fixture |
| tests/fixtures/fail-closed.jsonl | activegraph | unknown-footprint regression | authored test fixture |

`evidence/manifest.json` binds evidence fixtures by SHA-256 and refuses entries
without source receipts or an explicit public/sanitized declaration. The
language-neutral vectors have a JSON Schema at
`conformance/schema/v1.schema.json`.

## ActiveGraph scheduler audit

| Field | Value |
|---|---|
| Repository | https://github.com/yoheinakajima/activegraph |
| Revision | `8aedb1866cf5dce056af97529152ffd6f468a1ed` |
| Version | 1.10.0 |
| Evidence | `activegraph/runtime/queue.py`, `registry.py`, `runtime.py`, `CONTRACT.md` |

The production scheduler is FIFO, matches behaviors in registration order, and
dispatches sequentially. Delayed work at the same tick retains FIFO order.
This is a deterministic policy for one schedule, not evidence of confluence
under other legal schedules. Projection occurs before notification, so an
earlier registered behavior can change state observed by a later behavior
handling the same original event. Relation enumeration has no documented
canonical ordering. The kernel therefore treats ProductionOrder as one policy
and separately permutes event order and activation order.

The public repository has a deterministic nine-event golden log and an offline
quickstart fixture, but no committed production capsule or production SQLite
store at the pinned revision. The golden JSONL also omits canonical database
sequence, run identity, and fork lineage, so it is not used as fork evidence.

## Synthetic Players public capsule

### Provenance

| Field | Value |
|---|---|
| Repository | https://github.com/yoheinakajima/synthetic-players |
| Revision | `82556ef9dae3af59693f5d007b60095bf8a2dbe4` |
| Compressed store | `capsule/data/engine.db.xz` |
| Compressed store SHA-256 | `7b7fdfe64b5b6f1d0a6aea7c661b6e054be2a7e90005ba0d8f142b1f075f2822` |
| Capsule checksum ledger | `capsule/SHA256SUMS.capsule` |
| Checksum-ledger SHA-256 | `6d80f3b19b921d6c0b188123ad49e7f62324f59f495b2d72ea8c85f2652d641d` |
| Decompressed store SHA-256 | `0ba5756bddfb39f08dd11f69fb383b098327c7d6c5c1f78259009a5b449f816f` |
| Publication status | public and externally reproducible |

Tracked files in the source checkout matched the pinned revision. Three
untracked generated `* 2.json` driver files were present after verification;
they are outside the tracked capsule and were neither used nor removed.

Running `capsule/verify.sh` produced:

> CAPSULE VERIFICATION PASS — 4,919 archived Phase 3-5 runs verified
> (4,916 confirmatory + 3 legacy diagnostics)

The store contains 5,540 runs, 311,756 events, 36,251 `llm.requested` events,
36,227 `llm.responded` events, and 30,397 `decision.parsed` events. The capsule
verifier and the kernel answer different questions: the former verifies the
sealed artifact, while the latter grades evidence retained inside each run log.

### All-cut law results

Command sequence:

~~~bash
xz -dc capsule/data/engine.db.xz > /tmp/synthetic-players-engine.db
./scripts/export_activegraph_sqlite.sh \
  /tmp/synthetic-players-engine.db /tmp/synthetic-players-jsonl
dotnet run --project apps/AgentRuntimeLaws.Cli --no-build -- \
  validate-directory activegraph /tmp/synthetic-players-jsonl
~~~

| Measure | Result |
|---|---:|
| Runs | 5,540 |
| Source / normalized events | 311,756 / 311,756 |
| Grade distribution | Observed: 5,540 |
| Verified from log alone | 0 |
| Projection cuts | Sound 317,296; Conditional 0; Unsound 0 |
| External-continuation cuts | Sound 160,076; Conditional 120,969; Unsound 36,251 |
| Counterfactual-world cuts | Sound 150,230; Conditional 24,172; Unsound 142,894 |
| Runs with a counterfactual-unsound cut | 4,923 |

All runs grade Observed because capsule verification is not recorded as an
attestation inside each run log. The 36,251 external-continuation-unsound cuts
are exactly the cuts through outstanding LLM requests. Conditional cuts retain
an already committed oracle call and require serving the recorded result rather
than re-executing it. Counterfactual-world verdicts are stricter because a call
in the discarded suffix still occurred and may already have incurred cost.

Unclassified domain/runtime types are reported rather than guessed:
`behavior.started`, `behavior.completed`, `decision.parsed`, `infra.*`,
`object.created`, `patch.applied`, `round.played`, `round.requested`,
`run.completed`, and `runtime.idle`.

### Observed-fork audit

~~~bash
./scripts/audit_activegraph_forks.sh /tmp/synthetic-players-engine.db
~~~

| Measure | Result |
|---|---:|
| Forks | 121 |
| Distinct parents | 41 |
| Nested forks | 0 |
| Compared retained-prefix events | 16,625 |
| Prefix mismatches | 0 |
| Missing cut events | 0 |
| Cut event type | `round.played`: 121 |
| Retained classified external requests | 0 |
| Unresolved retained requests | 0 |

These observed forks preserve every compared retained-prefix field: event ID,
type, actor, payload, frame, cause, and timestamp. Child run identity and the
store's global sequence number are intentionally excluded. The forks satisfy
the current external-continuation precondition, but they do not validate cuts
through external effects because none of the 121 retained prefixes contains
one.

## activegraph-bridge synthetic-executive-demo-v1

### Provenance and limitation

The artifact was recovered from an unpublished local checkout at
`activegraph-model-migration-lab/artifacts/experiments/synthetic-executive-demo-v1`.

The containing repository has no commit and no configured remote; all source
files are untracked. The four hashes below bind this local result, but an
external reviewer cannot reproduce source provenance until the artifact is
committed or published.

| File | SHA-256 |
|---|---|
| `runs.db` | `7a38a1e8dce58bbc7a82ce019c7d05fde815f4bf81cfbd73f2d8eb0bc8d0730c` |
| `manifest.json` | `da1cbc3bd8ba6c9662e6daba10cdc79ec16a721923e9be929a746f7a46107af6` |
| `metrics.json` | `9f9242e1d064aa5f211cf8976c97c8594364ae5cb221081743fc08a877ee008c` |
| `pairs.json` | `f4d95dbb413936d59685b16133698617c6a7fed3e16a335ea26dd3bf4ca55110` |

The manifest records 12 synthetic cases, two deterministic offline mock
strategies, 24 runs, no real model, and verification enabled. Its stated claim
scope is instrumentation feasibility only. The paired metrics recover 66.6667%
decision agreement, 0% ordered path agreement, and 66.6667%
same-decision/different-path cases.

### All-cut law results

| Measure | Result |
|---|---:|
| Runs | 24 |
| Source / normalized events | 2,592 / 2,640 |
| Grade distribution | Boundary: 24 |
| Verified from log alone | 24 |
| Projection cuts | Sound 2,664; Conditional 0; Unsound 0 |
| External-continuation cuts | Sound 1,992; Conditional 288; Unsound 384 |
| Counterfactual-world cuts | Sound 0; Conditional 288; Unsound 2,376 |
| Runs with a counterfactual-unsound cut | 24 |

Boundary grade is derived only when a run log explicitly records fresh-factory
reconstruction and a successful verification event with an effects-served
count and null divergence. The normalized count is 48 events higher because
those 24 source events each establish two distinct evidence facts.

Every run contains a one-shot `submit_recommendation` effect. Consequently, no
cut supports an unconditional claim that the external world is as if the run
never happened: before the effect, it appears in the discarded suffix; after
the effect, continuation is conditional on not executing it again.

## Interpretation limits

- FsCheck and all-cut enumeration are executable falsification and finite
  evidence, not mechanized proof.
- Projection equality excludes run IDs, event IDs, sequence numbers, and
  lineage metadata; byte equality is reported separately where available.
- ExternalContinuation assumes the target environment represents the retained
  prefix and asks whether execution may proceed without re-running inherited
  effects. CounterfactualWorld is the stronger test for whether a retrospectively
  discarded suffix left the external world unchanged.
- Adapter evidence is granted only for exact recognized event types. Names that
  merely contain words such as `verification` remain unclassified.
- A recorded response proves replay material is present. It does not by itself
  prove an external write committed or was idempotent.
- Unknown effect footprint, malformed lifecycle, absent ordering authority, or
  missing receipt evidence fail closed.
- Production behavior-set conformance remains open because no complete
  read/write/trigger inventory was available.

## Reproduction commands

Validate one JSONL log:

~~~bash
dotnet run --project apps/AgentRuntimeLaws.Cli -- \
  validate activegraph path/to/log.jsonl
~~~

Validate every JSONL run in a directory:

~~~bash
dotnet run --project apps/AgentRuntimeLaws.Cli -- \
  validate-directory activegraph path/to/log-directory
~~~

Validate checked-in hash-bound fixtures:

~~~bash
dotnet run --project apps/AgentRuntimeLaws.Cli -- \
  manifest evidence/manifest.json
~~~
