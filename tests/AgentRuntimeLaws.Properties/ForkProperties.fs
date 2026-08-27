namespace AgentRuntimeLaws.Properties

open AgentRuntimeLaws
open FsCheck.Xunit
open Xunit

module ForkProperties =
    let private footprintFrom (value: byte) =
        match int value % 5 with
        | 0 -> Pure
        | 1 -> Idempotent
        | 2 -> Compensatable
        | 3 -> OneShot
        | _ -> UnknownFootprint

    let private replaySourceFrom (value: byte) =
        match int value % 3 with
        | 0 -> Deterministic
        | 1 -> Recorded
        | _ -> Uncaptured

    [<Property(MaxTest = 250)>]
    let identity_fork_preserves_the_full_trace (values: int list) =
        let log = TestData.logOfFacts values
        Forks.identityHolds ExactTrace log

    [<Property(MaxTest = 250)>]
    let nested_fork_collapses_to_the_direct_retained_prefix
        (values: int list)
        (outerRaw: uint16)
        (innerRaw: uint16)
        =
        let log = TestData.logOfFacts values
        let outer = int outerRaw % (log.Length + 1)
        let inner = int innerRaw % (outer + 1)
        Forks.observationalNestedCollapseHolds ExactTrace outer inner log

    [<Property(MaxTest = 250)>]
    let fork_commutes_with_projection_at_every_generated_cut
        (values: int list)
        (rawCut: uint16)
        =
        let log = TestData.logOfFacts values
        let cut = int rawCut % (log.Length + 1)
        Forks.projectionCommutes cut log

    [<Property(MaxTest = 250)>]
    let discarded_suffix_is_irrelevant_to_retained_prefix_projection
        (values: int list)
        (rawCut: uint16)
        =
        let log = TestData.logOfFacts values
        let cut = int rawCut % (log.Length + 1)
        Forks.discardedSuffixIrrelevantToProjection cut log

    [<Property(MaxTest = 250)>]
    let external_continuation_verdict_matches_generated_effect_dimensions
        (footprintRaw: byte)
        (replayRaw: byte)
        =
        let footprint = footprintFrom footprintRaw
        let replaySource = replaySourceFrom replayRaw
        let log = TestData.effectLog footprint replaySource
        let actual = Forks.assess ExternalContinuation log.Length log

        let expected =
            match replaySource, footprint with
            | Uncaptured, _ -> Unsound
            | _, OneShot -> Conditional
            | _, UnknownFootprint -> Unsound
            | _ -> Sound

        actual.Verdict = expected

    [<Property(MaxTest = 250)>]
    let discarded_effect_world_verdict_matches_generated_footprint
        (footprintRaw: byte)
        (replayRaw: byte)
        =
        let footprint = footprintFrom footprintRaw
        let replaySource = replaySourceFrom replayRaw
        let log = TestData.effectLog footprint replaySource
        let actual = Forks.assess CounterfactualWorld 0 log

        let expected =
            match footprint with
            | Pure -> Sound
            | Idempotent
            | Compensatable -> Conditional
            | OneShot
            | UnknownFootprint -> Unsound

        actual.Verdict = expected

    [<Fact>]
    let child_continuation_has_isolated_identity_and_preserves_parent_prefix () =
        let log = TestData.logOfFacts [ 1; 2 ]

        let branch =
            Forks.fork
                (RunId "parent-run")
                (RunId "child-run")
                1
                log

        let continued = Forks.continueWith (FactSet("child", 3)) branch
        let event = Assert.Single(continued.Continuation)
        let eventId =
            match event.Id with
            | EventId value -> value

        Assert.Empty(branch.Continuation)
        Assert.Equal(log.Head, continued.SharedPrefix.Head)
        Assert.Equal(RunId "child-run", event.RunId)
        Assert.StartsWith("child-run:continuation:", eventId)
        Assert.Equal(2L, event.Sequence)

    [<Fact>]
    let nested_branch_records_the_outer_child_as_its_parent () =
        let log = TestData.logOfFacts [ 1; 2 ]

        let outer =
            Forks.fork
                (RunId "root")
                (RunId "outer-child")
                1
                log
            |> Forks.continueWith (FactSet("outer", 9))

        let nested =
            Forks.forkBranch
                (RunId "nested-child")
                2
                outer

        Assert.Equal(outer.ChildRunId, nested.ParentRunId)
        Assert.Equal(RunId "nested-child", nested.ChildRunId)
        Assert.Equal(2, nested.SharedPrefix.Length)

    [<Fact>]
    let projected_state_ignores_replay_event_identity_metadata () =
        let original = TestData.logOfFacts [ 1; 2 ]

        let renamed =
            original
            |> List.mapi (fun index event ->
                { event with
                    Id = EventId(sprintf "renamed-%d" index) })

        Assert.True(Forks.equivalent ProjectedState original renamed)
        Assert.False(Forks.equivalent ExactTrace original renamed)

    [<Fact>]
    let discarded_one_shot_invalidates_environmental_counterfactual () =
        let log = TestData.effectLog OneShot Recorded
        let projection = Forks.assess ProjectionReplay 0 log
        let environmental = Forks.assess CounterfactualWorld 0 log

        Assert.Equal(Sound, projection.Verdict)
        Assert.Equal(Unsound, environmental.Verdict)

        Assert.Contains(
            environmental.Findings,
            fun finding -> finding.Code = "discarded-one-shot-still-happened"
        )

    [<Fact>]
    let retained_committed_one_shot_is_conditional () =
        let log = TestData.effectLog OneShot Recorded
        let assessment = Forks.assess ExternalContinuation log.Length log

        Assert.Equal(Conditional, assessment.Verdict)

        Assert.Contains(
            assessment.Findings,
            fun finding -> finding.Code = "inherit-one-shot-without-reexecution"
        )

    [<Fact>]
    let cut_through_external_request_is_unsound_for_strict_replay () =
        let log = TestData.effectLog Idempotent Recorded
        let assessment = Forks.assess StrictExecutionReplay 1 log

        Assert.Equal(Unsound, assessment.Verdict)

        Assert.Contains(
            assessment.Findings,
            fun finding -> finding.Code = "cut-through-request"
        )

    [<Fact>]
    let discarded_unresolved_request_is_environmentally_unsound () =
        let descriptor = TestData.descriptor "unresolved" Idempotent Recorded

        let log =
            [ TestData.event
                  "unresolved"
                  1
                  (EffectRequested descriptor) ]

        let assessment = Forks.assess CounterfactualWorld 0 log

        Assert.Equal(Unsound, assessment.Verdict)

        Assert.Contains(
            assessment.Findings,
            fun finding -> finding.Code = "discarded-request-may-have-executed"
        )

    [<Fact>]
    let malformed_effect_outcome_makes_even_projection_assessment_unsound () =
        let effectId = EffectId "malformed"

        let log =
            [ TestData.event
                  "malformed"
                  1
                  (EffectCommitted(effectId, "response")) ]

        let assessment = Forks.assess ProjectionReplay log.Length log

        Assert.Equal(Unsound, assessment.Verdict)

        Assert.Contains(
            assessment.Findings,
            fun finding -> finding.Code = "effect-outcome-without-request"
        )

    [<Fact>]
    let duplicate_effect_request_makes_fork_assessment_unsound () =
        let descriptor = TestData.descriptor "duplicate-fork" Pure Deterministic

        let log =
            [ TestData.event "duplicate-fork" 1 (EffectRequested descriptor)
              TestData.event "duplicate-fork" 2 (EffectRequested descriptor) ]

        let assessment = Forks.assess ProjectionReplay log.Length log

        Assert.Equal(Unsound, assessment.Verdict)

        Assert.Contains(
            assessment.Findings,
            fun finding -> finding.Code = "duplicate-effect-request"
        )
