# Independent Codex technical review

Review target: commit
`768a30542b61951a047b80f0c8186a099634b3a4` on
`review/explore-science-round1-2026-08-28`.

Method: fresh isolated, read-only review of the manuscript, evidence and
findings ledgers, executable kernel, tests, validation scripts, pinned
ActiveGraph source, and public activegraph-bridge fixture. The reviewer did not
edit the repository. This file preserves the report against the exact reviewed
commit; later dispositions do not rewrite it.

## Verdict

Not arXiv-ready at commit `768a30542b61951a047b80f0c8186a099634b3a4`.

The empirical results and denominators are reproducible and internally
consistent. No Critical defect or numerical invalidation was found. Submission
is blocked by one central scheduler-semantics contradiction, overbroad headline
phrasing for the bridge evidence, and the manuscript's own unresolved
publication gates.

## Critical

None.

## Major

### 1. Production scheduler semantics contradict the declared authoritative ledgers

Submission blocker at the reviewed commit.

- The manuscript correctly states that an earlier ActiveGraph handler may
  synchronously project an emitted event before a later handler reads graph
  state: `paper/sections/03-confluence.tex:122-135`.
- The authoritative ledger explicitly denies that the source audit establishes
  this and instead reports same-state behavior: `EVIDENCE.md:36-46`.
- `FINDINGS.md:61-65` similarly presents same-trigger snapshot behavior without
  distinguishing kernel from production.
- `paper/README.md:3-5` says `EVIDENCE.md` and `FINDINGS.md` win when claims
  differ.
- The F# kernel genuinely uses snapshot semantics:
  `src/AgentRuntimeLaws/Kernel.fs:172-190,248-259`; its regression locks that
  choice at `tests/AgentRuntimeLaws.Properties/KernelProperties.fs:139-172`;
  the paper specifies it at `paper/sections/02-model.tex:125-133`.

The pinned ActiveGraph source establishes the precise production behavior:

- eligibility and relation/pattern matches are computed once:
  `activegraph/runtime/registry.py:40-70` and
  `activegraph/runtime/runtime.py:1269-1296`;
- each handler view is rebuilt against the current graph:
  `activegraph/runtime/runtime.py:1389-1435`;
- handler emission synchronously invokes `Graph.emit`:
  `activegraph/runtime/behavior_graph.py:141-153`;
- `Graph.emit` projects before later notification/queue processing:
  `activegraph/core/graph.py:564-606`.

Therefore, eligibility is fixed for the triggering event, but later handlers
can read earlier handlers' mutations. The paper paragraph is directionally
correct; the ledgers contain stale hardening language and fail to disclose the
kernel/production abstraction difference.

### 2. Bridge headline conflates conformance evidence with environmental truth

Submission blocker until narrowed.

The fixture is valid: the generator directly observes that the mock oracle call
count does not increase, and the receipt verifier passes. But the verifier
checks a process-produced zero-call counter plus hash/log consistency; the HMAC
authenticates a caller-issued assertion under a deliberately public fixture key.
It does not verify snapshot contents or establish that the environment truly
represents the retained prefix.

- Overbroad headline language: `paper/main.tex:58-61`,
  `paper/sections/07-validation.tex:253-256`, and
  `paper/sections/11-conclusion.tex:25-27`.
- More accurate detailed caveats already exist:
  `paper/sections/07-validation.tex:131-156` and
  `paper/sections/10-limitations.tex:68-80`.
- Direct call-count observation:
  `activegraph-bridge/scripts/generate_post_oracle_fixture.py:94-127`.
- Receipt/internal-consistency checks:
  `activegraph-bridge/src/activegraph_bridge/receipts.py:403-448`.
- Environment claim verification:
  `activegraph-bridge/src/activegraph_bridge/runs.py:947-982`.
- The signed claims bind fork identifiers, prefix, and target fingerprint, not
  environment contents:
  `activegraph-bridge/evidence/post-oracle-fork-v1/receipt.json:48-64`.

The supported claim is: one deterministic offline fixture fork served its
inherited recorded oracle result without another observed fixture-oracle call,
while validating a fork-bound HMAC assertion under a public conformance key.

### 3. Publication and artifact provenance gates remain unresolved

Submission blocker under the project's own protocol.

- `agent-runtime-laws` is private; the manuscript says the artifact accompanies
  the draft: `paper/main.tex:65-72`.
- The submission checklist requires making it public and freezing the revision:
  `paper/README.md:19-28`; status remains provisional at `README.md:153-161`.
- The 24-run study has no public remote:
  `paper/sections/07-validation.tex:158-175`,
  `paper/sections/10-limitations.tex:82-89`, and `EVIDENCE.md:245-258`.
- The checklist requires publishing that bundle or removing the study:
  `paper/README.md:23-24`.

The bridge commit is publicly fetchable, but it is the head of an open draft
pull request, not default `main` or a release.

## Minor

### 1. Bridge release status should be exact

`EVIDENCE.md:195-205` accurately says “public review branch,” but
`paper/sections/09-discussion.tex:65-67` and
`paper/sections/07-validation.tex:225-226` can read as though v0.2 were
released. Call it an “unreleased v0.2 draft-PR implementation at commit
`8855d3...`,” or merge, tag, or archive it.

### 2. Local-study hash table mixes source and release namespaces

`EVIDENCE.md:260-271` follows discussion of the release bundle but lists the
original uncompressed source-store and source-manifest hashes. The release
bundle intentionally has a redacted manifest and compressed store with
different hashes. `paper/sections/07-validation.tex:166-172` repeats the source
hashes without naming that namespace. Label `SOURCE_SHA256SUMS` and
release-payload hashes explicitly. This does not affect the reproduced study
counts.

### 3. Observed-fork SQL vocabulary differs from the normalizer

`scripts/audit_activegraph_forks.sql:118-149` includes `human.*` but omits
`effect.*`, `retrieval.*`, and `external.*`; the normalizer vocabulary is at
`src/AgentRuntimeLaws/Validation.fs:180-190,216-227`. The corpus contains only
`llm.*`, so reported results are unaffected, but
`paper/sections/06-method.tex:102-112` describes the audit generically. Share
one vocabulary or state that this SQL is corpus-specific.

No citation blocker was found. The seven arXiv records and the cited official
Temporal, Azure, DBOS, Restate, and LangGraph documentation resolve and support
their attributed descriptions.

## Required fixes

1. Commit the scheduler correction across manuscript, `EVIDENCE.md`, and
   `FINDINGS.md`, explicitly separating fixed eligibility from evolving
   per-handler reads and production semantics from kernel semantics.
2. Rewrite bridge headlines to attribute zero-call evidence to the fixture
   generator/test and describe the verifier as checking receipt consistency
   plus a fork-bound caller assertion.
3. Make `agent-runtime-laws` public and freeze the submitted revision.
4. Publish the local-study bundle or remove its subsection, table, conclusion
   reference, and ledger claims.
5. Merge, tag, or archive the bridge artifact, or disclose its draft-PR status
   everywhere.
6. Separate source/release hashes and align the request vocabularies.
7. Rerun tests, both empirical analyses, bridge verification, PDF build, and
   visual QA from the final frozen commit.

## QA evidence inspected

- Full `main.tex`, all eleven included sections, bibliography, `EVIDENCE.md`,
  `FINDINGS.md`, kernel, fork assessor, validators, tests, conformance vectors,
  manifests, SQL, and relevant pinned ActiveGraph/bridge source.
- Reproduced the public-corpus analysis exactly: 5,540 runs, 311,756 events,
  317,296 cuts; continuation `160,076 / 120,969 / 36,251`; counterfactual
  `150,230 / 24,172 / 142,894`; 4,923 affected runs.
- Reproduced the observed-fork audit: 121 forks, 41 parents, 16,625 compared
  events, zero mismatches, zero missing cuts, zero retained requests.
- Reproduced the local study: 24 runs, 2,592 source events, 2,640 normalized
  events, 2,616 primary cuts, and every reported verdict total.
- Bridge verifier passed; bridge suite passed 76/76. Exact bridge CI is green on
  Python 3.11, 3.12, and 3.13.
- Exact target CI passed 50/50, conformance vectors, and evidence-manifest
  validation.
- The checked-in 27-page PDF at the reviewed commit had SHA-256 prefix
  `80613df`; an isolated Tectonic rebuild succeeded, extracted text matched,
  and all pages passed visual inspection.

