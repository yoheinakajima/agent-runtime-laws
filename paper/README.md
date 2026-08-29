# Submission manuscript

`main.tex` is the submission manuscript. It is deliberately downstream of
`../FINDINGS.md` and `../EVIDENCE.md`; if a number or claim differs, those two
ledgers win until the discrepancy is resolved.

Build with Tectonic:

~~~bash
mkdir -p ../output/pdf
tectonic --outdir ../output/pdf --keep-logs main.tex
mv ../output/pdf/main.pdf ../output/pdf/agent-runtime-laws-paper-draft.pdf
~~~

The checked-in review copy is
`../output/pdf/agent-runtime-laws-paper-draft.pdf`. Build logs and visual-QA
scratch renders are intentionally excluded from version control.

The owner approved this candidate for public freeze and submission on
2026-08-28. The remaining release checklist is:

1. **Completed:** publish and pin, in the public v0.2.0 release, a post-oracle bridge
   fixture whose generator observes no second fixture-oracle call and whose
   verifier checks a hash-bound receipt plus fork-bound caller assertion;
2. **Completed:** exclude the unpublished local executive study from the
   submission while preserving its historical review record;
3. **Completed for release:** make `agent-runtime-laws` public and freeze the
   submission revision as `arxiv-v1`;
4. **Completed:** rerun the capsule, all-cut tables, code gate, and PDF QA from
   the release candidate;
5. **Completed for the repository:** preserve and resolve the independent
   technical review dispositions and obtain owner approval. Submission-service
   author declarations remain an owner action.

## Post-freeze review

The `arxiv-v1` tag is immutable. A second Explore Science review dated
2026-08-29 found 11 minor and zero major issues. Its supplied PDF, online-only
eleventh issue, and disposition ledger are preserved under
`../reviews/2026-08-29/explore-science/`. Warranted changes are developed as a
successor submission candidate rather than moving the v1 tag.
