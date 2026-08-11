namespace AgentRuntimeLaws.Properties

open AgentRuntimeLaws
open FsCheck.Xunit
open Xunit

module KernelProperties =
    [<Property(MaxTest = 250)>]
    let project_is_deterministic_for_a_closed_log (values: int list) =
        let log = TestData.logOfFacts values
        Kernel.project log = Kernel.project log

    [<Property(MaxTest = 250)>]
    let evolve_is_idempotent_by_event_identity value =
        let event = TestData.event "idempotence" 1 (FactIncremented("x", value % 1000))
        let once = Kernel.evolve State.empty event
        let twice = Kernel.evolve once event
        once = twice

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
