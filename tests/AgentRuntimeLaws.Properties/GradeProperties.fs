namespace AgentRuntimeLaws.Properties

open AgentRuntimeLaws
open FsCheck.Xunit
open Xunit

module GradeProperties =
    let private allGrades =
        [ Observed; Envelope; Boundary; Checkpointed; Native ]

    let private fromRaw (value: byte) =
        allGrades[int value % allGrades.Length]

    let private evidenceLog facts =
        facts
        |> List.mapi (fun index fact ->
            TestData.event
                "grade"
                (index + 1)
                (EvidenceRecorded fact))

    [<Property(MaxTest = 250)>]
    let lattice_laws_hold_for_all_generated_grade_pairs
        (leftRaw: byte)
        (rightRaw: byte)
        =
        Grades.latticeLawsHold
            (fromRaw leftRaw)
            (fromRaw rightRaw)

    [<Property(MaxTest = 250)>]
    let higher_grades_license_every_question_from_lower_grades
        (leftRaw: byte)
        (rightRaw: byte)
        =
        let left = fromRaw leftRaw
        let right = fromRaw rightRaw
        let lower, upper =
            if Grades.rank left <= Grades.rank right then
                left, right
            else
                right, left

        Set.isSubset (Grades.licenses lower) (Grades.licenses upper)

    [<Property(MaxTest = 250)>]
    let grade_is_deterministic_for_a_closed_log (values: int list) =
        let log = TestData.logOfFacts values
        Grades.grade log = Grades.grade log

    [<Fact>]
    let a_later_hazard_can_downgrade_a_previously_boundary_grade () =
        let safe =
            evidenceLog
                [ EnvelopeCaptured
                  InvocationCompleted
                  BoundaryMediated
                  CleanReconstructionAvailable
                  VerificationPassed ]

        let hazardous =
            safe
            @ [ TestData.event
                    "grade"
                    (safe.Length + 1)
                    (EvidenceRecorded(
                        HazardDetected "untracked network path"
                    )) ]

        Assert.Equal(Boundary, (Grades.grade safe).Grade)
        Assert.Equal(Envelope, (Grades.grade hazardous).Grade)

    [<Fact>]
    let native_marker_alone_does_not_bypass_lower_grade_prerequisites () =
        let report =
            evidenceLog [ NativeRuntime; VerificationPassed ]
            |> Grades.grade

        Assert.Equal(Observed, report.Grade)
        Assert.DoesNotContain(NativeReplayForkAndDiff, report.Licenses)

    [<Fact>]
    let unknown_effect_footprint_blocks_boundary_grade () =
        let evidence =
            evidenceLog
                [ EnvelopeCaptured
                  InvocationCompleted
                  BoundaryMediated
                  CleanReconstructionAvailable ]

        let offset = evidence.Length
        let descriptor = TestData.descriptor "unknown" UnknownFootprint Recorded

        let log =
            evidence
            @ [ TestData.event
                    "grade"
                    (offset + 1)
                    (EffectRequested descriptor)
                TestData.event
                    "grade"
                    (offset + 2)
                    (EffectCommitted(descriptor.Id, "response")) ]

        let report = Grades.grade log

        Assert.Equal(Envelope, report.Grade)
        Assert.Contains(report.Blockers, fun item -> item.Contains("unknown footprint"))

    [<Fact>]
    let grade_taxonomy_is_a_five_element_chain () =
        Assert.True(Grades.formsChain allGrades)
        Assert.Equal(Observed, Grades.meet Observed Native)
        Assert.Equal(Native, Grades.join Observed Native)
