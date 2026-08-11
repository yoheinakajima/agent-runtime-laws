namespace AgentRuntimeLaws

open System
open System.Security.Cryptography
open System.Text

type ForkProperty =
    | ProjectionReplay
    | StrictExecutionReplay
    | ExternalContinuation
    | CounterfactualWorld

type FindingSeverity =
    | Condition
    | Blocker

type SafetyFinding =
    { Code: string
      Severity: FindingSeverity
      EffectId: EffectId option
      Message: string }

type ForkVerdict =
    | Sound
    | Conditional
    | Unsound

type ForkAssessment =
    { Property: ForkProperty
      Cut: int
      Verdict: ForkVerdict
      Findings: SafetyFinding list }

type ForkedLog =
    { ParentRunId: RunId
      ChildRunId: RunId
      Cut: int
      SharedPrefix: Event list
      Continuation: Event list
      PrefixHash: string
      InheritedEffects: Map<EffectId, EffectDescriptor> }

module Forks =
    let prefix (cut: int) (log: Event list) : Event list =
        if cut < 0 || cut > List.length log then
            invalidArg
                (nameof cut)
                "cut must be between zero and the log length"

        List.take cut log

    let suffix (cut: int) (log: Event list) : Event list =
        if cut < 0 || cut > List.length log then
            invalidArg
                (nameof cut)
                "cut must be between zero and the log length"

        List.skip cut log

    let private hashPrefix (events: Event list) =
        let canonical =
            events
            |> List.map (fun event ->
                sprintf
                    "%A|%A|%d|%A|%A|%A"
                    event.Id
                    event.RunId
                    event.Sequence
                    event.Kind
                    event.CausedBy
                    event.EmittedBy)
            |> String.concat "\n"

        canonical
        |> Encoding.UTF8.GetBytes
        |> SHA256.HashData
        |> Convert.ToHexString
        |> _.ToLowerInvariant()

    let events (branch: ForkedLog) =
        branch.SharedPrefix @ branch.Continuation

    let fork
        (parentRunId: RunId)
        (childRunId: RunId)
        (cut: int)
        (log: Event list)
        : ForkedLog =
        let retained = prefix cut log

        { ParentRunId = parentRunId
          ChildRunId = childRunId
          Cut = cut
          SharedPrefix = retained
          Continuation = []
          PrefixHash = hashPrefix retained
          InheritedEffects = (Kernel.project retained).Effects }

    let forkBranch
        (childRunId: RunId)
        (cut: int)
        (source: ForkedLog)
        : ForkedLog =
        fork source.ChildRunId childRunId cut (events source)

    let continueWith
        (kind: EventKind)
        (branch: ForkedLog)
        : ForkedLog =
        let ordinal = branch.Continuation.Length + 1
        let child =
            match branch.ChildRunId with
            | RunId value -> value

        let causedBy =
            events branch
            |> List.tryLast
            |> Option.map _.Id

        let event =
            { Id =
                EventId(
                    sprintf
                        "%s:continuation:%06d"
                        child
                        ordinal
                )
              RunId = branch.ChildRunId
              Sequence =
                int64 (
                    branch.SharedPrefix.Length
                    + ordinal
                )
              Kind = kind
              CausedBy = causedBy
              EmittedBy = None }

        { branch with
            Continuation = branch.Continuation @ [ event ] }

    let private finding
        (severity: FindingSeverity)
        (code: string)
        (effectId: EffectId option)
        (message: string)
        : SafetyFinding =
        { Code = code
          Severity = severity
          EffectId = effectId
          Message = message }

    let private integrityFinding fault =
        match fault with
        | DuplicateEffectRequest effectId ->
            finding
                Blocker
                "duplicate-effect-request"
                (Some effectId)
                "the same effect identity was requested more than once"
        | ConflictingEffectDescriptor effectId ->
            finding
                Blocker
                "conflicting-effect-descriptor"
                (Some effectId)
                "one effect identity has conflicting request descriptors"
        | EffectOutcomeWithoutRequest effectId ->
            finding
                Blocker
                "effect-outcome-without-request"
                (Some effectId)
                "an effect outcome has no preceding request"
        | EffectAlreadyTerminal(effectId, lifecycle) ->
            finding
                Blocker
                "effect-already-terminal"
                (Some effectId)
                (sprintf
                    "an outcome followed terminal lifecycle %A"
                    lifecycle)

    let private malformedFindings
        (log: Event list)
        : SafetyFinding list =
        let duplicateIds =
            log
            |> List.countBy _.Id
            |> List.choose (fun (eventId, count) ->
                if count > 1 then
                    Some(
                        finding
                            Blocker
                            "duplicate-event-id"
                            None
                            (sprintf
                                "event id %A occurs %d times"
                                eventId
                                count)
                    )
                else
                    None)

        let expected = [ 1L .. int64 log.Length ]
        let actual = log |> List.map _.Sequence

        let sequence =
            if actual = expected then
                []
            else
                [ finding
                      Blocker
                      "non-contiguous-sequence"
                      None
                      (sprintf
                          "expected sequence %A but observed %A"
                          expected
                          actual) ]

        let integrity =
            (Kernel.project log).IntegrityFaults
            |> List.map integrityFinding

        duplicateIds @ sequence @ integrity

    let private effectsAt
        (events: Event list)
        : Map<EffectId, EffectDescriptor> =
        (Kernel.project events).Effects

    let private openCutFindings
        (retained: Event list)
        : SafetyFinding list =
        effectsAt retained
        |> Map.toList
        |> List.choose (fun (effectId, effect) ->
            match effect.Lifecycle with
            | Requested ->
                Some(
                    finding
                        Blocker
                        "cut-through-request"
                        (Some effectId)
                        "the cut retains a request without a terminal outcome"
                )
            | Unknown ->
                Some(
                    finding
                        Blocker
                        "unknown-effect-at-cut"
                        (Some effectId)
                        "the cut inherits an externally ambiguous effect"
                )
            | Committed
            | Failed -> None)

    let private strictReplayFindings
        (retained: Event list)
        : SafetyFinding list =
        effectsAt retained
        |> Map.toList
        |> List.choose (fun (effectId, effect) ->
            if effect.ReplaySource = Uncaptured then
                Some(
                    finding
                        Blocker
                        "uncaptured-prefix-effect"
                        (Some effectId)
                        "strict replay cannot serve or reproduce this result"
                )
            else
                None)

    let private noReexecutionConditions
        (retained: Event list)
        : SafetyFinding list =
        effectsAt retained
        |> Map.toList
        |> List.choose (fun (effectId, effect) ->
            match effect.Lifecycle, effect.Footprint with
            | Committed, OneShot ->
                Some(
                    finding
                        Condition
                        "inherit-one-shot-without-reexecution"
                        (Some effectId)
                        "the inherited one-shot must not execute again"
                )
            | Committed, UnknownFootprint ->
                Some(
                    finding
                        Blocker
                        "unknown-inherited-footprint"
                        (Some effectId)
                        "the inherited effect footprint is unknown"
                )
            | _ -> None)

    let private committedWorldFinding effectId effect =
        match effect.Footprint with
        | Pure -> None
        | Idempotent ->
            Some(
                finding
                    Condition
                    "discarded-idempotent-world-effect"
                    (Some effectId)
                    "the discarded effect requires reconciliation or overwrite"
            )
        | Compensatable ->
            Some(
                finding
                    Condition
                    "discarded-effect-requires-compensation"
                    (Some effectId)
                    "environmental safety requires successful compensation"
            )
        | OneShot ->
            Some(
                finding
                    Blocker
                    "discarded-one-shot-still-happened"
                    (Some effectId)
                    "the omitted irreversible action remains true in the world"
            )
        | UnknownFootprint ->
            Some(
                finding
                    Blocker
                    "discarded-effect-footprint-unknown"
                    (Some effectId)
                    "the external footprint of the discarded effect is unknown"
            )

    let private unresolvedDiscardedFinding effectId effect =
        match effect.Lifecycle with
        | Requested
        | Unknown ->
            Some(
                finding
                    Blocker
                    "discarded-request-may-have-executed"
                    (Some effectId)
                    "a discarded request has no trustworthy terminal boundary"
            )
        | Failed ->
            match effect.Footprint with
            | Pure -> None
            | Idempotent
            | Compensatable ->
                Some(
                    finding
                        Condition
                        "discarded-failed-effect-needs-reconciliation"
                        (Some effectId)
                        "failure does not establish that the world was unchanged"
                )
            | OneShot
            | UnknownFootprint ->
                Some(
                    finding
                        Blocker
                        "discarded-failed-effect-ambiguous"
                        (Some effectId)
                        "a failed irreversible or unknown effect may be partial"
                )
        | Committed -> None

    let private suffixWorldFindings
        (discarded: Event list)
        (fullLog: Event list)
        : SafetyFinding list =
        let fullEffects = effectsAt fullLog

        discarded
        |> List.choose (fun event ->
            match event.Kind with
            | EffectRequested descriptor ->
                fullEffects
                |> Map.tryFind descriptor.Id
                |> Option.bind (unresolvedDiscardedFinding descriptor.Id)
            | EffectCommitted(effectId, _) ->
                fullEffects
                |> Map.tryFind effectId
                |> Option.bind (committedWorldFinding effectId)
            | EffectBecameUnknown effectId ->
                Some(
                    finding
                        Blocker
                        "discarded-unknown-world-effect"
                        (Some effectId)
                        "the discarded suffix has an ambiguous external result"
                )
            | _ -> None)

    let private verdict
        (findings: SafetyFinding list)
        : ForkVerdict =
        if
            findings
            |> List.exists (fun item ->
                item.Severity = Blocker)
        then
            Unsound
        elif findings.IsEmpty then
            Sound
        else
            Conditional

    let assess
        (property: ForkProperty)
        (cut: int)
        (log: Event list)
        : ForkAssessment =
        let retained = prefix cut log
        let discarded = suffix cut log
        let malformed = malformedFindings log

        let propertyFindings =
            match property with
            | ProjectionReplay -> []
            | StrictExecutionReplay ->
                openCutFindings retained
                @ strictReplayFindings retained
            | ExternalContinuation ->
                openCutFindings retained
                @ strictReplayFindings retained
                @ noReexecutionConditions retained
            | CounterfactualWorld ->
                openCutFindings retained
                @ strictReplayFindings retained
                @ noReexecutionConditions retained
                @ suffixWorldFindings discarded log

        let findings = malformed @ propertyFindings

        { Property = property
          Cut = cut
          Verdict = verdict findings
          Findings = findings }

    let equivalent
        (observation: Observation)
        (left: Event list)
        (right: Event list)
        : bool =
        match observation with
        | ExactTrace -> left = right
        | ProjectedState ->
            State.domainProjection (Kernel.project left) = State.domainProjection (Kernel.project right)
        | FactsOnly keys ->
            let leftState = Kernel.project left
            let rightState = Kernel.project right

            keys
            |> Set.forall (fun key ->
                Map.tryFind key leftState.Facts = Map.tryFind key rightState.Facts)

    let private sourceRunId (log: Event list) : RunId =
        log
        |> List.tryHead
        |> Option.map _.RunId
        |> Option.defaultValue (RunId "empty-parent")

    let identityHolds
        (observation: Observation)
        (log: Event list)
        : bool =
        let branch =
            fork
                (sourceRunId log)
                (RunId "identity")
                log.Length
                log

        equivalent observation branch.SharedPrefix log

    let observationalNestedCollapseHolds
        (observation: Observation)
        (outerCut: int)
        (innerCut: int)
        (log: Event list)
        : bool =
        if
            innerCut < 0
            || outerCut < innerCut
            || outerCut > log.Length
        then
            false
        else
            let parent = sourceRunId log

            let outer =
                fork parent (RunId "outer") outerCut log

            let nested =
                forkBranch (RunId "nested") innerCut outer

            let direct =
                fork parent (RunId "direct") innerCut log

            equivalent
                observation
                nested.SharedPrefix
                direct.SharedPrefix
            && nested.ParentRunId = outer.ChildRunId

    let projectionCommutes
        (cut: int)
        (log: Event list)
        : bool =
        let branch =
            fork
                (sourceRunId log)
                (RunId "projection")
                cut
                log

        State.domainProjection (Kernel.project branch.SharedPrefix) = State.domainProjection (Kernel.project (prefix cut log))

    let discardedSuffixIrrelevantToProjection
        (cut: int)
        (log: Event list)
        : bool =
        let retained = prefix cut log

        let branch =
            fork
                (sourceRunId log)
                (RunId "projection")
                cut
                log

        State.domainProjection (Kernel.project retained) = State.domainProjection (Kernel.project branch.SharedPrefix)
