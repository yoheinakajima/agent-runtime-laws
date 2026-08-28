open System
open System.IO
open AgentRuntimeLaws

let configuration = function
    | Quiescent value
    | BlockedAwaitingEffect value
    | Diverged(value, _) -> value

let demo () =
    let starts _ event =
        match event.Kind with
        | SignalRaised "start" -> true
        | _ -> false

    let writer name value =
        { Id = BehaviorId name
          Trigger = starts
          Fire = fun _ _ -> [ Emit(SetFact("winner", value)) ]
          Writes = Set.singleton "winner" }

    let behaviors = [ writer "alpha" 1; writer "beta" 2 ]
    let initial = Kernel.initialize (RunId "demo") [ SignalRaised "start" ]

    let canonical =
        Kernel.settle 50 behaviors Scheduler.canonical initial
        |> configuration

    let reverse =
        Kernel.settle 50 behaviors Scheduler.activationReverse initial
        |> configuration

    printfn "Agent Runtime Laws counterexample"
    printfn "canonical winner: %A" (Map.tryFind "winner" canonical.State.Facts)
    printfn "reverse winner:   %A" (Map.tryFind "winner" reverse.State.Facts)
    printfn
        "same domain projection: %b"
        (State.domainProjection canonical.State = State.domainProjection reverse.State)

    printfn
        "declared write conflicts: %A"
        (Kernel.declaredWriteConflicts behaviors)
    0

let conformance path =
    let results = Conformance.validateFile path

    for result in results do
        if result.Errors.IsEmpty then
            printfn "PASS %s" result.Name
        else
            printfn "FAIL %s" result.Name

            for error in result.Errors do
                printfn "  %s" error

    if results |> List.forall (fun result -> result.Errors.IsEmpty) then 0 else 1

let validate profile path =
    let summary = Validation.summarize (Validation.parseProfile profile) path
    printfn "source: %s" summary.Source
    printfn "events: %d" summary.InputEvents
    printfn "grade: %A (verified=%b)" summary.Grade.Grade summary.Grade.Verified
    printfn "projection cuts: %A" summary.ProjectionCuts
    printfn "external-continuation cuts: %A" summary.ExternalContinuationCuts
    printfn "counterfactual cuts: %A" summary.CounterfactualCuts
    printfn "counterfactual-unsound cuts: %A" summary.CounterfactualUnsoundCuts
    printfn "unclassified source types: %A" summary.UnclassifiedTypes
    0

let validateDirectory profile path =
    let paths =
        Directory.GetFiles(path, "*.jsonl")
        |> Array.sort
        |> Array.toList

    if paths.IsEmpty then
        invalidArg
            (nameof path)
            "directory contains no JSONL logs"

    let summary =
        Validation.summarizeMany
            (Validation.parseProfile profile)
            paths

    printfn "runs: %d" summary.Runs
    printfn "events: %d -> %d" summary.InputEvents summary.NormalizedEvents
    printfn "grade distribution: %A" summary.GradeDistribution
    printfn "verified runs: %d" summary.VerifiedRuns
    printfn "projection cuts: %A" summary.ProjectionCuts
    printfn "external-continuation cuts: %A" summary.ExternalContinuationCuts
    printfn "counterfactual cuts: %A" summary.CounterfactualCuts
    printfn
        "runs with counterfactual-unsound cuts: %d"
        summary.RunsWithCounterfactualUnsoundCuts
    printfn "unclassified source types: %A" summary.UnclassifiedTypes
    0

let manifest path =
    let summaries = Validation.validateManifest path

    for summary in summaries do
        printfn
            "%s: events=%d grade=%A counterfactual-unsafe=%d"
            summary.Source
            summary.InputEvents
            summary.Grade.Grade
            summary.CounterfactualUnsoundCuts.Length

    0

[<EntryPoint>]
let main argv =
    try
        match Array.toList argv with
        | [ "demo" ] -> demo ()
        | [ "conformance"; path ] -> conformance path
        | [ "validate"; profile; path ] -> validate profile path
        | [ "validate-directory"; profile; path ] ->
            validateDirectory profile path
        | [ "manifest"; path ] -> manifest path
        | _ ->
            eprintfn "usage:"
            eprintfn "  dotnet run --project apps/AgentRuntimeLaws.Cli -- demo"
            eprintfn "  dotnet run --project apps/AgentRuntimeLaws.Cli -- conformance FILE"
            eprintfn "  dotnet run --project apps/AgentRuntimeLaws.Cli -- validate activegraph|bridge|generic FILE"
            eprintfn "  dotnet run --project apps/AgentRuntimeLaws.Cli -- validate-directory activegraph|bridge|generic DIR"
            eprintfn "  dotnet run --project apps/AgentRuntimeLaws.Cli -- manifest FILE"
            2
    with ex ->
        eprintfn "%s" ex.Message
        1
