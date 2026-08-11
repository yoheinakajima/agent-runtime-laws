namespace AgentRuntimeLaws

open System

[<StructuralEquality; StructuralComparison>]
type EventId = EventId of string

[<StructuralEquality; StructuralComparison>]
type EffectId = EffectId of string

[<StructuralEquality; StructuralComparison>]
type RunId = RunId of string

[<StructuralEquality; StructuralComparison>]
type BehaviorId = BehaviorId of string

type Footprint =
    | Pure
    | Idempotent
    | Compensatable
    | OneShot
    | UnknownFootprint

type ReplaySource =
    | Deterministic
    | Recorded
    | Uncaptured

type EffectLifecycle =
    | Requested
    | Committed
    | Failed
    | Unknown

type EffectDescriptor =
    { Id: EffectId
      Name: string
      Footprint: Footprint
      ReplaySource: ReplaySource
      Lifecycle: EffectLifecycle
      RequestHash: string
      ResponseHash: string option }

type IntegrityFault =
    | DuplicateEffectRequest of EffectId
    | EffectOutcomeWithoutRequest of EffectId
    | EffectAlreadyTerminal of EffectId * previous: EffectLifecycle
    | ConflictingEffectDescriptor of EffectId

type EvidenceFact =
    | EnvelopeCaptured
    | InvocationCompleted
    | BoundaryMediated
    | CleanReconstructionAvailable
    | VerificationPassed
    | CheckpointRecorded
    | NativeRuntime
    | HazardDetected of string
    | LossyEnvelope
    | UnmediatedEffect of string

type EventKind =
    | FactSet of key: string * value: int
    | FactIncremented of key: string * delta: int
    | SignalRaised of signal: string
    | EffectRequested of EffectDescriptor
    | EffectCommitted of effectId: EffectId * responseHash: string
    | EffectFailed of effectId: EffectId * reason: string
    | EffectBecameUnknown of effectId: EffectId
    | EvidenceRecorded of EvidenceFact

type Event =
    { Id: EventId
      RunId: RunId
      Sequence: int64
      Kind: EventKind
      CausedBy: EventId option
      EmittedBy: BehaviorId option }

type State =
    { Facts: Map<string, int>
      Signals: Set<string>
      Effects: Map<EffectId, EffectDescriptor>
      Evidence: EvidenceFact list
      IntegrityFaults: IntegrityFault list
      AppliedEvents: Set<EventId> }

type DomainProjection =
    { Facts: Map<string, int>
      Signals: Set<string>
      Effects: Map<EffectId, EffectDescriptor>
      Evidence: EvidenceFact list
      IntegrityFaults: IntegrityFault list }

module State =
    let empty =
        { Facts = Map.empty
          Signals = Set.empty
          Effects = Map.empty
          Evidence = []
          IntegrityFaults = []
          AppliedEvents = Set.empty }

    let domainProjection (state: State) : DomainProjection =
        { Facts = state.Facts
          Signals = state.Signals
          Effects = state.Effects
          Evidence = state.Evidence
          IntegrityFaults = state.IntegrityFaults }

type Emission =
    | SetFact of key: string * value: int
    | IncrementFact of key: string * delta: int
    | RaiseSignal of signal: string

type Action =
    | Emit of Emission
    | Request of EffectDescriptor

type Behavior =
    { Id: BehaviorId
      Trigger: State -> Event -> bool
      Fire: State -> Event -> Action list
      Writes: Set<string> }

type Activation =
    { BehaviorId: BehaviorId
      TriggeringEvent: Event }

type Configuration =
    { RunId: RunId
      State: State
      PendingEvents: Event list
      Enabled: Activation list
      Outstanding: Map<EffectId, EffectDescriptor>
      Trace: Event list
      FiringTrace: (BehaviorId * EventId) list
      NextSequence: int64
      NextGeneratedId: int64 }

type Scheduler =
    { Name: string
      SelectEvent: Event list -> int
      SelectActivation: Activation list -> int }

module Scheduler =
    let private first _ = 0

    let private last items =
        max 0 (List.length items - 1)

    let canonical =
        { Name = "canonical"
          SelectEvent = first
          SelectActivation = first }

    let eventReverse =
        { Name = "event-reverse"
          SelectEvent = last
          SelectActivation = first }

    let activationReverse =
        { Name = "activation-reverse"
          SelectEvent = first
          SelectActivation = last }

    let reverse =
        { Name = "reverse"
          SelectEvent = last
          SelectActivation = last }

    let indexed name eventSelector activationSelector =
        { Name = name
          SelectEvent = eventSelector
          SelectActivation = activationSelector }

type DivergenceReason =
    | StepBoundReached of bound: int

type SettleOutcome =
    | Quiescent of Configuration
    | BlockedAwaitingEffect of Configuration
    | Diverged of Configuration * DivergenceReason

type StepOutcome =
    | Progressed of Configuration
    | Terminal of SettleOutcome

type EffectOutcome =
    | Commit of EffectId * responseHash: string
    | Fail of EffectId * reason: string
    | MarkUnknown of EffectId

type Observation =
    | ExactTrace
    | ProjectedState
    | FactsOnly of keys: Set<string>

module Internal =
    let clampIndex length index =
        if length <= 0 then
            invalidArg (nameof length) "cannot select from an empty collection"

        Math.Clamp(index, 0, length - 1)

    let removeAt index items =
        items
        |> List.mapi (fun i value -> i, value)
        |> List.choose (fun (i, value) -> if i = index then None else Some value)
