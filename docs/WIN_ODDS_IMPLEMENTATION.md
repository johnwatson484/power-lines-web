# Win forecasts and fair odds: implementation and measured outcome

Production defaults remain the corrected goals incumbent (365-day half-life, prior five, fitted feasible rho, no calibration) with conservative inclusive 0.65/0.70 H/A tiers. The development-selected prior-ten/temperature-0.85 challenger and wider/sparse/market policies remain experiments: **neither the fair-odds nor shortlist promotion gate passed**. No model novelty or additional low-confidence rows are claimed as a validated improvement.

Companion importer PR: https://github.com/johnwatson484/power-lines-data-service/pull/2 . It is required for source-qualified imports, nullable half-time transport, quote families and import completion records. This web PR cannot correct historical source prices on its own.

## Behavior

- Win shortlist and available-price decisions are separate. H/A wins must outrank the draw and have a unique winner. Strong `p >= high`; broader `low <= p < high`; union counted once. No quota or H/A renormalization. All configured labels are rendered from thresholds.
- Every positive fair price uses `1/p` from unrounded probabilities. Full H/D/A probabilities retain draw risk. Feasible per-fixture rho prevents negative score cells; invalid/empty numerical distributions fail explicitly.
- An actionable price needs supported probabilities, H/A, a genuine available offer, source/revision/observation time, freshness, valid commission/range, and `p * net_odds - 1 > MinExpectedReturn >= 0`. Fractional Kelly must be positive and capped. `MinEdge` remains a separately named disagreement diagnostic. Average/closing/max prices retain their kinds and cannot become bookmaker offers by changing a label.
- Live snapshots are append-only and separate raw/final probabilities, origin, creation/training/calibration cutoffs, model/input/calibration/policy versions and support. Settlements append outcome revisions without changing forecasts. Replay publication preserves `HistoricalReplay`. Legacy rows retain unknown provenance.
- Quote-only changes refresh decisions and reuse rating fits. Score revisions invalidate rating inputs; changed model/calibration/policy versions invalidate projections. `Created` stays ingestion time; `Updated`, general revision and score revision have separate meanings. Transactions use the host's retry strategy as a whole; retries discard prior-attempt row caches.
- Calibration artifacts are versioned and as-of filtered; legacy `accuracy_calibration` buckets are reporting data only. The evaluator fits temperatures from earlier raw out-of-fold forecasts of the matching recipe. Each development block has a preceding cutoff; final parameters, blend and thresholds stay frozen while daily ratings use strictly earlier dates.
- All in-scope fixtures remain visible, including pending, unsupported, failed, stale-feed and past-kickoff rows. Unknown kickoff time is unavailable; postponed/cancelled lifecycle states abstain. Source/quote age, origin/support and distinct model/market prices are shown. There is no current fixture coverage in the frozen local snapshot.
- Legacy host-driven historical overwrite/report workers are removed. Only completed, fingerprint-checked evaluation runs can be atomically published. Immutable evidence is protected in EF and PostgreSQL. Empty scoring cohorts are unavailable; paired market/model scores use identical IDs.

## Frozen inputs and protocol

The read-only repeatable-read export reproduces **241,380 results / 22 divisions**, through **2026-08-20**, and **165 fixtures, August 21–24**. There are no current fixtures as of the 2026-11-04 implementation date. It is local evidence, not a claim about a separate production feed. The original web process remained running; a read-only post-check confirms its schema migration is still `20260822113939_AddExpectedGoalsPerSide`.

The legacy fixture diagnostics reproduce **123 forecasts, six strong and eleven lower flags including strong**. Venue-ten, total-ten and total-five gates all qualify **134/165**; eight fixtures have an unknown in-division team and 23 have known thin history. Merely changing the count gate recovers zero fixtures in this snapshot. The eleven other missing stored forecasts precede their August 22 calculation date.

Committed evidence lives in `evaluation/evidence/win-odds-v1/`: compressed exact snapshot and source-qualified quote inputs, audits, source reconciliation, ordered cohort IDs, original frozen selection/calibration artifacts, predictions CSV, reliability curves, comparison reports, completed-run metadata and reproducibility hashes. Full raw development/final caches remain local under `artifacts/` and can be regenerated. The manifest pins input hashes, divisions, recipe grids, calendar folds, seed 484, 2,000 weekly block resamples and the original promotion targets before final scoring.

Warm raw forecasts start 2021-08-01; development is 2022-08-01–2025-07-31; the protected final period is 2025-08-01–2026-07-31; August 2026 is a separate diagnostic. All result dates are date-only. Training/calibration exclude the entire forecast day. Final parameter/policy selection never uses holdout outcomes. The period may have been used in earlier repository experiments; it is not globally untouched research data. Newly reserved future, actual pre-match validation is still required after feed restoration.

## Protected final results

All-eligible denominator: **7,639** valid matches. Both incumbent and selected challenger forecast **7,057 (92.38%)**. Missing/unforecastable matches stay in coverage denominators. Brier sums squared errors over H/D/A.

| Measure | Corrected incumbent | Locked challenger |
| --- | ---: | ---: |
| All-forecast log loss | 1.016946 | 1.016986 |
| All-forecast Brier | 0.609282 | 0.609175 |
| Unique H/A selections (legacy strict >.65 vs configured >=.65) | 522 | 678 |
| Hit rate on each selected cohort | 77.59% | 76.40% |

Challenger-minus-incumbent paired log-loss delta **+0.0000396**, weekly-block 95% interval **[-0.0010325, +0.0011285]**: inconclusive, not improvement. Brier delta **-0.000107** does not rescue the failed primary gate.

Common-cohort shortlist volume is **+29.89%** with hit-rate delta **-1.185 percentage points**, interval **[-2.777, +0.215] pp**. However overall mean-probability minus hit-rate gap is **-3.413 pp**, interval **[-6.691, -0.133] pp**, outside the required ±5 pp envelope. The expanded total-history policy has 700 selections, but its hit-rate-delta lower bound is **-3.460 pp**, below the -3 pp limit. Both gate failures are retained; targets were not relaxed.

The 678 selections split into 396 strong / 282 broader, with 530 home / 148 away. Home hit rate is 76.79%; away 75.00%. The 156 incremental common-cohort selections hit 72.44%, mean probability 66.32%, and have insufficient support for a reliable incremental claim. The full report includes weekly intervals, all-home and market-favourite baselines on those exact selections, price distributions, every division, calendar periods and class reliability bins.

Total-ten history adds 293 forecasts (96.22% full coverage), but its 22 newcomer selections hit **59.09% against mean probability 72.08%**. Total-five coverage is 98.09%; prior and qualified-market fallback research reaches 100%, with explicit support/source labels. These are descriptive coverage experiments, not promoted recommendations. The 29 completely unknown-team matches are reported separately; prior-backed unknown teams cannot become strong picks merely from default ratings.

Qualified model/market comparison uses **7,015 identical IDs**: challenger log loss **1.016908** versus source-average market **0.999548**; model-minus-market delta **+0.017360**, interval **[+0.013791, +0.021121]**. Market-alone forecasts 7,593/7,639, which is a different all-forecast cohort. Validation chose blend alpha zero, with goals fallback on unpriced matches; this remains shadow research because decision-time quote provenance is absent. There are **zero actual available-price recommendations**. No executable ROI or closing-line-value claim is made; timing-unknown average-price ROI would be a simulation.

## Reproduction and validation

The reproduction helper uses Docker with the tested .NET 8 SDK image pinned by digest, mounts source read-only, and writes only its output directory. The direct console command requires .NET 8. None of these commands starts the ASP.NET host:

```bash
python3 scripts/reproduce-evaluation.py --output artifacts/reproduce-locked-final
# Full declared development tournament, if auditing the original selection rule:
dotnet run --project PowerLinesWeb.Evaluation --configuration Release -- \
  --snapshot evaluation/evidence/win-odds-v1/snapshot.jsonl.gz \
  --quotes evaluation/evidence/win-odds-v1/qualified-quotes.json.gz \
  --manifest evaluation/manifests/win-odds-v1.json \
  --output artifacts/reproduce-full --phase full

dotnet test PowerLinesWeb.Tests/PowerLinesWeb.Tests.csproj --configuration Release
```

Set `POWERLINES_TEST_DB` only to a disposable isolated database named `power_lines_test` to include persistence/job tests; without it those tests explicitly skip. Never point tests or the web host at the full development database for experiments. The EF design-time factory builds migrations without executing `Program`.

Completed checks: **106 web tests** with isolated PostgreSQL and the host retry strategy; **4 companion parser tests**, including the complete E0 source; fixed-snapshot chronological development and protected evaluation; repeated native .NET **8.0.33** predictions/report/reliability hashes; non-root UID 1000 production-container HTTP/security-header smoke; isolated RabbitMQ fixture quote updates/result correction/import-status transport; immutable settlement/publication integration; and complete 380-row staged repair/idempotence/numeric rollback.

The host’s .NET 10.0.14 produced identical report/reliability hashes but different low-order raw probability decimal bytes. Exact prediction hashes therefore use the pinned .NET 8.0.33 Linux container; runtime/platform/architecture also enter raw-cache and run identity. The provided reproduction helper was executed successfully and verified all three recorded hashes without reselecting parameters.

The old explicit real-database half-life/MinEdge sweep tests were **not run**. Their legacy real-DB/calibration assumptions are unsuitable for promotion; the declared half-life/prior ablations ran in the fixed offline evaluator instead. Joint DC fitting, multinomial/Elo and new covariates were not run or promoted. No shared migration, re-import, deployment, merge, bet or prospective feed/offer validation occurred. The .NET 8 build still reports the pre-existing explicit MinEdge-sweep cast analyzer warning and Docker legacy ENV syntax warning; neither test was used as evidence.

## Feed, migration, backfill and rollback

The actual producer is `power-lines-data-service`'s one-shot `Program → ImportService → ImportFactory → FixtureImport/ResultImport`; it downloads Football-Data and sends fanout messages. Its manual `scripts/run-service` wrapper and master image-build workflow do not schedule imports. No importer is in the inspected local stack, and no local user crontab/external production schedule was identified. Confirm deployment ownership/schedule and per-division successful current-season completion before advertising live recovery. Football-Data's [fixture page](https://football-data.co.uk/matches.php) advertised a 2026-11-03 refresh when inspected; that does not establish a successful local or production import.

`Feed` options expose season-aware freshness, including a configurable summer transition and in-season result/import age limits. Legacy completion times remain unknown. Confirm source kickoff timezone conventions before enabling live workers; date-only historical replay deliberately avoids intraday assumptions. Average-price quote observation times remain unknown, and offered-price collection is a separate dependency.

Before production rollout, pause producers/workers, take and restore-test a consistent PostgreSQL backup, retain source hashes/queue state, and review staged before/after cohorts. Apply the additive evidence/revision migrations with workers controlled (`Workers__Enabled=false`), then deploy compatible web consumers before the importer. Legacy fixture ingestion timestamps and historical calibration provenance are not fabricated. Reconcile approved source cohorts idempotently using exact identity/score/value guards; preserve `Created` and valid values when new data is missing. The complete E0 staged repair passed in a disposable database; the other 15,098 staged differences still require cohort/source review before application.

Reports are published only by an explicit operator action after the completed artifact checksums verify:

```bash
# POWERLINES_PUBLISH_DB must name the intended database; this does NOT migrate or start workers.
dotnet run --project PowerLinesWeb.Evaluation --configuration Release -- \
  --publish artifacts/reproduce-locked-final
```

Publication is transactional and append-only, including replay snapshot rows. It was exercised only against an isolated smoke database. Collect actual pre-match forecasts and decision-time offers prospectively after feed restoration.

For rollback, pause producers/workers and revert application images while retaining additive evidence tables. Restore the verified backup if data/provenance repair must be fully undone; the generated guarded inverse restores numeric prices only and is not a complete provenance rollback. Preserve immutable forecast/outcome revisions for audit. Do not run destructive Down migrations on retained evidence as a routine rollback. No rollout action is part of this PR.

Implementation resides in an isolated worktree. The original checkout's README edits were preserved. A running source watcher was discovered during inspection and the implementation was moved out; the original process was not restarted, and the shared migration version was independently verified unchanged.
