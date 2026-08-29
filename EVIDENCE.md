# Evidence ledger

The validation harness is read-only. It normalizes ordered JSONL logs into the
kernel event model, computes a replay grade, evaluates every cut from zero
through the normalized log length, separately reports atomic source-event
boundaries, and reports unclassified source event types. External verification
is not silently promoted into log evidence.

## Checked-in harness fixtures

These authored fixtures contain no production payloads. They test normalization
and fail-closed behavior only.

| Fixture | Profile | Purpose | Provenance |
|---|---|---|---|
| evidence/fixtures/bridge-sanitized.jsonl | bridge | boundary-grade adapter | synthetic, sanitized, shape-conformant |
| evidence/fixtures/activegraph-sanitized.jsonl | activegraph | effect lifecycle adapter | synthetic, sanitized, shape-conformant |
| tests/fixtures/activegraph-recorded.jsonl | activegraph | recorded-result regression | authored test fixture |
| tests/fixtures/fail-closed.jsonl | activegraph | unknown-footprint regression | authored test fixture |
| tests/fixtures/bridge-multi-derived.jsonl | bridge | source-boundary versus intra-expansion cut regression | authored test fixture |

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
under other legal schedules. `Registry.match` fixes the enabled behaviors and
their relation/pattern matches before the handler loop. Each invocation then
calls `build_view` against the current graph. A handler's `BehaviorGraph.emit`
calls `Graph.emit`, which appends and projects synchronously before the emitted
event is queued for later behavior matching. An earlier handler can therefore
change state read by a later handler of the same triggering event, even though
it cannot change that trigger's already-computed activation set. Relation
enumeration has no documented canonical ordering.

The executable kernel deliberately chooses a narrower snapshot semantics:
activations for one trigger read the same state, while emitted events become
visible at later event-processing steps. Its properties therefore check the
kernel contract rather than reproducing ActiveGraph's per-invocation view
refresh. The later-event read/trigger counterexample remains applicable, and
the production source audit supplies an additional reason not to infer
schedule independence from one stable ProductionOrder.

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
36,227 `llm.responded` events, and 30,397 `decision.parsed` events. It contains
5,505 completed and 35 incomplete runs. The verifier covers 4,916 confirmatory
Phase 3--5 runs plus 3 legacy diagnostics. The remaining 586 completed runs are
outside that verifier contract. The complete reconciliation is therefore
4,919 verifier-covered + 586 other completed + 35 incomplete = 5,540 store
runs. The capsule verifier and the kernel answer different questions: the
former verifies the sealed artifact, while the latter grades evidence retained
inside each run log.

### All-cut property results

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
are exactly the cuts through outstanding LLM requests. Direct sequence audit
found 36,227 requests immediately followed by their linked response, yielding
one open-boundary cut each. The other 24 requests are the final event of a
failed run, also yielding one open-boundary cut each. Thus 36,227 + 24 = 36,251;
an unmatched request does not create later cuts because no later event exists in
those runs. Conditional cuts retain an already committed oracle call and
require serving the recorded result rather than re-executing it.
Counterfactual-world verdicts are stricter because a call in the discarded
suffix still occurred and may already have incurred cost.

A separate ordinal query confirms that all 160,076 Sound
ExternalContinuation cuts occur before the first classified external request;
617 runs contain no classified request at all. The public corpus therefore does
not empirically validate a post-oracle continuation, a target-environment
attestation, or a zero-reexecution receipt. The F# assessor takes the target
environment as a supplied premise and computes typed obligations; the companion
bridge fixture below separately implements and tests a deployment-side
receipt path that signature-checks and fork-binds a caller-issued environment
assertion without independently validating its contents.

The confirming read-only SQLite query was:

~~~sql
WITH ordered AS (
  SELECT
    run_id,
    type,
    ROW_NUMBER() OVER (PARTITION BY run_id ORDER BY seq) AS ordinal,
    COUNT(*) OVER (PARTITION BY run_id) AS n
  FROM events
),
per_run AS (
  SELECT
    run_id,
    MAX(n) AS n,
    MIN(CASE WHEN type IN (
      'llm.requested', 'model.requested', 'embedding.requested',
      'tool.requested', 'human.requested', 'retrieval.requested',
      'external.requested', 'effect.requested'
    ) THEN ordinal END) AS first_request
  FROM ordered
  GROUP BY run_id
)
SELECT
  SUM(CASE WHEN first_request IS NULL THEN n + 1 ELSE first_request END),
  SUM(CASE WHEN first_request IS NULL THEN 1 ELSE 0 END),
  COUNT(*)
FROM per_run;
-- 160076 | 617 | 5540
~~~

The remaining aggregate verdicts have an independent structural audit in
`scripts/audit_synthetic_cut_identities.sql`:

~~~bash
sqlite3 -header -column \
  'file:/tmp/synthetic-players-engine.db?mode=ro&immutable=1' \
  < scripts/audit_synthetic_cut_identities.sql
~~~

The query verifies 36,227 immediately linked request/response pairs, 24
terminal unmatched requests, and zero other request shapes. It reproduces:

| Identity | Result |
|---|---:|
| Total cuts | 317,296 |
| ExternalContinuation Sound | 160,076 |
| ExternalContinuation Unsound | 36,251 |
| ExternalContinuation Conditional | 120,969 |
| CounterfactualWorld Unsound, closed-response runs | 141,545 |
| CounterfactualWorld Unsound, open runs | 1,349 |
| CounterfactualWorld Unsound total | 142,894 |
| Runs with at least one classified request | 4,923 |

For a request-bearing run with event count `n`, first-request ordinal `q`, and
request count `m`, the continuation-Conditional identity is
`n + 1 - q - m`. For CounterfactualWorld, a run with all requests closed
contributes the ordinal of its final response; a run ending open contributes all
`n + 1` cuts. Of the 24 open runs, four contain no prior response and 20 are
already among the 4,919 runs with at least one response, reconciling the 4,923
affected-run union.

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
type, actor, payload, frame, cause, and timestamp. Child run identity and
`events.seq` are intentionally excluded. In this schema, `events.seq` is the
database-wide autoincrement primary key used to order each run, not a run-local
envelope field; the per-run contiguous sequence is derived only after export.
The forks satisfy
the current external-continuation precondition, but they do not validate cuts
through external effects because none of the 121 retained prefixes contains
one.

## Public activegraph-bridge post-oracle fork receipt

### Provenance

| Field | Value |
|---|---|
| Repository | https://github.com/yoheinakajima/activegraph-bridge |
| Pull request | https://github.com/yoheinakajima/activegraph-bridge/pull/2 (merged) |
| Release | https://github.com/yoheinakajima/activegraph-bridge/releases/tag/v0.2.0 |
| Revision | `843824a44d48d816779fc0c08580ae06108fe7b6` |
| Fixture | `evidence/post-oracle-fork-v1` |
| Publication status | public v0.2.0 source release, immutable revision pinned |

The fixture, merged to the public default branch, records an actual bridge fork after one committed recorded
fixture-oracle call. The inherited effect is classified `one_shot + recorded`
with `provider.cost` and `provider.oracle` observables. The parent makes one
deterministic offline oracle call. The generator directly observes the call
counter before and after the child operation. Verification and the child serve
request `evt_008` from recorded outcome `evt_009`; that process-produced
inherited-prefix external-call count is zero. A changed tool result creates a
divergent child tail.

| File or field | SHA-256 / value |
|---|---|
| `parent.jsonl` | `884e8ae3429604b2d7a24dd7fff56c05f8d471427256faeadda18047d5c7c176` |
| `child.jsonl` | `747305a6774a3a505820a4b6a2ccbaf3556fb40d76dbef4d83873b9a483faaa6` |
| `receipt.json` | `da5503a80b9276f13f0fe37ce4cbf61a3ddb2416a7ec64c664084ca1f7883164` |
| `environment-attestation.json` | `43a0e7bb6dec6ba9bca578f87b169ecb02224dcf8e82702ea0860a6678ccc584` |
| `manifest.json` | `dac48a19b35b6c72756ee3cb38a923670685c01a70ac2216d4880fe891f82f07` |
| canonical prefix hash | `3de3251368f20c4da2d234ac101e8a31b7b3142ede845157d9dcf39d69ccd8e4` |
| canonical receipt hash | `5d36df7dc177a9801fa8ff6a4b070612fd0b16e4c45207825498fda03af3d3f0` |
| source oracle calls | 1 |
| inherited external calls in fork | 0 |

The receipt binds parent and child identities, the cut, source/prefix/child-log
hashes, inherited request and outcome identities, source and target runtime
fingerprints, and target-environment claims. The verifier rejects receipt,
attestation, or log tampering; verifies an HMAC-SHA256 signature using a
configured trust root; binds the caller-issued assertion to the child, cut,
prefix, and target fingerprint; and checks receipt/log consistency around the
process-produced zero-call counter. It does not inspect or independently
establish the asserted environment contents.

~~~bash
./evidence/post-oracle-fork-v1/verify.sh
# POST-ORACLE FORK RECEIPT PASS — committed oracle served from record;
# 0 inherited external calls
~~~

The HMAC key is deliberately published as a conformance trust root. This is
evidence for the receipt protocol and zero-reexecution mechanism, not for a real
provider, production identity, model quality, or production environmental
fidelity.

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
  merely contain words such as `verification` remain unclassified. A profile's
  claim that its recognized effect-request vocabulary is complete is an
  explicit coverage premise; unclassified domain events cannot establish a
  complete cross-runtime footprint claim.
- A recorded response proves replay material is present. It does not by itself
  prove an external write committed or was idempotent.
- Unknown effect footprint, malformed lifecycle, absent ordering authority, or
  missing receipt evidence fail closed within the declared adapter vocabulary.
- Production behavior-set conformance remains open because no complete
  read/write/trigger inventory was available.
- A cut verdict is relative to the exact log snapshot presented to the assessor.
  Production continuation requires a source high-water mark plus an explicit
  closure, quiescence, or freshness policy and a final recheck; a hazard appended
  later can downgrade the evidence.

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
