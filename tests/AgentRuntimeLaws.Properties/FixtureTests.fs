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
