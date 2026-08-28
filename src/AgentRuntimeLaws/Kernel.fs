namespace AgentRuntimeLaws

module Kernel =
    let private normalizeRequest (effect: EffectDescriptor) : EffectDescriptor =
        { effect with
            Lifecycle = Requested
            ResponseHash = None }

    let private sameRequest (left: EffectDescriptor) (right: EffectDescriptor) : bool =
        left.Id = right.Id
        && left.Name = right.Name
        && left.Footprint = right.Footprint
        && left.ReplaySource = right.ReplaySource
        && left.RequestHash = right.RequestHash

    let private addFault (fault: IntegrityFault) (state: State) : State =
        { state with
            IntegrityFaults = state.IntegrityFaults @ [ fault ] }

    let private applyRequest (descriptor: EffectDescriptor) (state: State) : State =
        let requested = normalizeRequest descriptor

        match Map.tryFind requested.Id state.Effects with
        | None ->
            { state with
                Effects = Map.add requested.Id requested state.Effects }
        | Some existing when sameRequest existing requested ->
            addFault (DuplicateEffectRequest requested.Id) state
        | Some _ ->
            addFault (ConflictingEffectDescriptor requested.Id) state

    let private applyOutcome (effectId: EffectId) (lifecycle: EffectLifecycle) (responseHash: string option) (state: State) : State =
        match Map.tryFind effectId state.Effects with
        | None ->
            addFault (EffectOutcomeWithoutRequest effectId) state
        | Some effect when effect.Lifecycle = Requested ->
            let updated =
                { effect with
                    Lifecycle = lifecycle
                    ResponseHash = responseHash }

            { state with
                Effects = Map.add effectId updated state.Effects }
        | Some effect ->
            addFault
                (EffectAlreadyTerminal(effectId, effect.Lifecycle))
                state

    let evolve (state: State) (event: Event) : State =
        if Set.contains event.Id state.AppliedEvents then
            state
        else
            let applied = Set.add event.Id state.AppliedEvents

            let evolved =
                match event.Kind with
                | FactSet(key, value) ->
                    { state with
                        Facts = Map.add key value state.Facts }
                | FactIncremented(key, delta) ->
                    let current =
                        Map.tryFind key state.Facts
                        |> Option.defaultValue 0

                    { state with
                        Facts = Map.add key (current + delta) state.Facts }
                | SignalRaised signal ->
                    { state with
                        Signals = Set.add signal state.Signals }
                | EffectRequested descriptor ->
                    applyRequest descriptor state
                | EffectCommitted(effectId, responseHash) ->
                    applyOutcome
                        effectId
                        Committed
                        (Some responseHash)
                        state
                | EffectFailed(effectId, _) ->
                    applyOutcome effectId Failed None state
                | EffectBecameUnknown effectId ->
                    applyOutcome effectId Unknown None state
                | EvidenceRecorded evidence ->
                    { state with
                        Evidence = state.Evidence @ [ evidence ] }

            { evolved with AppliedEvents = applied }

    let project (events: Event list) : State =
        events |> List.fold evolve State.empty

    let private generatedId (RunId runId) (nextId: int64) : EventId =
        EventId(sprintf "%s:event:%06d" runId nextId)

    let private createPending
        (kind: EventKind)
        (causedBy: EventId option)
        (emittedBy: BehaviorId option)
        (configuration: Configuration)
        : Event * Configuration =
        let event =
            { Id =
                generatedId
                    configuration.RunId
                    configuration.NextGeneratedId
              RunId = configuration.RunId
              Sequence = 0L
              Kind = kind
              CausedBy = causedBy
              EmittedBy = emittedBy }

        event,
        { configuration with
            NextGeneratedId = configuration.NextGeneratedId + 1L }

    let enqueue
        (kind: EventKind)
        (causedBy: EventId option)
        (emittedBy: BehaviorId option)
        (configuration: Configuration)
        : Event * Configuration =
        let event, next =
            createPending kind causedBy emittedBy configuration

        event,
        { next with
            PendingEvents = next.PendingEvents @ [ event ] }

    let initialize
        (runId: RunId)
        (seed: EventKind list)
        : Configuration =
        let empty =
            { RunId = runId
              State = State.empty
              PendingEvents = []
              Enabled = []
              Outstanding = Map.empty
              Trace = []
              FiringTrace = []
              NextSequence = 1L
              NextGeneratedId = 1L }

        seed
        |> List.fold
            (fun configuration kind ->
                enqueue kind None None configuration |> snd)
            empty

    let private unresolved
        (effects: Map<EffectId, EffectDescriptor>)
        =
        effects
        |> Map.filter (fun _ effect ->
            effect.Lifecycle = Requested
            || effect.Lifecycle = Unknown)

    let private processEvent
        (behaviors: Behavior list)
        (scheduler: Scheduler)
        (configuration: Configuration)
        : Configuration =
        let index =
            scheduler.SelectEvent configuration.PendingEvents
            |> Internal.clampIndex configuration.PendingEvents.Length

        let pending = configuration.PendingEvents[index]

        let event =
            { pending with
                Sequence = configuration.NextSequence }

        let state = evolve configuration.State event

        let enabled =
            behaviors
            |> List.filter (fun behavior ->
                behavior.Trigger state event)
            |> List.map (fun behavior ->
                { BehaviorId = behavior.Id
                  TriggeringEvent = event })

        { configuration with
            State = state
            PendingEvents =
                Internal.removeAt
                    index
                    configuration.PendingEvents
            Enabled = enabled
            Outstanding = unresolved state.Effects
            Trace = configuration.Trace @ [ event ]
            NextSequence = configuration.NextSequence + 1L }

    let private emissionKind = function
        | SetFact(key, value) -> FactSet(key, value)
        | IncrementFact(key, delta) ->
            FactIncremented(key, delta)
        | RaiseSignal signal -> SignalRaised signal

    let private materializeActions
        (activation: Activation)
        (actions: AgentRuntimeLaws.Action list)
        (configuration: Configuration)
        : Configuration =
        actions
        |> List.fold
            (fun current action ->
                let kind =
                    match action with
                    | Emit emission -> emissionKind emission
                    | Request descriptor ->
                        EffectRequested(
                            normalizeRequest descriptor
                        )

                enqueue
                    kind
                    (Some activation.TriggeringEvent.Id)
                    (Some activation.BehaviorId)
                    current
                |> snd)
            configuration

    let private fireActivation
        (behaviors: Behavior list)
        (scheduler: Scheduler)
        (configuration: Configuration)
        : Configuration =
        let index =
            scheduler.SelectActivation configuration.Enabled
            |> Internal.clampIndex configuration.Enabled.Length

        let activation = configuration.Enabled[index]

        let behavior =
            behaviors
            |> List.tryFind (fun candidate ->
                candidate.Id = activation.BehaviorId)
            |> Option.defaultWith (fun () ->
                invalidOp (
                    sprintf
                        "behavior %A is not registered"
                        activation.BehaviorId
                ))

        let remaining =
            Internal.removeAt index configuration.Enabled

        let actions =
            behavior.Fire
                configuration.State
                activation.TriggeringEvent

        { configuration with
            Enabled = remaining
            FiringTrace =
                configuration.FiringTrace
                @ [ activation.BehaviorId,
                    activation.TriggeringEvent.Id ] }
        |> materializeActions activation actions

    let private terminalOutcome configuration =
        if not configuration.Enabled.IsEmpty then
            None
        elif not configuration.PendingEvents.IsEmpty then
            None
        elif not configuration.Outstanding.IsEmpty then
            Some(BlockedAwaitingEffect configuration)
        else
            Some(Quiescent configuration)

    let step
        (behaviors: Behavior list)
        (scheduler: Scheduler)
        (configuration: Configuration)
        : StepOutcome =
        match terminalOutcome configuration with
        | Some outcome -> Terminal outcome
        | None when not configuration.Enabled.IsEmpty ->
            fireActivation behaviors scheduler configuration
            |> Progressed
        | None ->
            processEvent behaviors scheduler configuration
            |> Progressed

    let settle
        (stepBound: int)
        (behaviors: Behavior list)
        (scheduler: Scheduler)
        (configuration: Configuration)
        : SettleOutcome =
        if stepBound < 1 then
            invalidArg
                (nameof stepBound)
                "step bound must be positive"

        let rec loop steps current =
            match terminalOutcome current with
            | Some outcome -> outcome
            | None when steps >= stepBound ->
                Diverged(
                    current,
                    StepBoundReached stepBound
                )
            | None ->
                match step behaviors scheduler current with
                | Progressed next -> loop (steps + 1) next
                | Terminal outcome -> outcome

        loop 0 configuration

    let private pendingOutcome effectId configuration =
        configuration.PendingEvents
        |> List.exists (fun event ->
            match event.Kind with
            | EffectCommitted(candidate, _)
            | EffectFailed(candidate, _)
            | EffectBecameUnknown candidate ->
                candidate = effectId
            | _ -> false)

    let injectOutcome
        (outcome: EffectOutcome)
        (configuration: Configuration)
        : Result<Configuration, string> =
        let effectId, kind =
            match outcome with
            | Commit(effectId, responseHash) ->
                effectId,
                EffectCommitted(effectId, responseHash)
            | Fail(effectId, reason) ->
                effectId, EffectFailed(effectId, reason)
            | MarkUnknown effectId ->
                effectId, EffectBecameUnknown effectId

        match Map.tryFind effectId configuration.State.Effects with
        | None ->
            Error(
                sprintf
                    "effect %A has not been requested"
                    effectId
            )
        | Some effect when effect.Lifecycle <> Requested ->
            Error(
                sprintf
                    "effect %A has lifecycle %A"
                    effectId
                    effect.Lifecycle
            )
        | Some _ when pendingOutcome effectId configuration ->
            Error(
                sprintf
                    "effect %A already has a queued outcome"
                    effectId
            )
        | Some _ ->
            enqueue kind None None configuration
            |> snd
            |> Ok

    let equivalent
        (observation: Observation)
        (left: Configuration)
        (right: Configuration)
        : bool =
        match observation with
        | ExactTrace -> left.Trace = right.Trace
        | ProjectedState ->
            State.domainProjection left.State = State.domainProjection right.State
        | FactsOnly keys ->
            keys
            |> Set.forall (fun key ->
                Map.tryFind key left.State.Facts = Map.tryFind key right.State.Facts)

    let declaredWriteConflicts
        (behaviors: Behavior list)
        : Set<string> =
        behaviors
        |> List.collect (fun behavior ->
            behavior.Writes
            |> Set.toList
            |> List.map (fun key -> key, behavior.Id))
        |> List.groupBy fst
        |> List.choose (fun (key, writers) ->
            if writers.Length > 1 then Some key else None)
        |> Set.ofList
