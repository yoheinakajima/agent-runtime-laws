namespace AgentRuntimeLaws

open System
open System.IO
open System.Security.Cryptography
open System.Text.Json

type SourceProfile =
    | ActiveGraph
    | ActiveGraphBridge
    | GenericJsonl

type VerdictCounts =
    { Sound: int
      Conditional: int
      Unsound: int }

type ValidationSummary =
    { Source: string
      InputEvents: int
      NormalizedEvents: int
      Grade: GradeReport
      ProjectionCuts: VerdictCounts
      ExternalContinuationCuts: VerdictCounts
      CounterfactualCuts: VerdictCounts
      CounterfactualUnsoundCuts: int list
      UnclassifiedTypes: string list }

[<CLIMutable>]
type EvidenceManifestEntry =
    { Path: string
      Sha256: string
      Profile: string
      FixtureKind: string
      SourceRepository: string
      SourceRevision: string
      SourcePath: string
      Transformation: string
      PublicOrSanitized: bool }

[<CLIMutable>]
type EvidenceManifest =
    { SchemaVersion: int
      Entries: EvidenceManifestEntry array }

module Validation =
    type private RawEvent =
        { Id: string
          Type: string
          CausedBy: string option
          Payload: JsonElement }

    let private property
        (name: string)
        (element: JsonElement)
        =
        let mutable value =
            Unchecked.defaultof<JsonElement>

        if
            element.ValueKind = JsonValueKind.Object
            && element.TryGetProperty(name, &value)
        then
            Some value
        else
            None

    let private stringProperty
        (name: string)
        (element: JsonElement)
        =
        property name element
        |> Option.bind (fun value ->
            if value.ValueKind = JsonValueKind.String then
                value.GetString() |> Option.ofObj
            else
                None)

    let private boolProperty
        (name: string)
        (element: JsonElement)
        =
        property name element
        |> Option.bind (fun value ->
            if value.ValueKind = JsonValueKind.True then
                Some true
            elif value.ValueKind = JsonValueKind.False then
                Some false
            else
                None)

    let private parseLine
        (index: int)
        (line: string)
        : RawEvent =
        use document = JsonDocument.Parse(line)
        let root = document.RootElement

        let payload =
            match property "payload" root with
            | Some value -> value.Clone()
            | None ->
                use empty = JsonDocument.Parse("{}")
                empty.RootElement.Clone()

        { Id =
            stringProperty "id" root
            |> Option.defaultValue (
                sprintf "source-event-%06d" index
            )
          Type =
            stringProperty "type" root
            |> Option.defaultValue "unknown"
          CausedBy =
            stringProperty "caused_by" root
            |> Option.orElseWith (fun () ->
                stringProperty "causedBy" root)
          Payload = payload }

    let private readRaw
        (path: string)
        : RawEvent list =
        File.ReadLines(path)
        |> Seq.filter (
            String.IsNullOrWhiteSpace >> not
        )
        |> Seq.mapi (fun index line ->
            parseLine (index + 1) line)
        |> Seq.toList

    let private isRequest
        (eventType: string)
        : bool =
        eventType.EndsWith(
            ".requested",
            StringComparison.Ordinal
        )

    let private responseHash
        (raw: RawEvent)
        : string option =
        stringProperty "response_hash" raw.Payload
        |> Option.orElseWith (fun () ->
            stringProperty "output_hash" raw.Payload)
        |> Option.filter (
            String.IsNullOrWhiteSpace >> not
        )

    let private isResponse
        (raw: RawEvent)
        : bool =
        raw.Type.EndsWith(
            ".responded",
            StringComparison.Ordinal
        )
        || String.Equals(
            raw.Type,
            "effect.completed",
            StringComparison.Ordinal
        )

    let private requestName
        (raw: RawEvent)
        : string =
        stringProperty "name" raw.Payload
        |> Option.orElseWith (fun () ->
            stringProperty "tool" raw.Payload)
        |> Option.orElseWith (fun () ->
            stringProperty "behavior" raw.Payload)
        |> Option.defaultValue raw.Type

    let private footprint
        (raw: RawEvent)
        : Footprint =
        let sideEffect =
            stringProperty "side_effect" raw.Payload

        let hasIdempotency =
            stringProperty "idempotency_key" raw.Payload
            |> Option.exists (
                String.IsNullOrWhiteSpace >> not
            )

        match sideEffect, hasIdempotency with
        | Some "pure", _ -> Pure
        | Some "read", _ -> Idempotent
        | Some "write", true -> Idempotent
        | Some "compensatable", _ -> Compensatable
        | Some "one-shot", _ -> OneShot
        | Some "write", false -> OneShot
        | _ -> UnknownFootprint

    let private requestHash
        (raw: RawEvent)
        : string =
        stringProperty "request_hash" raw.Payload
        |> Option.orElseWith (fun () ->
            stringProperty "args_hash" raw.Payload)
        |> Option.orElseWith (fun () ->
            stringProperty "prompt_hash" raw.Payload)
        |> Option.defaultValue ""

    let private normalizedType
        (raw: RawEvent)
        =
        raw.Type
            .Replace(".", "_")
            .Replace("-", "_")
            .ToLowerInvariant()

    let private evidenceFor
        (_profile: SourceProfile)
        (raw: RawEvent)
        : EvidenceFact option =
        let value = normalizedType raw

        if
            value.Contains("run_started")
            || value.Contains("invocation_started")
            || value.Contains("envelope_captured")
        then
            Some EnvelopeCaptured
        elif value.Contains("invocation_completed") then
            Some InvocationCompleted
        elif value.Contains("boundary_mediated") then
            Some BoundaryMediated
        elif value.Contains("clean_reconstruction") then
            Some CleanReconstructionAvailable
        elif value.Contains("checkpoint") then
            Some CheckpointRecorded
        elif value.Contains("native_runtime") then
            Some NativeRuntime
        elif value.Contains("verification") then
            match boolProperty "ok" raw.Payload with
            | Some true -> Some VerificationPassed
            | Some false ->
                Some(
                    HazardDetected(
                        sprintf
                            "verification failed: %s"
                            raw.Type
                    )
                )
            | None ->
                Some(
                    HazardDetected(
                        sprintf
                            "verification missing ok: %s"
                            raw.Type
                    )
                )
        elif value.Contains("hazard") then
            Some(HazardDetected raw.Type)
        else
            None

    let private normalizeRaw
        (profile: SourceProfile)
        (raw: RawEvent list)
        : Event list * string list =
        let capturedResponses =
            raw
            |> List.choose (fun item ->
                match
                    isResponse item,
                    item.CausedBy,
                    responseHash item
                with
                | true, Some requestId, Some hash ->
                    Some(requestId, hash)
                | _ -> None)
            |> Map.ofList

        let runId = RunId "normalized-source"
        let unclassified = ResizeArray<string>()

        let events =
            raw
            |> List.mapi (fun index item ->
                let eventKind =
                    if isRequest item.Type then
                        let replaySource =
                            if
                                Map.containsKey
                                    item.Id
                                    capturedResponses
                            then
                                Recorded
                            else
                                Uncaptured

                        EffectRequested
                            { Id = EffectId item.Id
                              Name = requestName item
                              Footprint = footprint item
                              ReplaySource = replaySource
                              Lifecycle = Requested
                              RequestHash = requestHash item
                              ResponseHash = None }
                    elif
                        isResponse item
                        && item.CausedBy.IsSome
                    then
                        EffectCommitted(
                            EffectId item.CausedBy.Value,
                            responseHash item
                            |> Option.defaultValue ""
                        )
                    else
                        match evidenceFor profile item with
                        | Some evidence ->
                            EvidenceRecorded evidence
                        | None ->
                            unclassified.Add item.Type
                            SignalRaised(
                                sprintf
                                    "source:%s"
                                    item.Type
                            )

                { Id = EventId item.Id
                  RunId = runId
                  Sequence = int64 (index + 1)
                  Kind = eventKind
                  CausedBy =
                    item.CausedBy
                    |> Option.map EventId
                  EmittedBy = None })

        events,
        (unclassified
         |> Seq.distinct
         |> Seq.sort
         |> Seq.toList)

    let normalize
        (profile: SourceProfile)
        (path: string)
        : Event list * string list =
        readRaw path |> normalizeRaw profile

    let private countVerdicts
        (assessments: ForkAssessment list)
        : VerdictCounts =
        { Sound =
            assessments
            |> List.filter (fun item ->
                item.Verdict = Sound)
            |> List.length
          Conditional =
            assessments
            |> List.filter (fun item ->
                item.Verdict = Conditional)
            |> List.length
          Unsound =
            assessments
            |> List.filter (fun item ->
                item.Verdict = Unsound)
            |> List.length }

    let summarize
        (profile: SourceProfile)
        (path: string)
        : ValidationSummary =
        let raw = readRaw path
        let log, unclassified = normalizeRaw profile raw
        let cuts = [ 0 .. log.Length ]

        let assessments property =
            cuts
            |> List.map (fun cut ->
                Forks.assess property cut log)

        let projection =
            assessments ProjectionReplay

        let continuation =
            assessments ExternalContinuation

        let counterfactual =
            assessments CounterfactualWorld

        let counterfactualUnsound =
            counterfactual
            |> List.choose (fun item ->
                if item.Verdict = Unsound then
                    Some item.Cut
                else
                    None)

        { Source = path
          InputEvents = raw.Length
          NormalizedEvents = log.Length
          Grade = Grades.grade log
          ProjectionCuts = countVerdicts projection
          ExternalContinuationCuts =
            countVerdicts continuation
          CounterfactualCuts =
            countVerdicts counterfactual
          CounterfactualUnsoundCuts =
            counterfactualUnsound
          UnclassifiedTypes = unclassified }

    let parseProfile
        (value: string)
        : SourceProfile =
        match value with
        | "activegraph" -> ActiveGraph
        | "bridge" -> ActiveGraphBridge
        | "generic" -> GenericJsonl
        | _ ->
            invalidArg
                "profile"
                (sprintf
                    "unknown source profile %s"
                    value)

    let sha256
        (path: string)
        : string =
        use stream = File.OpenRead(path)
        use hash = SHA256.Create()

        hash.ComputeHash(stream)
        |> Convert.ToHexString
        |> _.ToLowerInvariant()

    let loadManifest
        (path: string)
        : EvidenceManifest =
        let options =
            JsonSerializerOptions(
                PropertyNameCaseInsensitive = true
            )

        let json = File.ReadAllText(path)

        let manifest =
            JsonSerializer.Deserialize<EvidenceManifest>(
                json,
                options
            )

        if isNull (box manifest) then
            raise (
                InvalidDataException(
                    "empty evidence manifest"
                )
            )
        else
            manifest

    let private requireReceipt
        (entry: EvidenceManifestEntry)
        =
        let required =
            [ "fixture kind", entry.FixtureKind
              "source repository", entry.SourceRepository
              "source revision", entry.SourceRevision
              "source path", entry.SourcePath
              "transformation", entry.Transformation ]

        required
        |> List.iter (fun (name, value) ->
            if String.IsNullOrWhiteSpace value then
                raise (
                    InvalidDataException(
                        sprintf
                            "missing %s receipt for %s"
                            name
                            entry.Path
                    )
                ))

    let validateManifest
        (manifestPath: string)
        : ValidationSummary list =
        let manifest = loadManifest manifestPath

        if manifest.SchemaVersion <> 1 then
            raise (
                InvalidDataException(
                    sprintf
                        "unsupported evidence manifest schema %d"
                        manifest.SchemaVersion
                )
            )

        let root =
            Path.GetDirectoryName(
                Path.GetFullPath manifestPath
            )

        manifest.Entries
        |> Option.ofObj
        |> Option.defaultValue Array.empty
        |> Array.map (fun entry ->
            requireReceipt entry

            if not entry.PublicOrSanitized then
                raise (
                    InvalidDataException(
                        sprintf
                            "refusing non-sanitized evidence %s"
                            entry.Path
                    )
                )

            let path =
                Path.GetFullPath(entry.Path, root)

            let actual = sha256 path

            if
                not (
                    String.Equals(
                        actual,
                        entry.Sha256,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
            then
                raise (
                    InvalidDataException(
                        sprintf
                            "evidence hash mismatch for %s: expected %s, got %s"
                            entry.Path
                            entry.Sha256
                            actual
                    )
                )

            summarize
                (parseProfile entry.Profile)
                path)
        |> Array.toList
