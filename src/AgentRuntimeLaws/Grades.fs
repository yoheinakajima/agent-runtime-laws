namespace AgentRuntimeLaws

type ReplayGrade =
    | Observed
    | Envelope
    | Boundary
    | Checkpointed
    | Native

type LicensedQuestion =
    | InspectLineage
    | PlaybackRecordedOutput
    | ReexecuteOnRecordedEffects
    | ForkAtEffectBoundary
    | ResumeFromCheckpoint
    | NativeReplayForkAndDiff

type GradeReport =
    { Grade: ReplayGrade
      Verified: bool
      Blockers: string list
      Evidence: EvidenceFact list
      Licenses: Set<LicensedQuestion> }

module Grades =
    let licenses
        (grade: ReplayGrade)
        : Set<LicensedQuestion> =
        match grade with
        | Observed ->
            Set.singleton InspectLineage
        | Envelope ->
            Set.ofList
                [ InspectLineage
                  PlaybackRecordedOutput ]
        | Boundary ->
            Set.ofList
                [ InspectLineage
                  PlaybackRecordedOutput
                  ReexecuteOnRecordedEffects
                  ForkAtEffectBoundary ]
        | Checkpointed ->
            Set.ofList
                [ InspectLineage
                  PlaybackRecordedOutput
                  ReexecuteOnRecordedEffects
                  ForkAtEffectBoundary
                  ResumeFromCheckpoint ]
        | Native ->
            Set.ofList
                [ InspectLineage
                  PlaybackRecordedOutput
                  ReexecuteOnRecordedEffects
                  ForkAtEffectBoundary
                  ResumeFromCheckpoint
                  NativeReplayForkAndDiff ]

    let rank (grade: ReplayGrade) : int =
        Set.count (licenses grade)

    let compare
        (left: ReplayGrade)
        (right: ReplayGrade)
        : int =
        let leftLicenses = licenses left
        let rightLicenses = licenses right

        if leftLicenses = rightLicenses then
            0
        elif Set.isSubset leftLicenses rightLicenses then
            -1
        elif Set.isSubset rightLicenses leftLicenses then
            1
        else
            invalidArg
                "grades"
                "replay grades are incomparable by licensed questions"

    let meet
        (left: ReplayGrade)
        (right: ReplayGrade)
        : ReplayGrade =
        if compare left right <= 0 then left else right

    let join
        (left: ReplayGrade)
        (right: ReplayGrade)
        : ReplayGrade =
        if compare left right >= 0 then left else right

    let private contains
        (predicate: EvidenceFact -> bool)
        (evidence: EvidenceFact list)
        : bool =
        evidence |> List.exists predicate

    let private integrityBlocker = function
        | DuplicateEffectRequest effectId ->
            sprintf "duplicate request %A" effectId
        | ConflictingEffectDescriptor effectId ->
            sprintf "conflicting descriptor %A" effectId
        | EffectOutcomeWithoutRequest effectId ->
            sprintf "outcome without request %A" effectId
        | EffectAlreadyTerminal(effectId, lifecycle) ->
            sprintf
                "outcome after terminal %A for %A"
                lifecycle
                effectId

    let private blockers
        (evidence: EvidenceFact list)
        (state: State)
        : string list =
        let evidenceBlockers =
            evidence
            |> List.choose (function
                | HazardDetected detail ->
                    Some(sprintf "hazard: %s" detail)
                | LossyEnvelope ->
                    Some "lossy invocation envelope"
                | UnmediatedEffect detail ->
                    Some(
                        sprintf
                            "unmediated effect: %s"
                            detail
                    )
                | _ -> None)

        let effectBlockers =
            state.Effects
            |> Map.toList
            |> List.collect (fun (effectId, effect) ->
                [ match effect.Footprint with
                  | UnknownFootprint ->
                      yield
                          sprintf
                              "unknown footprint %A"
                              effectId
                  | _ -> ()

                  match effect.ReplaySource, effect.Lifecycle with
                  | Uncaptured, _ ->
                      yield
                          sprintf
                              "uncaptured effect %A"
                              effectId
                  | _, Unknown ->
                      yield
                          sprintf
                              "unknown effect outcome %A"
                              effectId
                  | _, Requested ->
                      yield
                          sprintf
                              "incomplete effect %A"
                              effectId
                  | _ -> () ])

        let integrityBlockers =
            state.IntegrityFaults
            |> List.map integrityBlocker

        evidenceBlockers
        @ effectBlockers
        @ integrityBlockers

    let grade (log: Event list) : GradeReport =
        let state = Kernel.project log
        let evidence = state.Evidence
        let blockers = blockers evidence state

        let envelope =
            contains ((=) EnvelopeCaptured) evidence
            && contains ((=) InvocationCompleted) evidence
            && not (contains ((=) LossyEnvelope) evidence)

        let boundary =
            envelope
            && contains ((=) BoundaryMediated) evidence
            && contains ((=) CleanReconstructionAvailable) evidence
            && blockers.IsEmpty

        let checkpointed =
            boundary
            && contains ((=) CheckpointRecorded) evidence

        let native =
            checkpointed
            && contains ((=) NativeRuntime) evidence

        let value =
            if native then Native
            elif checkpointed then Checkpointed
            elif boundary then Boundary
            elif envelope then Envelope
            else Observed

        let verified =
            blockers.IsEmpty
            && contains ((=) VerificationPassed) evidence

        { Grade = value
          Verified = verified
          Blockers = blockers
          Evidence = evidence
          Licenses = licenses value }

    let formsChain
        (grades: ReplayGrade list)
        : bool =
        grades
        |> List.forall (fun left ->
            grades
            |> List.forall (fun right ->
                let leftSet = licenses left
                let rightSet = licenses right

                Set.isSubset leftSet rightSet
                || Set.isSubset rightSet leftSet))

    let latticeLawsHold
        (left: ReplayGrade)
        (right: ReplayGrade)
        : bool =
        let lower = meet left right
        let upper = join left right

        meet left left = left
        && join left left = left
        && meet left right = meet right left
        && join left right = join right left
        && meet left upper = left
        && join left lower = left
        && Set.isSubset (licenses lower) (licenses left)
        && Set.isSubset (licenses left) (licenses upper)
