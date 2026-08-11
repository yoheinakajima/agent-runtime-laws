namespace AgentRuntimeLaws.Properties

open AgentRuntimeLaws
open FsCheck.Xunit
open Xunit

module ForkProperties =
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
