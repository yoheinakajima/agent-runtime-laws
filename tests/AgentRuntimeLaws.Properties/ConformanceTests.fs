namespace AgentRuntimeLaws.Properties

open System
open System.IO
open AgentRuntimeLaws
open Xunit

module ConformanceTests =
    let private vectorPath =
        Path.Combine(
            AppContext.BaseDirectory,
            "conformance",
            "vectors",
            "v1.json"
        )

    [<Fact>]
    let language_neutral_v1_vectors_all_conform () =
        let results = Conformance.validateFile vectorPath

        let failures =
            results
            |> List.collect (fun result ->
                result.Errors
                |> List.map (fun error ->
                    sprintf "%s: %s" result.Name error))

        Assert.NotEmpty(results)

        if not failures.IsEmpty then
            failwith (String.concat Environment.NewLine failures)
