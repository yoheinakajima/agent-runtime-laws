This is a strong companion paper to *The Log is the Agent*. The core move is right: take the predecessor’s architectural slogans (deterministic replay, cheap fork, lineage) and turn them into checkable, property-indexed claims. The draft is already unusually honest for work that evaluates the author’s own runtime. The main risks are overclaiming “laws,” running two papers in one, and leaving the hard empirical cases untested.

## Summary

The paper asks when event-sourced agent-runtime claims about replay and forking are *sound*, not whether another reducer shape is minimal.

It gives an executable F# model in which:

- a pure reducer folds a fixed log into state;
- behaviors react to events and emit facts or effect *requests*;
- outcomes come only from an interpreter outside the kernel;
- scheduler order is an explicit parameter.

Settlement can quiesce, block on an outstanding effect, or hit a step bound.

Two negative results follow:

1. **Termination is not free.** A self-triggering signal loops past every bound.
2. **Confluence is not free.** Overlapping writers disagree under activation reversal. Pairwise-disjoint *declared writes* still fail when emissions, event order, triggers, and reads interfere.

Forking is then indexed by four properties:

- **ProjectionReplay** — fold the retained prefix.
- **StrictExecutionReplay** — replay with zero live oracle/tool calls.
- **ExternalContinuation** — continue in an environment assumed to represent the prefix.
- **CounterfactualWorld** — the actual world matches one in which the discarded suffix never happened.

Effects are split along three orthogonal axes: **footprint**, **replay source**, and **lifecycle**. That split is the conceptual heart of the paper. “Cached” answers where the bytes come from; it does not say whether the original call spent money or mutated the world.

Validation uses two corpora (5,564 runs, 314,348 source events):

- Synthetic Players: every one of 317,296 projection cuts is sound; 36,251 cuts split an LLM request and are unsound for continuation; 121 observed forks match 16,625 prefix events with zero structural mismatches, and those real cuts avoid classified external requests.
- Bridge study: 66.7% decision agreement, 0% ordered-path agreement; Boundary grade everywhere; no cut is unconditionally Sound for CounterfactualWorld.

The practical conclusion is sharp: replay is a family of assertions, and a fork is sound only relative to one of them.

## What works

**The problem is real and timely.** Agent frameworks now advertise replay, time-travel, branching, and “the log is the source of truth.” Those phrases collapse several inequivalent claims. The seven-level hierarchy in §9.1 (byte / trace / projection / path / decision / execution / environment) is the sentence people will quote. The bridge numbers make it concrete: same decision, different path.

**Property-relative safety is the right contract.** `fork(L, k) = prefix(L, k)` is computationally cheap and semantically incomplete. Distinguishing “the child projects the prefix” from “the world is as if the suffix never happened” is the contribution. Table 2 is the paper’s best artifact.

**The three effect dimensions earn their keep.** A model call can be OneShot in footprint (cost, oracle interaction) and Recorded in replay source. Flat taxonomies (Pure / Cached / OneShot) smear those questions together. The request/outcome integrity rule is also the right fail-closed default.

**Genre honesty is a feature.** The draft says it is not a Coq paper, that passing FsCheck is finite evidence, and that negative results about ActiveGraph are part of the contribution. That is rarer than it should be, and it will help with reviewers who would otherwise treat this as marketing for [8].

**The empirical scale is unusual for this genre.** Exhaustive all-cut assessment over hundreds of thousands of prefixes is more persuasive than another toy calculus. The observed-fork audit (zero mismatches modulo expected lineage fields) is a genuine positive result, and the paper does not pretend those forks tested oracle boundaries.

**Limitations are written like a reviewer already sat down.** Bridge provenance, missing production behavior inventory, no observed effect-boundary fork, adapter conservatism, and “law” language are all named. Keep that tone.

## Critique

### 1. This is two papers sharing a kernel

Paper A: reactive settlement is neither terminating nor confluent in general; FIFO hides that.

Paper B: forks are only sound relative to named observations and effect evidence.

They share a model, but the evidence is almost all Paper B. Confluence is three fixtures plus a restricted writer family. Production confluence (V3) is explicitly incomplete. A PL or runtime reviewer will ask why the title leads with confluence when the validation does not measure production non-interference.

Either:

- demote confluence to a supporting section and retitle around fork-safety / replay licenses, or
- finish V3 with a real behavior-dependency inventory, even on a subset of ActiveGraph behaviors.

Right now the title promises a theorem the body refuses to claim.

### 2. “Law” and “proposition” overstate the method

Proposition 1 is “fold is deterministic.” Propositions 2–4 are prefix identities. Proposition 5 is the interesting one, and it is a definitional correction, not a derived theorem.

FsCheck plus named fixtures is a good *engineering* method. Calling the results laws invites a comparison with λA (Coq, 42 theorems) and LLMbda (machine-checked noninterference) that this draft will lose on its own terms.

Better vocabulary: *executable contracts*, *checked properties*, *regression fixtures*. Keep “proposition” for the few statements that are actually identities of the model.

### 3. The hard fork case is untested in the wild

The 121 observed forks all cut at `round.played` and retain no classified external request. The paper says this. Reviewers will still treat “zero mismatches” as the headline and miss the caveat.

The interesting claim is: can you fork *after* a committed OneShot model call, serve the recorded result, and refuse re-execution? That case exists only in all-cut analysis and fixtures. One public, hash-pinned fork that crosses an oracle boundary with a no-reexecution receipt would change the paper more than another 100 domain-only forks.

### 4. Related work is too local

The citations to Mealy, Elm, Decider, λA, LLMbda, McCann, AgentSpec, and Agent libOS are fair and well bounded. Missing neighbors that a SE/systems reviewer will expect:

- Classic event sourcing / CQRS (Young, Vernon) and why business event sourcing never claimed to rewind the world.
- Sagas, outbox, and compensation — your Compensatable footprint is that literature.
- Workflow/replay engines: Temporal, Cadence, Durable Functions. They already distinguish history replay from side-effect execution.
- CRDTs / join-semilattices as the actual confluence story for commutative writes.
- LangGraph / Crew checkpoints and “time-travel” UIs — the competing product claim.
- Snapshot isolation / MVCC as the database analogue of “prefix plus isolated child environment.”
- Deterministic simulation and record-replay (FoundationDB, rr, DEBS).

Without those, the novelty paragraph (“we are not aware of a study that jointly does i–v”) is easy to attack as search-limited, which the draft already admits.

### 5. Section 5 is a third paper

Replay grades as a lattice of licensed questions is neat, and Counterexample 4 (hazard downgrade) is good. But the grades are a *summary statistic* over evidence the fork assessor already computes cut-by-cut. The lattice laws are definitional. For a 21-page draft, this section costs focus.

Compress it to one page: grades license *classes* of questions; cuts decide *this* fork. Move the lattice checks to the artifact appendix.

### 6. The model is described, not specified

A reader cannot reconstruct the small-step relation from the text alone. Configurations are listed in prose; `C →σ C′` is introduced without inference rules; “later activation observes earlier writes” is the load-bearing scheduling choice and deserves a rule.

You do not need Coq. You need one figure: configuration grammar + two rules (process-event, fire-activation) + the three settlement outcomes. That would also make the counterexamples checkable on paper.

### 7. Self-evaluation and corpus narrowness

Both corpora are ActiveGraph-family and synthetic. “Real” is carefully defined as “actual runtime artifacts,” which is fair, but the paper then generalizes to “event-sourced agent runtimes.” One external corpus — even a small LangGraph or Temporal export — would show the contract is not an adapter for one store.

The bridge study’s missing Git remote is worse because provenance is part of the thesis. Either publish it or drop the quantitative claims to a footnote until you can.

### 8. Presentation

- Affiliation is missing. The predecessor lists Untapped Capital / activegraph.ai.
- No figures. This paper is begging for: (1) effect-dimension cube or matrix, (2) the four properties as a stack, (3) a cut-through-request timeline, (4) the replay-claim hierarchy.
- The abstract is a page. Split: 150-word abstract + a “results at a glance” box.
- Line-count confession (§6.1, §10) reads as internal process notes. Reviewers do not care that you missed a 1,000-line target.
- “Research brief” and “initial version asked whether *A : S × E → S × F\**” are lab-notebook residue. Keep the concession of minimality; drop the genealogy.
- Tables 4–5 would be stronger with percentages and a “per-run” column (you already have 4,923 runs with some unsound counterfactual cut).

### 9. A few technical nits

- Definition 2 is Church–Rosser-shaped only for *quiescent* normal forms. BlockedAwaitingEffect is a second terminal class and is excluded. Say that explicitly, or confluence under blocking is a different proposition.
- Whole-log malformedness blocking *every* cut, including projection of a clean prefix, is stricter than Proposition 4. You note this. Consider reporting both policies.
- Model calls as default OneShot is conservative and will classify some local/deterministic models too harshly. A sensitivity row (“if models were Idempotent+Recorded, ExternalContinuation unsound drops from X to Y”) would show the classification is doing work.
- 36,251 unsound continuation cuts = number of `llm.requested` events. That is clean. State the exact cut rule: the unsound cut is the prefix that contains the request and not its terminal outcome.
- CounterfactualWorld Sound 150,230 vs Continuation Sound 160,076: a one-sentence accounting of the gap would help.
- AgentSpec as ICSE 2026: verify the venue record before submission; preprint-to-proceedings drift is common.

## Comments on the argument

The predecessor paper [8] says deterministic replay, cheap forking, and lineage *fall out of* making the log authoritative. This draft is the necessary correction: they fall out only after the log is fixed, the schedule is named, oracle results are captured, and the claimed property is weaker than “the world didn’t happen.”

That is a good intellectual sequence. The danger is sounding like a retraction. It does not have to. Frame it as: [8] is the architecture; this paper is the contract. ActiveGraph’s FIFO+registration order is a legitimate *operational* contract. It is not evidence of schedule independence. That sentence is already in §3.3 and should be in the abstract.

The deepest correct observation is §9.5: the graph can be a projection; the scheduler and the interpreter cannot be wished away. Once two behaviors can fire on one event, order is semantics unless you impose non-interference. Once an effect leaves the process, the world is a second state. That is why a Decider that returns only domain events cannot express the agent-runtime question.

I would push that further. The missing primitive is not `A : S × E → S × F*`. It is something like:

\[
\text{step}: C \times \Sigma \rightarrow C \qquad
\text{interpret}: R \rightarrow \{\text{Committed},\text{Failed},\text{Unknown}\}
\]

with evidence that ties interpreter outcomes back to request identities. The paper already implements this. Name it in one line in the introduction.

## Suggested improvements, in priority order

1. **Retitle or rebalance.**
   *Fork-Safety and Replay Licenses in Event-Sourced Agent Runtimes* (confluence as §3) will match the evidence better than the current title.

2. **Add three figures** before any more prose: scheduling/interference, cut-through-request, property stack.

3. **Publish or quarantine the bridge artifact.** Same standard you apply to the capsule.

4. **Exhibit one effect-boundary fork** in a public store, with a served-from-record / zero-reexecution receipt.

5. **Write the small-step rules.** One column. The counterexamples become readable.

6. **Widen related work** to event sourcing, sagas, workflow replay, and product checkpointing. Then the novelty claim is “property-indexed fork assessment against archived agent logs,” which is true and smaller.

7. **Add a sensitivity table** for footprint classification (models as OneShot vs Idempotent).

8. **Drop internal process.** Line counts, research-brief history, 1,000-line target.

9. **Give the fork API a type in the discussion.** Something a implementer can copy:

   `fork(log, k, property) → Ok(child) | Refuse(reason)`

   That makes §9.6 concrete.

10. **Optional but high leverage:** run the normalizer on one non-ActiveGraph trace source, even if most events stay unclassified. It shows the contract is about evidence, not about your event names.

## Verdict

Ready as a research draft; not yet ready as a submission without a structural pass.

The idea is good enough that the paper should be *smaller and sharper*, not longer. Protect three things:

- effects have three dimensions, not one bucket;
- fork soundness is indexed by a named property;
- observed prefix copying is strong, unrestricted continuation is not.

Everything else — grades lattice, line counts, minimality autobiography, four scheduler policies as a confluence “result” — is support. If a reviewer can leave with those three claims and Table 4, the paper has done its job.
