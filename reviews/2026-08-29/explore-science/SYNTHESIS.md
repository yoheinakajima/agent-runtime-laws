# Review synthesis and disposition

Status: incorporated on the successor review branch. The immutable `arxiv-v1`
tag remains unchanged.

## Summary

The review scored the manuscript 99/100 and reported 11 minor, zero major
issues. All 11 were accepted in substance. Two proposed count explanations were
re-derived from the pinned SQLite capsule before inclusion; the manuscript uses
the exact per-run identities rather than the review's informal per-response
ratio.

## Dispositions

| Issue | Disposition |
|---|---|
| A1 assertion/property cross-map | Accepted. Added an explicit seven-row map to fork properties, equivalences, grades, and intentional scope exclusions. |
| B1 delayed hazard race | Accepted. Made every verdict snapshot-relative and required a bound high-water mark plus closure/quiescence/freshness and a final recheck for production continuation. |
| B2 overloaded log notation | Accepted. Reserved `L` for a fixed input log and renamed the configuration trace to `L_ret`. |
| B3 FsCheck reporting | Accepted. Reported 15 properties, 35 facts, MaxTest 100--250, 3,300 top-level trials, and no fixed global seed. |
| B4 Definition 5 self-containment | Accepted. Kept the normative ExternalContinuation statement inside the definition and moved implementation/bridge limits to a labeled remark. |
| B5 conformance-vector coverage | Accepted as a scope correction. Added a seven-vector coverage table and named four obligations without dedicated portable vectors. No late expansion of the conformance schema was attempted. |
| C1 CounterfactualWorld count | Accepted after independent derivation. The exact identity is 141,545 final-response-ordinal cuts over closed runs plus 1,349 cuts over 24 open runs, totaling 142,894. |
| C2 continuation Conditional count | Accepted with corrected mechanism. The exact per-run term is `n + 1 - first_request_ordinal - request_count`; its sum is 120,969. The suggested 3.34-per-response attribution was not used because tail positions overlap across responses. |
| C3 verdict capitalization | Accepted. Added a terminology convention and normalized token uses while retaining lowercase ordinary predicates. |
| D1 tonal register | Accepted. Replaced both colloquial phrases with formal alternatives. |
| E1 tabular display labels | Accepted. Relabeled the two row/column displays as tables and explicitly anchored both in the preceding prose. |

## Artifact impact

No kernel or activegraph-bridge behavior changed. The delayed-hazard issue is a
production admission/freshness contract outside the pure fixed-snapshot
assessor; the public bridge v0.2.0 receipt remains valid for its bounded offline
fixture claim and is not silently promoted into a production freshness result.

A new read-only SQL audit,
`scripts/audit_synthetic_cut_identities.sql`, independently reproduces the
ExternalContinuation and CounterfactualWorld headline counts and verifies the
request adjacency assumption used by the derivations.

## Final QA

The successor manuscript passed the complete release gate on 2026-08-29:

- .NET restore/build completed with zero warnings and zero errors; all 50 tests
  passed, including 15 FsCheck properties and 35 facts;
- the seven language-neutral vectors and evidence manifest passed;
- the pinned Synthetic Players capsule verifier passed for 4,919 archived runs;
- fresh export and validation reproduced 5,540 logs, 311,756 events, 317,296
  cuts, and every reported verdict total;
- the new structural SQL audit reproduced the two reviewed count identities,
  and the observed-fork audit reproduced 121 forks, 16,625 compared prefix
  events, and zero mismatches;
- a clean checkout of activegraph-bridge at revision
  `843824a44d48d816779fc0c08580ae06108fe7b6` passed the post-oracle receipt
  verifier, all 76 tests, Ruff, and mypy; and
- the 27-page PDF was rebuilt without substantive TeX warnings, all fonts are
  embedded, and every page passed visual inspection. Its SHA-256 is
  `a1f76e90ef6e8904b3fad8b963a0653ccb297d09396d4b93e6652b8c6f121d7d`.
