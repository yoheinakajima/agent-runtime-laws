namespace AgentRuntimeLaws

open System
open System.IO
open System.Text.Json

[<CLIMutable>]
type VectorFact =
    { Key: string
      Value: int }

[<CLIMutable>]
type VectorEvent =
    { Kind: string
      Key: string
      Value: int
      EffectId: string
      Name: string
      Footprint: string
      ReplaySource: string
      RequestHash: string
      ResponseHash: string
      Evidence: string }

[<CLIMutable>]
type VectorCase =
    { Name: string
      Events: VectorEvent array
      Cut: int
      Property: string
      ExpectedVerdict: string
      ExpectedGrade: string
      ExpectedFacts: VectorFact array }

[<CLIMutable>]
type VectorFile =
    { SchemaVersion: int
      Cases: VectorCase array }

type CaseValidation =
    { Name: string
      Errors: string list }

module Conformance =
    let private parseFootprint (value: string) =
        match value with
        | "pure" -> Pure
        | "idempotent" -> Idempotent
        | "compensatable" -> Compensatable
        | "one-shot" -> OneShot
        | value -> invalidArg "footprint" (sprintf "unknown footprint %s" value)

    let private parseReplaySource (value: string) =
        match value with
        | "deterministic" -> Deterministic
        | "recorded" -> Recorded
        | "uncaptured" -> Uncaptured
        | value -> invalidArg "replaySource" (sprintf "unknown replay source %s" value)

    let private parseEvidence (value: string) =
        match value with
        | "envelope-captured" -> EnvelopeCaptured
        | "invocation-completed" -> InvocationCompleted
        | "boundary-mediated" -> BoundaryMediated
        | "clean-reconstruction" -> CleanReconstructionAvailable
        | "verification-passed" -> VerificationPassed
        | "checkpoint-recorded" -> CheckpointRecorded
        | "native-runtime" -> NativeRuntime
        | "lossy-envelope" -> LossyEnvelope
        | value when value.StartsWith("hazard:") ->
            HazardDetected(value.Substring("hazard:".Length))
        | value when value.StartsWith("unmediated:") ->
            UnmediatedEffect(value.Substring("unmediated:".Length))
        | value -> invalidArg "evidence" (sprintf "unknown evidence %s" value)

    let private parseProperty (value: string) =
        match value with
        | "projection" -> ProjectionReplay
        | "strict-replay" -> StrictExecutionReplay
        | "external-continuation" -> ExternalContinuation
        | "counterfactual-world" -> CounterfactualWorld
        | value -> invalidArg "property" (sprintf "unknown fork property %s" value)

    let private gradeName (grade: ReplayGrade) =
        match grade with
        | Observed -> "observed"
        | Envelope -> "envelope"
        | Boundary -> "boundary"
        | Checkpointed -> "checkpointed"
        | Native -> "native"

    let private verdictName (verdict: ForkVerdict) =
        match verdict with
        | Sound -> "sound"
        | Conditional -> "conditional"
        | Unsound -> "unsound"

    let private text (value: string) =
        if isNull value then "" else value

    let private descriptor (vector: VectorEvent) : EffectDescriptor =
        { Id = EffectId(text vector.EffectId)
          Name = text vector.Name
          Footprint = parseFootprint (text vector.Footprint)
          ReplaySource = parseReplaySource (text vector.ReplaySource)
          Lifecycle = Requested
          RequestHash = text vector.RequestHash
          ResponseHash = None }

    let private kind (vector: VectorEvent) : EventKind =
        match text vector.Kind with
        | "fact.set" -> FactSet(text vector.Key, vector.Value)
        | "fact.increment" -> FactIncremented(text vector.Key, vector.Value)
        | "signal" -> SignalRaised(text vector.Key)
        | "effect.requested" -> EffectRequested(descriptor vector)
        | "effect.committed" ->
            EffectCommitted(EffectId(text vector.EffectId), text vector.ResponseHash)
        | "effect.failed" ->
            EffectFailed(EffectId(text vector.EffectId), text vector.ResponseHash)
        | "effect.unknown" ->
            EffectBecameUnknown(EffectId(text vector.EffectId))
        | "evidence" -> EvidenceRecorded(parseEvidence (text vector.Evidence))
        | value -> invalidArg "kind" (sprintf "unknown vector event kind %s" value)

    let toLog (case: VectorCase) : Event list =
        let runId = RunId(sprintf "vector:%s" case.Name)

        (case.Events |> Option.ofObj |> Option.defaultValue Array.empty)
        |> Array.toList
        |> List.mapi (fun index vector ->
            { Id = EventId(sprintf "%s:event:%06d" case.Name (index + 1))
              RunId = runId
              Sequence = int64 (index + 1)
              Kind = kind vector
              CausedBy = None
              EmittedBy = None })

    let load (path: string) : VectorFile =
        let options = JsonSerializerOptions(PropertyNameCaseInsensitive = true)
        let json = File.ReadAllText(path)

        let file =
            JsonSerializer.Deserialize<VectorFile>(json, options)

        if isNull (box file) then
            raise (InvalidDataException("empty conformance file"))
        else
            file

    let validateCase (case: VectorCase) : CaseValidation =
        let errors = ResizeArray<string>()

        try
            let log = toLog case
            let state = Kernel.project log

            for expected in
                case.ExpectedFacts
                |> Option.ofObj
                |> Option.defaultValue Array.empty do
                let actual = Map.tryFind expected.Key state.Facts

                if actual <> Some expected.Value then
                    errors.Add(
                        sprintf
                            "fact %s expected %d but was %A"
                            expected.Key
                            expected.Value
                            actual
                    )

            if not (String.IsNullOrWhiteSpace case.ExpectedVerdict) then
                let actual =
                    Forks.assess (parseProperty case.Property) case.Cut log
                    |> _.Verdict
                    |> verdictName

                if actual <> case.ExpectedVerdict then
                    errors.Add(
                        sprintf
                            "fork verdict expected %s but was %s"
                            case.ExpectedVerdict
                            actual
                    )

            if not (String.IsNullOrWhiteSpace case.ExpectedGrade) then
                let actual = Grades.grade log |> _.Grade |> gradeName

                if actual <> case.ExpectedGrade then
                    errors.Add(
                        sprintf
                            "grade expected %s but was %s"
                            case.ExpectedGrade
                            actual
                    )
        with ex ->
            errors.Add(ex.Message)

        { Name = case.Name
          Errors = List.ofSeq errors }

    let validate (file: VectorFile) : CaseValidation list =
        if file.SchemaVersion <> 1 then
            raise (
                InvalidDataException(
                    sprintf
                        "unsupported conformance schema %d; expected 1"
                        file.SchemaVersion
                )
            )

        file.Cases
        |> Option.ofObj
        |> Option.defaultValue Array.empty
        |> Array.map validateCase
        |> Array.toList

    let validateFile (path: string) : CaseValidation list =
        load path |> validate
