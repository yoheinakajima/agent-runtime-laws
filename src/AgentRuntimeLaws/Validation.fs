namespace AgentRuntimeLaws

open System
open System.IO
open System.Security.Cryptography
open System.Text
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

type BatchValidationSummary =
    { Runs: int
      InputEvents: int
      NormalizedEvents: int
      GradeDistribution: Map<ReplayGrade, int>
      VerifiedRuns: int
      ProjectionCuts: VerdictCounts
      ExternalContinuationCuts: VerdictCounts
      CounterfactualCuts: VerdictCounts
      RunsWithCounterfactualUnsoundCuts: int
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

    let private intProperty
        (name: string)
        (element: JsonElement)
        =
        property name element
        |> Option.bind (fun value ->
            if value.ValueKind = JsonValueKind.Number then
                match value.TryGetInt32() with
                | true, number -> Some number
                | false, _ -> None
            else
                None)

    let private nullProperty
        (name: string)
        (element: JsonElement)
        =
        property name element
        |> Option.exists (fun value ->
            value.ValueKind = JsonValueKind.Null)

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
        [ "effect.requested"
          "model.requested"
          "tool.requested"
          "llm.requested"
          "embedding.requested"
          "retrieval.requested"
          "external.requested" ]
        |> List.contains (eventType.ToLowerInvariant())

    let private hashText (value: string) =
        value
        |> Encoding.UTF8.GetBytes
        |> SHA256.HashData
        |> Convert.ToHexString
        |> _.ToLowerInvariant()
        |> sprintf "derived-sha256:%s"

    let private responseHash
        (raw: RawEvent)
        : string option =
        stringProperty "response_hash" raw.Payload
        |> Option.orElseWith (fun () ->
            stringProperty "output_hash" raw.Payload)
        |> Option.orElseWith (fun () ->
            stringProperty "raw_text" raw.Payload
            |> Option.filter (
                String.IsNullOrWhiteSpace >> not
            )
            |> Option.map hashText)
        |> Option.filter (
            String.IsNullOrWhiteSpace >> not
        )

    let private isResponse
        (raw: RawEvent)
        : bool =
        [ "effect.responded"
          "model.responded"
          "tool.responded"
          "llm.responded"
          "embedding.responded"
          "retrieval.responded"
          "external.responded"
          "effect.completed" ]
        |> List.contains (raw.Type.ToLowerInvariant())

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
        | None, _
            when [ "llm.requested"
                   "model.requested"
                   "embedding.requested" ]
                 |> List.contains (raw.Type.ToLowerInvariant()) ->
            OneShot
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
        (profile: SourceProfile)
        (raw: RawEvent)
        : EvidenceFact list =
        let value = normalizedType raw

        let common =
            match value with
            | "run_started"
            | "bridge_run_started"
            | "invocation_started"
            | "bridge_invocation_started"
            | "runtime_invocation_started"
            | "envelope_captured"
            | "runtime_envelope_captured" ->
                [ EnvelopeCaptured ]
            | "invocation_completed"
            | "bridge_invocation_completed"
            | "runtime_invocation_completed" ->
                [ InvocationCompleted ]
            | "boundary_mediated"
            | "bridge_boundary_mediated"
            | "runtime_boundary_mediated" ->
                [ BoundaryMediated ]
            | "clean_reconstruction"
            | "bridge_clean_reconstruction"
            | "runtime_clean_reconstruction" ->
                [ CleanReconstructionAvailable ]
            | "checkpoint_recorded"
            | "bridge_checkpoint_recorded"
            | "runtime_checkpoint_recorded" ->
                [ CheckpointRecorded ]
            | "native_runtime"
            | "runtime_native_runtime" ->
                [ NativeRuntime ]
            | "verification"
            | "bridge_verification"
            | "runtime_verification" ->
                match boolProperty "ok" raw.Payload with
                | Some true -> [ VerificationPassed ]
                | Some false ->
                    [ HazardDetected(
                          sprintf
                              "verification failed: %s"
                              raw.Type
                      ) ]
                | None ->
                    [ HazardDetected(
                          sprintf
                              "verification missing ok: %s"
                              raw.Type
                      ) ]
            | "hazard_detected"
            | "bridge_hazard_detected"
            | "runtime_hazard_detected" ->
                [ HazardDetected raw.Type ]
            | _ ->
                []

        let bridgeDerived =
            match profile, value with
            | ActiveGraphBridge, "bridge_run_started"
                when stringProperty "reconstruction" raw.Payload
                        = Some "fresh_factory" ->
                [ CleanReconstructionAvailable ]
            | ActiveGraphBridge, "bridge_verification"
                when boolProperty "ok" raw.Payload = Some true
                     && (intProperty "effects_served" raw.Payload
                         |> Option.isSome)
                     && nullProperty "divergence" raw.Payload ->
                [ BoundaryMediated ]
            | _ -> []

        common @ bridgeDerived |> List.distinct

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

        let classified =
            raw
            |> List.collect (fun item ->
                let eventKinds =
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

                        [ EffectRequested
                              { Id = EffectId item.Id
                                Name = requestName item
                                Footprint = footprint item
                                ReplaySource = replaySource
                                Lifecycle = Requested
                                RequestHash = requestHash item
                                ResponseHash = None } ]
                    elif
                        isResponse item
                        && item.CausedBy.IsSome
                    then
                        [ EffectCommitted(
                              EffectId item.CausedBy.Value,
                              responseHash item
                              |> Option.defaultValue ""
                          ) ]
                    else
                        match evidenceFor profile item with
                        | [] ->
                            unclassified.Add item.Type
                            [ SignalRaised(
                                  sprintf
                                      "source:%s"
                                      item.Type
                              ) ]
                        | evidence ->
                            evidence
                            |> List.map EvidenceRecorded

                eventKinds
                |> List.mapi (fun derivedIndex kind ->
                    item, derivedIndex, kind))

        let events =
            classified
            |> List.mapi (fun index (item, derivedIndex, eventKind) ->
                let eventId =
                    if derivedIndex = 0 then
                        item.Id
                    else
                        sprintf
                            "%s:derived:%02d"
                            item.Id
                            derivedIndex

                { Id = EventId eventId
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
        let assessments property =
            Forks.assessAll property log

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

    let summarizeMany
        (profile: SourceProfile)
        (paths: string list)
        : BatchValidationSummary =
        let summaries =
            paths
            |> List.sort
            |> List.map (summarize profile)

        let sumVerdicts selector =
            summaries
            |> List.map selector
            |> List.fold
                (fun total counts ->
                    { Sound = total.Sound + counts.Sound
                      Conditional =
                        total.Conditional
                        + counts.Conditional
                      Unsound = total.Unsound + counts.Unsound })
                { Sound = 0
                  Conditional = 0
                  Unsound = 0 }

        { Runs = summaries.Length
          InputEvents = summaries |> List.sumBy _.InputEvents
          NormalizedEvents =
            summaries |> List.sumBy _.NormalizedEvents
          GradeDistribution =
            summaries
            |> List.countBy (fun summary ->
                summary.Grade.Grade)
            |> Map.ofList
          VerifiedRuns =
            summaries
            |> List.filter (fun summary ->
                summary.Grade.Verified)
            |> List.length
          ProjectionCuts = sumVerdicts _.ProjectionCuts
          ExternalContinuationCuts =
            sumVerdicts _.ExternalContinuationCuts
          CounterfactualCuts =
            sumVerdicts _.CounterfactualCuts
          RunsWithCounterfactualUnsoundCuts =
            summaries
            |> List.filter (fun summary ->
                not summary.CounterfactualUnsoundCuts.IsEmpty)
            |> List.length
          UnclassifiedTypes =
            summaries
            |> List.collect _.UnclassifiedTypes
            |> List.distinct
            |> List.sort }

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
