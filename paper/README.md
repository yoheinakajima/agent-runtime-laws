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

1. publish or commit the bridge study artifact so its source provenance is
   externally reproducible;
2. review every related-work characterization against the cited primary source;
3. freeze the F# artifact revision and replace the draft repository placeholder;
4. rerun the capsule and all-cut tables from that revision;
5. complete an author and independent technical review.
