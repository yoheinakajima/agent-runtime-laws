namespace AgentRuntimeLaws.Properties

open AgentRuntimeLaws
open FsCheck.Xunit
open Xunit

module KernelProperties =
    [<Property(MaxTest = 250)>]
    let project_is_deterministic_for_a_closed_log (values: int list) =
        let log = TestData.logOfFacts values
        Kernel.project log = Kernel.project log

    [<Fact>]
    let quiescence_on_the_exact_step_bound_is_not_divergence () =
        let initial =
            Kernel.initialize
                (RunId "exact-bound")
                [ FactSet("answer", 42) ]

        match Kernel.settle 1 [] Scheduler.canonical initial with
        | Quiescent configuration ->
            Assert.Equal(42, Map.find "answer" configuration.State.Facts)
        | outcome ->
            failwithf "expected exact-bound quiescence, got %A" outcome

    [<Property(MaxTest = 250)>]
    let evolve_is_idempotent_by_event_identity value =
        let event = TestData.event "idempotence" 1 (FactIncremented("x", value % 1000))
        let once = Kernel.evolve State.empty event
        let twice = Kernel.evolve once event
        once = twice

    [<Property(MaxTest = 150)>]
    let independent_generated_writer_family_converges_under_all_schedulers
        (values: int list)
        =
        let starts _ event =
            match event.Kind with
            | SignalRaised "start" -> true
            | _ -> false

        let behaviors =
            TestData.bounded values
            |> List.truncate 8
            |> List.mapi (fun index value ->
                let key = sprintf "key-%d" index

                { Id = BehaviorId(sprintf "writer-%d" index)
                  Trigger = starts
                  Fire = fun _ _ -> [ Emit(SetFact(key, value)) ]
                  Writes = Set.singleton key })

        let initial =
            Kernel.initialize
                (RunId "generated-independent")
                [ SignalRaised "start" ]

        let projections =
            [ Scheduler.canonical
              Scheduler.eventReverse
              Scheduler.activationReverse
              Scheduler.reverse ]
            |> List.map (fun scheduler ->
                Kernel.settle 200 behaviors scheduler initial
                |> TestData.outcomeConfiguration
                |> _.State
                |> State.domainProjection)

        Kernel.declaredWriteConflicts behaviors = Set.empty
        && match projections with
           | [] -> true
           | first :: rest -> rest |> List.forall ((=) first)

    [<Property(MaxTest = 150)>]
    let disjoint_writers_converge_under_opposite_activation_schedules left right =
        let starts _ event =
            match event.Kind with
            | SignalRaised "start" -> true
            | _ -> false

        let writer name key value =
            { Id = BehaviorId name
              Trigger = starts
              Fire = fun _ _ -> [ Emit(SetFact(key, value % 1000)) ]
              Writes = Set.singleton key }

        let behaviors =
            [ writer "left" "left" left
              writer "right" "right" right ]

        let initial = Kernel.initialize (RunId "disjoint") [ SignalRaised "start" ]

        let canonical =
            Kernel.settle 40 behaviors Scheduler.canonical initial
            |> TestData.outcomeConfiguration

        let reverse =
            Kernel.settle 40 behaviors Scheduler.activationReverse initial
            |> TestData.outcomeConfiguration

        Kernel.declaredWriteConflicts behaviors = Set.empty
        && State.domainProjection canonical.State = State.domainProjection reverse.State

    [<Property(MaxTest = 150)>]
    let generated_conflicting_writers_expose_schedule_dependence value =
        let leftValue = value % 1000
        let rightValue = leftValue + 1

        let starts _ event =
            match event.Kind with
            | SignalRaised "start" -> true
            | _ -> false

        let writer name written =
            { Id = BehaviorId name
              Trigger = starts
              Fire = fun _ _ -> [ Emit(SetFact("winner", written)) ]
              Writes = Set.singleton "winner" }

        let initial =
            Kernel.initialize
                (RunId "generated-conflict")
                [ SignalRaised "start" ]

        let winner scheduler =
            Kernel.settle
                40
                [ writer "left" leftValue
                  writer "right" rightValue ]
                scheduler
                initial
            |> TestData.outcomeConfiguration
            |> _.State.Facts
            |> Map.find "winner"

        winner Scheduler.canonical <> winner Scheduler.activationReverse

    [<Fact>]
    let outstanding_external_request_blocks () =
        let behavior =
            { Id = BehaviorId "requester"
              Trigger =
                fun _ event ->
                    match event.Kind with
                    | SignalRaised "start" -> true
                    | _ -> false
              Fire =
                fun _ _ ->
                    [ Request(
                          TestData.descriptor
                              "email"
                              OneShot
                              Recorded
                      ) ]
              Writes = Set.empty }

        let initial = Kernel.initialize (RunId "blocked") [ SignalRaised "start" ]

        match Kernel.settle 40 [ behavior ] Scheduler.canonical initial with
        | BlockedAwaitingEffect configuration ->
            Assert.Single(configuration.Outstanding) |> ignore
        | outcome ->
            failwithf "expected blocked outcome, got %A" outcome

    [<Fact>]
    let duplicate_effect_request_is_an_integrity_fault () =
        let descriptor = TestData.descriptor "duplicate" Idempotent Recorded

        let state =
            [ TestData.event "duplicate" 1 (EffectRequested descriptor)
              TestData.event "duplicate" 2 (EffectRequested descriptor) ]
            |> Kernel.project

        Assert.Contains(
            DuplicateEffectRequest descriptor.Id,
            state.IntegrityFaults
        )

        Assert.Equal(Requested, state.Effects[descriptor.Id].Lifecycle)

    [<Fact>]
    let conflicting_effect_descriptor_does_not_overwrite_the_first_request () =
        let first = TestData.descriptor "conflict" Idempotent Recorded

        let second =
            { first with
                Name = "different-operation"
                Footprint = OneShot }

        let state =
            [ TestData.event "conflict" 1 (EffectRequested first)
              TestData.event "conflict" 2 (EffectRequested second) ]
            |> Kernel.project

        Assert.Contains(
            ConflictingEffectDescriptor first.Id,
            state.IntegrityFaults
        )

        Assert.Equal(first.Name, state.Effects[first.Id].Name)
        Assert.Equal(first.Footprint, state.Effects[first.Id].Footprint)

    [<Fact>]
    let effect_outcome_without_request_is_an_integrity_fault () =
        let effectId = EffectId "missing-request"

        let state =
            [ TestData.event
                  "missing-request"
                  1
                  (EffectCommitted(effectId, "response")) ]
            |> Kernel.project

        Assert.Contains(
            EffectOutcomeWithoutRequest effectId,
            state.IntegrityFaults
        )

        Assert.False(Map.containsKey effectId state.Effects)

    [<Fact>]
    let terminal_effect_cannot_transition_again () =
        let descriptor = TestData.descriptor "terminal" OneShot Recorded

        let state =
            [ TestData.event "terminal" 1 (EffectRequested descriptor)
              TestData.event
                  "terminal"
                  2
                  (EffectCommitted(descriptor.Id, "first-response"))
              TestData.event
                  "terminal"
                  3
                  (EffectFailed(descriptor.Id, "late-failure")) ]
            |> Kernel.project

        Assert.Contains(
            EffectAlreadyTerminal(descriptor.Id, Committed),
            state.IntegrityFaults
        )

        Assert.Equal(Committed, state.Effects[descriptor.Id].Lifecycle)
        Assert.Equal(Some "first-response", state.Effects[descriptor.Id].ResponseHash)

    [<Fact>]
    let a_second_outcome_cannot_be_queued_for_the_same_request () =
        let descriptor = TestData.descriptor "queued" Idempotent Recorded

        let requester =
            { Id = BehaviorId "requester"
              Trigger =
                fun _ event ->
                    match event.Kind with
                    | SignalRaised "start" -> true
                    | _ -> false
              Fire = fun _ _ -> [ Request descriptor ]
              Writes = Set.empty }

        let blocked =
            Kernel.initialize (RunId "queued") [ SignalRaised "start" ]
            |> Kernel.settle 20 [ requester ] Scheduler.canonical
            |> TestData.outcomeConfiguration

        let queued =
            match Kernel.injectOutcome (Commit(descriptor.Id, "response")) blocked with
            | Ok configuration -> configuration
            | Error message -> failwith message

        match Kernel.injectOutcome (Fail(descriptor.Id, "duplicate")) queued with
        | Error message -> Assert.Contains("already has a queued outcome", message)
        | Ok _ -> failwith "expected the second queued outcome to be rejected"

    [<Fact>]
    let cyclic_trigger_is_reported_as_divergence () =
        let behavior =
            { Id = BehaviorId "loop"
              Trigger =
                fun _ event ->
                    match event.Kind with
                    | SignalRaised "loop" -> true
                    | _ -> false
              Fire = fun _ _ -> [ Emit(RaiseSignal "loop") ]
              Writes = Set.empty }

        let initial = Kernel.initialize (RunId "cycle") [ SignalRaised "loop" ]

        match Kernel.settle 40 [ behavior ] Scheduler.canonical initial with
        | Diverged(_, StepBoundReached 40) -> ()
        | outcome ->
            failwithf "expected bounded divergence, got %A" outcome

    [<Property(MaxTest = 100)>]
    let generated_step_bounds_never_turn_a_self_cycle_into_quiescence
        (rawBound: byte)
        =
        let bound = 1 + (int rawBound % 100)

        let behavior =
            { Id = BehaviorId "generated-loop"
              Trigger =
                fun _ event ->
                    match event.Kind with
                    | SignalRaised "loop" -> true
                    | _ -> false
              Fire = fun _ _ -> [ Emit(RaiseSignal "loop") ]
              Writes = Set.empty }

        let initial =
            Kernel.initialize
                (RunId "generated-cycle")
                [ SignalRaised "loop" ]

        match Kernel.settle bound [ behavior ] Scheduler.canonical initial with
        | Diverged(_, StepBoundReached observed) -> observed = bound
        | Quiescent _
        | BlockedAwaitingEffect _ -> false
