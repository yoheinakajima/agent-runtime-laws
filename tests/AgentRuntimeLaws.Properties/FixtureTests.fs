namespace AgentRuntimeLaws.Properties

open System
open System.IO
open System.Text.Json
open AgentRuntimeLaws
open Xunit

module FixtureTests =
    let private path (parts: string list) : string =
        parts
        |> List.fold (fun current part -> Path.Combine(current, part)) AppContext.BaseDirectory

    let private readFixture (name: string) : JsonElement =
        let fixturePath = path [ "tests"; "fixtures"; name ]
        use document = JsonDocument.Parse(File.ReadAllText fixturePath)
        document.RootElement.Clone()

    let private property (name: string) (element: JsonElement) : JsonElement =
        element.GetProperty(name)

    let private winner (scheduler: Scheduler) : int =
        let starts (_: State) (event: Event) =
            match event.Kind with
            | SignalRaised "start" -> true
            | _ -> false

        let writer (name: string) (value: int) : Behavior =
            { Id = BehaviorId name
              Trigger = starts
              Fire = fun _ _ -> [ Emit(SetFact("winner", value)) ]
              Writes = Set.singleton "winner" }

        let initial =
            Kernel.initialize
                (RunId "write-conflict")
                [ SignalRaised "start" ]

        match
            Kernel.settle
                50
                [ writer "alpha" 1; writer "beta" 2 ]
                scheduler
                initial
        with
        | Quiescent configuration ->
            Map.find "winner" configuration.State.Facts
        | outcome ->
            failwithf "expected quiescence, got %A" outcome

    [<Fact>]
    let preserved_write_conflict_depends_on_activation_order () =
        let root = readFixture "counterexamples.json"
        let expected = property "writeConflict" root
        let canonical = winner Scheduler.canonical
        let reverse = winner Scheduler.activationReverse

        Assert.Equal(
            (property "canonicalWinner" expected).GetInt32(),
            canonical
        )

        Assert.Equal(
            (property "reverseWinner" expected).GetInt32(),
            reverse
        )

        Assert.NotEqual(canonical, reverse)

    [<Fact>]
    let disjoint_declared_writes_do_not_prevent_read_trigger_interference () =
        let starts _ event =
            match event.Kind with
            | SignalRaised "start" -> true
            | _ -> false

        let signal name emitted =
            { Id = BehaviorId name
              Trigger = starts
              Fire = fun _ _ -> [ Emit(RaiseSignal emitted) ]
              Writes = Set.empty }

        let observer =
            { Id = BehaviorId "observer"
              Trigger =
                fun _ event ->
                    match event.Kind with
                    | SignalRaised "left-ready" -> true
                    | _ -> false
              Fire =
                fun state _ ->
                    let value =
                        if Set.contains "right-ready" state.Signals then 1 else 0

                    [ Emit(SetFact("observed-right", value)) ]
              Writes = Set.singleton "observed-right" }

        let behaviors =
            [ signal "left" "left-ready"
              signal "right" "right-ready"
              observer ]

        let result scheduler =
            Kernel.initialize
                (RunId "read-trigger-interference")
                [ SignalRaised "start" ]
            |> Kernel.settle 50 behaviors scheduler
            |> TestData.outcomeConfiguration
            |> _.State.Facts
            |> Map.find "observed-right"

        let root = readFixture "counterexamples.json"
        let expected = property "readTriggerInterference" root
        let canonical = result Scheduler.canonical
        let reversed = result Scheduler.eventReverse

        Assert.Empty(Kernel.declaredWriteConflicts behaviors)
        Assert.Equal((property "canonicalValue" expected).GetInt32(), canonical)
        Assert.Equal((property "eventReverseValue" expected).GetInt32(), reversed)
        Assert.NotEqual(canonical, reversed)

    [<Fact>]
    let preserved_cycle_is_reported_as_divergence_not_quiescence () =
        let root = readFixture "counterexamples.json"
        let expected =
            (property "expectedOutcome" (property "cycle" root)).GetString()

        let behavior =
            { Id = BehaviorId "loop"
              Trigger =
                fun _ event ->
                    match event.Kind with
                    | SignalRaised "loop" -> true
                    | _ -> false
              Fire = fun _ _ -> [ Emit(RaiseSignal "loop") ]
              Writes = Set.empty }

        let initial =
            Kernel.initialize
                (RunId "cycle-fixture")
                [ SignalRaised "loop" ]

        match Kernel.settle 30 [ behavior ] Scheduler.canonical initial with
        | Diverged(_, StepBoundReached 30) ->
            Assert.Equal("step-bound-reached", expected)
        | outcome ->
            failwithf "expected cycle divergence, got %A" outcome

    [<Fact>]
    let preserved_one_shot_suffix_is_not_a_counterfactual_world () =
        let root = readFixture "counterexamples.json"
        let expected =
            (property "findingCode" (property "discardedOneShot" root)).GetString()

        let assessment =
            TestData.effectLog OneShot Recorded
            |> Forks.assess CounterfactualWorld 0

        Assert.Equal(Unsound, assessment.Verdict)

        Assert.Contains(
            assessment.Findings,
            fun finding -> finding.Code = expected
        )

    [<Fact>]
    let sanitized_evidence_manifest_is_hash_bound_and_replayable () =
        let manifestPath = path [ "evidence"; "manifest.json" ]
        let summaries = Validation.validateManifest manifestPath

        Assert.Equal(2, summaries.Length)

        let grades =
            summaries
            |> List.map (fun summary -> summary.Grade.Grade)
            |> Set.ofList

        Assert.Contains(Boundary, grades)
        Assert.Contains(Native, grades)

        Assert.All(
            summaries,
            fun (summary: ValidationSummary) ->
                Assert.True(summary.InputEvents > 0)
                Assert.Equal(summary.InputEvents, summary.NormalizedEvents)
                Assert.True(summary.ProjectionCuts.Sound > 0)
        )

    [<Fact>]
    let normalization_fails_closed_when_source_evidence_is_missing () =
        let fixturePath = path [ "tests"; "fixtures"; "fail-closed.jsonl" ]
        let log, unclassified = Validation.normalize ActiveGraph fixturePath
        let state = Kernel.project log
        let effect = state.Effects[EffectId "unknown-effect"]
        let report = Grades.grade log

        Assert.Equal(UnknownFootprint, effect.Footprint)
        Assert.Equal(Uncaptured, effect.ReplaySource)
        Assert.Contains("runtime.mystery", unclassified)
        Assert.Contains("runtime.verification.shadow", unclassified)
        Assert.DoesNotContain(NativeRuntime, state.Evidence)
        Assert.DoesNotContain(VerificationPassed, state.Evidence)

        Assert.Contains(
            state.Evidence,
            fun fact ->
                match fact with
                | HazardDetected detail -> detail.Contains("missing ok")
                | _ -> false
        )

        Assert.Equal(Observed, report.Grade)
        Assert.Contains(report.Blockers, fun item -> item.Contains("unknown footprint"))
        Assert.Contains(report.Blockers, fun item -> item.Contains("uncaptured effect"))

    [<Fact>]
    let activegraph_oracle_output_is_recorded_without_misclassifying_domain_requests () =
        let fixturePath =
            path [ "tests"; "fixtures"; "activegraph-recorded.jsonl" ]

        let log, unclassified = Validation.normalize ActiveGraph fixturePath
        let state = Kernel.project log
        let effect = state.Effects[EffectId "llm-request"]
        let assessment =
            Forks.assess ExternalContinuation log.Length log

        Assert.Equal(1, state.Effects.Count)
        Assert.Equal(OneShot, effect.Footprint)
        Assert.Equal(Recorded, effect.ReplaySource)
        Assert.Equal(Committed, effect.Lifecycle)
        Assert.StartsWith("derived-sha256:", effect.ResponseHash.Value)
        Assert.Contains("round.requested", unclassified)
        Assert.Equal(Conditional, assessment.Verdict)
