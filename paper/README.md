# Paper draft

`main.tex` is the working arXiv draft. It is deliberately downstream of
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

The draft is not publication-approved. Before submission:

1. **Completed:** publish and pin a post-oracle bridge fork with a hash-bound,
   zero-reexecution receipt and authenticated conformance environment;
2. make an owner decision on the separate 24-run executive study: publish its
   migration-lab bundle or remove the illustrative study from the submission;
3. make `agent-runtime-laws` public and freeze the submission revision;
4. rerun the capsule, all-cut tables, code gate, and PDF QA from that revision;
5. complete final author approval and the preserved independent technical
   review required by the project protocol.
