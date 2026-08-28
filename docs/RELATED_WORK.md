# Related-work boundary

This project does not claim novelty for the state-plus-input to
state-plus-output reducer shape.

| Work | Established lane | Boundary of this project |
|---|---|---|
| Chassaing, Functional Event Sourcing Decider (2021) | F# event-sourcing Decider with decide, evolve, initial state, terminal state, and composition context | Effects are separated from replayable facts; fork cuts and oracle-result capture are assessed explicitly |
| Elm architecture | update maps a message and model to a model plus command | Deployment precedent, not a replay/fork soundness result |
| Mealy machines | Output and next state depend on current state and input | Foundational model, not agent-runtime effect or fork semantics |
| Liu, lambda_A, arXiv:2604.11767 | Typed agent composition, oracle calls, bounded fixpoints, probabilistic choice; mechanized in Coq | Composition-time type safety and bounded termination, not runtime replay/fork cuts |
| Garby, Gordon, Sands, LLMbda, arXiv:2602.20064 | Agent conversations and probabilistic noninterference with machine-checked security results | Information flow, not replay or environmental fork safety |
| Schlapbach, arXiv:2603.24747 | Process-calculus relation between SGD and MCP | Protocol expressivity, not runtime event-log semantics |
| Zhang, Agent libOS, arXiv:2606.03895 | Capability-controlled runtime with checkpoint restore, fork, and commit | Operational mechanism, not the soundness conditions on an event-log cut |
| McCann, arXiv:2605.01030 | Effect-transparent governance and expressive minimality, mechanized in Rocq | This project concedes minimality and studies replay/fork conditions |
| Wang, Poskitt, Sun, AgentSpec, ICSE 2026 | Trigger/predicate/enforcement rules for runtime safety | Runtime policy enforcement, not replay guarantees |
| Nakajima, arXiv:2605.21997 | ActiveGraph architecture and claims of deterministic replay, cheap forking, and lineage | This project tests and refines the conditions under which those claims hold |
| Burch, Passerone, Sangiovanni-Vincentelli, Notes on Agent Algebras (2003) | Existing Agent Algebra framework for concurrent models, refinement, and composition | Reason the former project name was retired |

## Required self-citation framing

The introduction should say directly:

> This paper formalizes and empirically checks the replay and fork properties
> claimed on architectural grounds in Nakajima (2026).

The relationship is an advantage only if negative results about ActiveGraph are
reported plainly.

## Primary links

- https://thinkbeforecoding.com/post/2021/12/17/functional-event-sourcing-decider
- https://guide.elm-lang.org/architecture/
- https://xlinux.nist.gov/dads/HTML/mealyMachine.html
- https://arxiv.org/abs/2604.11767
- https://arxiv.org/abs/2602.20064
- https://arxiv.org/abs/2603.24747
- https://arxiv.org/abs/2606.03895
- https://arxiv.org/abs/2605.01030
- https://conf.researchr.org/details/icse-2026/icse-2026-research-track/29/AgentSpec-Customizable-Runtime-Enforcement-for-Safe-and-Reliable-LLM-Agents
- https://arxiv.org/abs/2605.21997
- https://citeseerx.ist.psu.edu/document?doi=a73b7ff806c2bfcb2883c7121a160b17a52e7d15

## Non-goals

- Minimality or irreducibility.
- A new agent-composition calculus.
- Mechanized proof.
- Noninterference.
- Capability confinement.
- Performance benchmarking.

The paper genre is executable specification, property-based law checking,
preserved counterexamples, and deployed-log validation.
