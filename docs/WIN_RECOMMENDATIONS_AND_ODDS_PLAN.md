# More win recommendations and better fair odds: review and Sol implementation plan

Reviewed **31 October 2026** against web commit `468d913` and the running local Docker database. This is a proposed implementation handover for a new PR; application code and database contents were not changed by this review. The pre-existing modified `README.md` was preserved.

## 1. Recommended direction

Keep the current goals model as a benchmark, repair the data/evaluation pipeline, and choose its replacement or improvements using chronological comparisons. There is enough results history to compare several modest models. There is currently no trustworthy evidence that a more complex model will be better.

Deliver two distinct user outcomes:

1. **More useful home/away win candidates:** recover supported forecast coverage and provide a ranked, calibrated win shortlist with explicit probability tiers.
2. **More accurate fair odds:** improve the full home/draw/away probability distribution, measuring it with out-of-sample log loss, Brier score and calibration. Fair decimal odds are `1 / probability`; closeness to bookmaker prices is not itself the definition of accuracy.

A win forecast and a bet worth taking at an available price are different decisions. Keep both visible. An actionable price-based recommendation also needs a valid, available quote and positive estimated return at that quote. Do not create extra recommendations by discarding draw probability, inflating confidence, or imposing a daily quota.

**Highest priorities:** data freshness and price provenance; chronological calibration; reproducible evaluation; supported forecasts for sparse teams; a validated wider win shortlist; a market-assisted forecast candidate. Joint Dixon–Coles fitting, richer classifiers and additional data are experiments, not predetermined upgrades.

## 2. What the running data actually contains

The read-only PostgreSQL snapshot below was taken at **2026-10-31 12:37:04 UTC**, using a repeatable-read transaction. It is the local Docker dataset, not evidence about the freshness of any separate production deployment.

| Observation | Measured result | Meaning |
| --- | --- | --- |
| Results | 241,380; 22 divisions; 1993-07-23 through 2026-08-20 | Substantial historical score data, but this snapshot has no September/October results. |
| Fixtures | 165; 2026-08-21 through 2026-08-24 | No current fixtures on the review date. Model changes cannot generate missing fixtures. |
| Complete result price triplets, each price > 1 | 162,240 / 241,380, or 67.21% | Numerically present does not establish correct column mapping or quote timing. |
| No result prices | 79,139; another one has incomplete/invalid prices | Missing prices must stay missing, not become zero-probability observations. |
| Recent prices, dates from 2023-07-01 | 23,340 / 23,354, or 99.94% | Recent history is promising for matched market comparisons after reconciliation. |
| Full-time result consistency | Zero invalid H/D/A codes, goal/result mismatches, negative goals, or home=away rows in these checks | Useful internal checks; not proof of completeness or agreement with source files. |
| Stored result kickoff times | All 241,380 dates are midnight | Historical same-day availability cannot be established. Exclude the entire forecast day from training. |
| Fixture forecasts | 123 / 165, or 74.55%; all calculated on 2026-08-22 | Stored outputs are a historical snapshot. |
| Strong home/away recommendations | 6 / 165, or 3.64% | The strict >70% gate is selective. |
| Lower home/away flag | 11 / 165, or 6.67%, **including the six strong picks** | There are 11 unique flagged win fixtures, not 17. |
| Fixture prices | 165 / 165 complete triplets | Missing prices do not explain this snapshot's missing forecasts. |
| Current history gate, reconstructed at each fixture date | 134 / 165 eligible | 31 fail: 8 have an unknown team in that division; 23 have known but thin-history teams. |
| Alternative gates: 10 total appearances or 5 total appearances | Both still 134 / 165 eligible | Merely replacing the venue count does **not** recover extra fixtures in this snapshot. |
| Other 11 missing forecasts | All were August 21 fixtures; stored forecasts were calculated August 22 | Consistent with the worker skipping fixtures before `DateTime.Today`; a lifecycle explanation, not demonstrated model failure. |
| Accuracy/calibration tables | Both empty at this snapshot | No completed stored accuracy report to validate. |
| Historical forecasts | 9,134 rows at snapshot; worker actively creating more | This is an incomplete, changing reconstruction, not a completed evaluation baseline. |

On the **123 existing forecast rows only**, the number with `max(P(home), P(away)) >= threshold` is 45 at 0.50, 24 at 0.55, 15 at 0.60, 11 at 0.65 and 6 at 0.70. This illustrates the volume tradeoff. It is **not a backtest of accuracy or profit**, and these inclusive diagnostic thresholds differ from the current strict `>` implementation.

Reproducibility artifacts accompany this plan:

- [Read-only audit SQL](review-evidence/2026-10-31-data-audit.sql)
- [Snapshot query output, including all divisions](review-evidence/2026-10-31-data-audit.txt)
- [Source price reconciliation](review-evidence/2026-10-31-source-reconciliation.json)

The Docker web service was already running its background analysis during review. We did not trigger a rebuild, import, migration or recalculation. Snapshot counts may differ when rerun.

To repeat the audit against the same local container (read-only; the review-date literal remains fixed):

```bash
docker exec -i -e PGOPTIONS='-c default_transaction_read_only=on' \
  power-lines-power-lines-web-postgres-1 \
  psql -X -U postgres -d power_lines_web \
  < docs/review-evidence/2026-10-31-data-audit.sql
```

### Confirmed historical price import defect

In sibling `power-lines-data-service`, `PowerLinesDataService/Imports/Readers/ResultReader.cs`, lines 18–23 find price indexes from the header. Lines 39–41 insert a missing `Time` field into the **row only**, but lines 58–60 reuse the original indexes. This shifts the selected price columns in older files.

Reconciliation against the [source E0 2018/19 CSV](https://www.football-data.co.uk/mmz4281/1819/E0.csv) found **380/380 stored triplets equal to maximum prices and 0/380 equal to average prices**. Example: Man United–Leicester, 2018-08-10, stores `1.60 / 4.20 / 8.05`; source averages are `1.56 / 3.92 / 7.06`. This can bias market comparisons and hypothetical returns. It does not prove the post-2019 triplets have the same defect; reconcile by source schema/season instead of applying a blanket correction.

The source documents distinguish pre-closing `AvgH/AvgD/AvgA`, closing `AvgCH/AvgCD/AvgCA`, maxima, and individual bookmakers. Store those distinctions. The present schema loses them. [Football-Data column definitions](https://www.football-data.co.uk/notes.txt), [collection conventions](https://www.football-data.co.uk/downloadm.php).

## 3. Current methodology and available predictors

The web app owns PostgreSQL, message consumers, ratings, analysis and accuracy jobs. The repository's `.github/copilot-instructions.md` still describes a UI-only HTTP client architecture; that description is stale. Follow the actual services registered in `PowerLinesWeb/Program.cs`, retaining the applicable C# style and security conventions.

| Stage | Current behavior | Assessment |
| --- | --- | --- |
| Training set | Same division, previous six years, strictly before fixture calendar date | Correct basic outcome cutoff. Six years is a configurable window, not use of the entire archive. |
| Ratings | Attack/defence per team, league home advantage, exponential recency weights; 365-day half-life, prior strength five | Reasonable parsimonious starting point for scores-only data. Revalidate parameter choices. |
| Optimization | Penalized independent-Poisson coordinate updates, followed by a separate fitted low-score correlation | Describe as a two-stage Poisson model with Dixon–Coles correction; not joint Dixon–Coles maximum likelihood. |
| Forecast | Expected home/away goals; score grid 0–10; low-score correction; normalization; sum to H/D/A | Preserve valid probability invariants and test extreme matchups. |
| Calibration | One division-level isotonic bucket mapping shared by H/D/A, then renormalized | Chronology and feedback are defective; sparse tail clipping may compress favourites. Renormalization alone is not a bug. |
| Eligibility | Home team needs 10 home appearances; away team needs 10 away appearances | Discards usable opposite-venue information and prevents early-season/newcomer forecasts. Changing count alone is insufficient here. |
| Selection | Unique most likely H/D/A result above .70 or .65 | High fixed thresholds; nested flags; draws permitted; prices ignored. |
| Value | Largest model-minus-de-vig probability gap >= .08, quote range 1.2–10, quarter Kelly | Does not require positive expected return at the actual price. |

The original [Dixon–Coles paper](https://academic.oup.com/jrsssc/article-abstract/46/2/265/6990546) supports this model family as a candidate; it does not establish performance on this dataset.

**Stored source inputs:** division, team-name strings, match date, full-time goals/outcome, half-time goals/outcome, one home/draw/away average-price triplet. Half-time fields are unused and have missing historical values represented partly as zeros. Exact string team matching can split histories when names change.

**Derived fields:** `HomeXg`/`AwayXg` are the model's expected goals, calculated in `OddsCalculator.cs:84–86`. They are not observed shot-quality xG and must not be presented as an additional independent source of evidence.

**Not stored:** observed xG, shots, shots on target, cards, lineups, injuries, suspensions, manager changes, stable source match/team IDs, result-availability times, individual bookmaker quote histories, or verified closing-line snapshots. Some match statistics exist in source files for some leagues, but require ingestion and missingness analysis. Prior-match goal form, rest days and home/away splits can be derived now; only lagged information is eligible as a pre-match feature. Never use the target match's half-time/full-time statistics as inputs.

## 4. Defects the PR must address

Paths below are relative to `PowerLinesWeb/` unless identified otherwise.

| Priority | Evidence | Required change |
| --- | --- | --- |
| P0 | `Analysis/AnalysisService.cs:25`, `Analysis/CalibrationProvider.cs:15–24` | Calibration lookup has no as-of cutoff. Use versioned artifacts trained only on earlier resolved raw forecasts. |
| P0 | `Accuracy/AccuracyCalculator.cs:45–54`, `Analysis/OddsCalculator.cs:29–41` | Already-calibrated outputs feed the next calibration map. Persist raw probabilities and separate calibration training from reporting buckets. |
| P0 | `Accuracy/AccuracyCalculator.cs:13–36`, `Models/Accuracy.cs:44` | Model loss uses all scored rows; market loss uses priced rows. Compute paired metrics on identical IDs and report separate all-forecast metrics. |
| P0 | `Accuracy/ResultAnalysisBackgroundService.cs:74–80,138–140`, `Accuracy/AccuracyBackgroundService.cs:100–110` | Historical forecasts are overwritten and reports mix runs/windows. Use immutable run outputs and completed-run publication. |
| P0 | `Analysis/OddsCalculator.cs:128–164` | Market disagreement is not offered-price EV. Require `p * odds - 1` above an explicit EV floor for price-based recommendations. |
| P1 | `Messaging/MessageService.cs:79–88,106–122`, `Fixtures/FixtureAnalysisBackgroundService.cs:55–74` | Quote changes and result corrections need their own revisions; `Created` is not an update watermark. Include model/calibration/policy versions in invalidation. |
| P1 | `Analysis/Ratings/TeamRatings.cs:33–40` | Separate ability to publish a forecast from evidence needed to recommend it. Validate total-history/shrinkage and fallback policies. |
| P1 | `Fixtures/FixtureService.cs:13–17` | Unforecastable fixtures disappear. Return all in-scope fixtures with status/reason and nullable estimates. Use an injected clock and explicit kickoff/status policy. |
| P1 | `Analysis/OddsCalculator.cs:89–121`, `Views/Fixtures/Index.cshtml:27–29` | H/A mode, deterministic ties, disjoint tiers, and configured boundary labels. Do not count the same fixture twice. |
| P1 | `Analysis/Ratings/RatingsFitter.cs:48–65`, `Analysis/DixonColes.cs`, `Analysis/GoalDistribution.cs` | Rho bounds cover training pairings; an extreme future pairing may require a tighter bound. Validate per fixture, use a documented feasible correction/fallback, and test nonnegative cells. No observed production failure is claimed. |

Illustrative regression for the value defect: `P(H)=.60, P(D)=.35, P(A)=.05`, offered prices `1.60/3.00/3.00`. De-vig home probability is about `.48387`; disagreement is `.11613`, above .08. Current selection can flag H, but `EV=.60*1.60-1=-.04` and Kelly is zero. This must produce **no actionable value selection**.

Also correct misleading evaluation text: all-home accuracy over every match is not a fair comparator for a selected-favourites subset; compare baselines on the same selections. Log loss and Brier measure overall probability quality, not calibration alone. An empty cohort is unavailable, not a perfect zero-loss result.

## 5. PR boundary and implementation sequence

Suggested web PR title: **Improve win forecast coverage and validate fair odds with chronological evaluation**.

The main PR belongs in `power-lines-web`. Implement the following steps as reviewable commits, with candidate models and selection policies behind configuration. Upstream parser/source-provenance repairs belong in an explicitly linked **companion data-service PR**; a web PR cannot repair another repository by itself. The local fixture service is not in the active Docker stack; do not assume its older analysis code owns current forecasts.

### Step A — Freeze and qualify the data

1. Create a repeatable-read export or consistent database backup for offline evaluation, with source counts, row fingerprint, schema version, code SHA, extraction time and fixed evaluation dates. Read source data only; write evaluation artifacts elsewhere. Do not start the ASP.NET host against the full database to run experiments: startup applies migrations and starts workers.
2. Extend the audit with source season/division counts, duplicates, date/goal validity, team aliases, odds completeness, mapping and overround distributions. Preserve valid score rows with missing/bad quotes; quarantine the quotes separately. Suspicious overround is a review signal, not permission to invent replacement odds.
3. In the data-service companion PR, replace positional parsing with header-name mapping using a proper CSV reader, invariant numeric parsing, explicit supported date formats, optional-column handling and per-row diagnostics. Never delete non-ASCII name characters as an identity strategy. Preserve source season/file hash, row identifier, price kind and collection information.
4. Produce an idempotent staged repair/re-import with a before/after reconciliation report. Fix the 380-row E0 reproduction and audit other no-`Time` schemas. A historical correction must not overwrite a known good value with missing data. Preserve original ingestion time and add meaningful update/revision metadata. Handle renamed/rescheduled fixtures with stable identity/aliases rather than an uncontrolled duplicate insert.
5. Investigate the schedule/message path supplying fresh fixtures/results. Show last successful import and source coverage per division, with season-aware freshness status. Distinguish “no scheduled games”, “feed not updated” and “forecasts unavailable”. The local stack has web/RabbitMQ/PostgreSQL only; importer deployment/schedule must be identified, not assumed.

**Done when:** the snapshot is reproducible, suspect odds are accounted for, recent source samples reconcile, and live freshness failures are visible. The web evaluator can proceed on qualified score data while quote repair is pending; market/ROI claims remain unavailable for unqualified cohorts.

### Step B — Add an offline chronological evaluator and immutable forecast records

Add a small .NET console project, e.g. `PowerLinesWeb.Evaluation`, using the same prediction components without launching web workers. Example target interface (to implement, not an existing command):

```bash
dotnet run --project PowerLinesWeb.Evaluation -- \
  --snapshot /path/to/snapshot \
  --manifest evaluation/manifests/win-odds-v1.json \
  --output artifacts/win-odds-v1
```

The manifest fixes data fingerprint, ordered divisions/match IDs, date folds, candidate recipes, hyperparameter search space, deterministic seed, quality gates and quote conventions **before** final evaluation. Do not derive test dates from `UtcNow`.

Recommended initial schedule for this snapshot, subject to coverage checks before viewing model scores:

| Use | Date boundary |
| --- | --- |
| Model warm-up | Up to six prior years for each forecast date; archive has sufficient depth |
| Generate earlier raw out-of-fold calibration examples | From 2021-08-01, fitting only on earlier dates |
| Development/validation | Rolling blocks from 2022-08-01 through 2025-07-31 |
| Final holdout | 2025-08-01 through 2026-07-31, inclusive |
| Recent diagnostic, separate from final test | 2026-08-01 through available 2026-08-20 results |

Use calendar blocks shared across divisions, not random match splits. At each forecast date `d`, training/feature/calibration outcomes must come strictly from dates `< d` because exact historical result-availability times are missing. For live forecasts, enforce information available by the **forecast creation time** as well as before kickoff; do not use future-fixture time as permission to ingest intervening future information in a replay.

The proposed final period is reserved from tuning in this new implementation. It may already have appeared in earlier repository experiments, so it cannot be claimed to be globally untouched research data. Record that limitation and confirm any selected policy on a newly reserved future period with actual pre-match forecast snapshots after feed restoration. If refreshing data provides a later unused sufficiently large evaluation interval, declare its boundaries before scoring it; do not move the test window to find a better result.

Generate raw forecasts in date order, resolve earlier results, then fit/update a calibrator from earlier **out-of-fold raw** probabilities of the same model recipe. Within development, each validation block's calibrator is trained before that block. For the final holdout freeze model recipe, calibrator parameters, blend weight and selection thresholds at the boundary; allow daily base-rating updates on earlier holdout results only as a declared online update rule. Do not tune after seeing final scores. A later candidate requires a new forward test, not repeated reuse of the same holdout as “untouched”.

Introduce explicit concepts (names may follow existing conventions):

- `EvaluationRun`: dataset hash, recipe/config/code versions, training/test windows, status and completion marker. Publish reports only for completed runs.
- `ForecastSnapshot`: match identity, origin (`LivePrematch` or `HistoricalReplay`), forecast time, model-training cutoff, raw and final H/D/A probabilities, forecast model type, calibration version/cutoff, input revision, support/abstention status. A historical reconstruction is never relabelled as a forecast actually published before kickoff.
- `CalibrationArtifact`: model recipe, fit window/cutoff, sample counts, outcome treatment, parameters and version. Reporting reliability buckets are separate derived outputs.
- `MarketQuote`: match, H/D/A prices or selection-specific bookmaker prices, source/bookmaker, price kind, observation time, availability status and source revision. Historical unknown timestamps stay unknown; `Created` is not a substitute. Average prices can inform benchmarks but are not a specific executable offer.
- `RecommendationDecision`: selection H/A, tier, unrounded probability, fair odds, quote reference if available, market disagreement, offered-price EV, policy version and exclusion reason.

Keep existing `match_odds` as a compatibility/latest projection if useful, but retain immutable published forecasts for settlement and auditing. Mark legacy rows with unknown provenance; do not fabricate historical cutoffs during migration. Add appropriate unique keys/indexes for run+match, quote identity and time-aware calibration lookup.

Output `manifest.json`, per-match `predictions.csv`, metrics by cohort/division/period/tier, exclusions, model comparisons, calibration data and a human-readable report. Sort deterministically; repeated runs on the same snapshot/configuration must agree. Cache fits by division, cutoff, input version and recipe; avoid fitting separately for every fixture or starting massive parameter grids.

### Step C — Repair calibration and compare a small model set

First reproduce the legacy selection policy with calibration explicitly disabled or reconstructed correctly. Saved legacy predictions are diagnostic only. Distinguish correctness fixes from model improvements with ablations.

| Candidate | Purpose / decision |
| --- | --- |
| Smoothed league H/D/A frequencies, learned from prior dates | Simple sanity baseline, including cold-start fallback research. |
| Current two-stage Poisson + fitted rho, raw probabilities | Primary corrected benchmark; keeps current strengths and date boundaries. |
| Independent Poisson, rho fixed at zero | Cheap ablation: establish whether low-score correction helps. |
| Same goals model + chronological calibration | Primary odds-accuracy improvement candidate. Compare no calibration with low-parameter multiclass temperature scaling; evaluate current isotonic only with independent chronological examples and sufficient support. |
| Market-only de-vig probabilities | Required benchmark on qualified priced matches. Preserve price kind/timing in every report. |
| Convex model/market blend | Primary alternative forecast candidate where valid market information exists. Fit weight on validation only; retain goals-only fallback. |
| Regularized multinomial H/D/A challenger | Second experiment if first-round results leave material error: prior-date rating differences, lagged goal form, rest days and league effects; optionally Elo-derived strength. It must justify added code/dependencies in the same harness before publication. |

For temperature scaling, a simple implementable multiclass transform is `p'_k = exp(log(max(p_k, eps))/T) / sum_j exp(log(max(p_j, eps))/T)`, with positive `T` fitted to earlier out-of-fold examples. It preserves normalization and ranking. Prefer global/pool-level shrinkage over separate low-sample league fits; retain identity calibration when unsupported. Compare outcome-specific reliability, especially away wins and strong favourites. Do not lift clipped tails just to cross selection thresholds. Independent calibration data and small-sample isotonic risks are documented in the [calibration guide](https://scikit-learn.org/stable/modules/calibration.html).

For the first market blend use `p_final = alpha * p_goals + (1-alpha) * p_market`, with one validation-selected weight and no-market fallback; test `alpha` in `{0, .25, .5, .75, 1}`. `p_goals` must identify whether it is raw or calibrated, and the same order must be used in evaluation and live inference. Market-assisted output is explicitly identified. Using the market as a predictor does not demonstrate independent betting edge. Closing prices cannot be features for an earlier publication horizon. Legacy timing-unknown quotes support retrospective sensitivity analyses, not an executable betting record.

Do not promote a market-assisted default solely from timing-unknown historical quotes. It needs verified price information available before the declared decision horizon or prospective validation at that horizon. Until then, ship the blend as an experimental/shadow candidate while collecting suitable snapshots.

Use bounded experiments, not a full Cartesian sweep: start from existing half-life 365/prior five/window six; compare half-life `{180,365,730}` and prior `{5,10,20}` in staged development tests, preserving a fixed control. If comparing training windows or class-specific calibrators, predeclare that next experiment. Do not tune per-league dozens of parameters on small samples.

Do not mandate neural networks, bivariate Poisson, a Skellam replacement, paid injury feeds or observed-xG ingestion in this PR. Skellam derived from the same independent Poisson rates is a different computation of goal difference, not automatically a richer forecast. Joint Dixon–Coles fitting is a reasonable later challenger if the approximate fitter is demonstrably limiting performance. Added historical shots/cards need missingness audits and strictly lagged features; the target match's statistics are prohibited.

### Step D — Recover supported coverage and define win-selection policy

Separate `ForecastStatus` from `RecommendationTier`. A valid but uncertain estimate can be displayed without being a strong recommendation.

1. Preserve a legacy eligibility baseline. Compare total prior appearances with existing shrinkage against the venue gate; record recent weighted evidence as well as raw count. If using effective sample size, define it explicitly, e.g. `(sum w)^2 / sum(w^2)`, and also retain total weight/last-match age so many obsolete matches do not look current.
2. Implement a named sparse-history strategy: stronger league-average prior for thin teams, validated by newcomer cohort; alternatively qualified market-only forecasts where available. No-history league-prior estimates are labelled low-support and cannot automatically qualify as strong picks. A pure zero-match goals fit must not produce false 100% certainty.
3. Cross-division rating transfer is optional follow-up after stable team identity and source/destination league offsets can be learned from past transitions only. Do not directly reuse raw ratings across leagues; they are not on a shared scale. This is relevant to the 31 snapshot exclusions, but cannot be promised without evaluation.
4. Default the discovery UI to home/away win candidates. Choose at most one team per fixture from the full H/D/A distribution. Require the selected win to outrank the draw for the shortlist, handle equal H/A deterministically as no recommendation, and retain the unconditional win probability. Never divide `P(H)` by `P(H)+P(A)` to make a win look safer.
5. Keep a strong tier at an explicit inclusive threshold of `.70` initially. Choose a broader shortlist threshold on development folds from `{.50,.55,.60,.625,.65,.675,.70}`. Use disjoint tiers: strong `p >= high`; broader `low <= p < high`; otherwise unselected. The **win shortlist is the union of strong and broader tiers**, counted once per fixture. Publish configured thresholds in the UI, including any boundary change from legacy `>` behavior.
6. Rank shortlist entries by win probability, with support/source, fair odds and market comparison visible. Provide filters for strong, broader shortlist and qualified positive-EV selections. No forced minimum picks and no automatic assumption that stronger win probability means better value.

For quote-based decisions calculate separately:

```text
fair_odds = 1 / p_win
market_disagreement = p_win - p_market_no_margin
expected_return = p_win * offered_decimal_odds - 1
full_kelly = max(0, expected_return / (offered_decimal_odds - 1))
```

Require valid supported forecast, H/A selection, valid quote/source/time, odds range, and `expected_return > MinExpectedReturn >= 0`. Name this floor separately from probability-point `MinEdge`; do not reinterpret `.08` as an EV setting during migration. If stake is displayed, require a positive result and cap the existing fractional-Kelly advice with explicit configuration; staking is not the optimization objective. Account for commission if the source is an exchange.

The price-based list is a separate policy: select at most one H/A outcome per fixture by the highest qualifying EV, with deterministic tie handling. It may differ from the most likely winner and need not be in the strong or broader probability tier. Explain which signal selected it; do not combine the two counts into a claim of additional unique matches.

Where only historical or current average prices with unknown timing exist, show a **market comparison / indicative EV**, with no claim of an available bookmaker bet. Do not fabricate quote provenance to fill the positive-EV list. Capture timestamped actual offers going forward if executable price recommendations are required.

### Step E — Refresh reliably and explain the result in the UI

- Recalculate price-based decisions when a quote changes, without needlessly refitting ratings. Corrected scores invalidate dependent rating/feature caches; model, calibration and policy version changes invalidate their own outputs. Track revisions separately from import debounce timestamps.
- Keep archived pre-match predictions immutable. Retrospective corrected-data experiments use new run IDs. Existing fixture projections can update until kickoff; define postponement/cancellation/rescheduling behavior and never present an in-play fixture as a new pre-match opportunity.
- Show every in-scope fixture, including “insufficient history”, “awaiting analysis”, “price unavailable”, “stale feed”, or “analysis failed” as appropriate. A failed job must not be indistinguishable from no supported forecast. Avoid generic “new fixtures coming soon” when the feed is stale.
- Label reciprocal prices **Model fair odds**; show win probability, market/available price, price age/source, forecast origin/support and tier. Keep technical implementation details in diagnostics rather than the normal user flow.
- Accuracy view: show completed run and date range, all-eligible denominator, forecast coverage, unique H/A selections, paired market/model scores, class/tier calibration, and sample counts. Correct the existing Brier/log-loss tooltip and overlapping lower-tier counts.

## 6. Evaluation, promotion gates and acceptance criteria

### Required measurement

Report source fixtures → valid in-scope matches → forecasts by league/period, then two branches:

- Forecast confidence: forecasts → H/A win shortlist, split into disjoint strong and broader tiers. Quotes are not a prerequisite.
- Price-based selection: forecasts + qualified quotes → positive-EV recommendations. These need not be strong favourites or shortlist entries.

Every excluded row gets one primary reason per branch, with optional diagnostic reasons. Show forecasts/selections per 100 eligible matches and per week, plus overlap/unique fixture counts across branches; do not hide unavailable forecasts from denominators.

Score probability accuracy on all forecastable results and on exact shared comparison cohorts. Report multiclass log loss, multiclass Brier (state sum-over-three-classes convention), mean prediction versus realized frequency, H/D/A reliability curves, and counts. Include probability bins and strong/newcomer segments with uncertainty. Use the same IDs and price snapshot convention for model versus market; separately report newly covered rows that the legacy model cannot price. If a uniform all-eligible score is desired, specify a common fallback instead of rewarding selective abstention.

For shortlist comparisons report unique selections, home/away split, hit rate, mean probability, calibration gap, fair/market price distributions, and incremental added selections. Compute market-favourite and all-home baselines on the same selected matches. Use paired resampling of weekly match blocks (fixed seed, e.g. 2,000 resamples) for loss deltas and policy differences, preserving all outcomes from a match together. Show per-division diagnostics without cherry-picking winning divisions.

ROI is secondary. Report average-price simulations as such; only use known decision-time offers for executable-price returns, with missing quotes excluded explicitly and costs included. Closing-line value requires separate closing snapshots and cannot be reconstructed from current average fields. No historical ROI result is a guarantee.

### Proposed predeclared targets, not promised improvements

| Dimension | Promotion criterion |
| --- | --- |
| Fair-odds model | Lower mean holdout log loss than the corrected incumbent on their common cohort, with paired 95% interval for the delta below zero; Brier not worse by more than 0.005 absolute. Report comparison with market separately; claim market superiority only when its paired evidence supports it. |
| Win shortlist volume | Development objective: at least 25% more unique H/A selections (strong + broader union) than the legacy union of >.65 flags, with hit-rate loss no greater than 3 percentage points. On the fixed final test require the volume target and a paired 95% lower bound for the candidate-minus-baseline hit-rate difference >=-.03; otherwise the wider policy remains experimental. Report the incremental segment. These are initial product tradeoffs, not user-specified guarantees. |
| Shortlist reliability | Report mean-probability minus hit-rate gap and confidence interval for selected and incremental cohorts; target absolute gap <=.05. For promotion, require the final overall shortlist gap interval to stay within [-.05,.05]; unsupported incremental segments remain experimental. Require >=200 selections for an overall tier claim; this is only a minimum, not sufficient proof. Small league/tier cells remain descriptive. |
| Coverage | Increase eligible forecast coverage where supported; report both all-eligible and legacy-common cohorts. A market-only or prior-backed estimate must retain its source/support label. No coverage claim from simply showing more rows. |
| Numerical/data correctness | All active forecasts finite, nonnegative, normalized within declared tolerance; invalid or stale inputs produce explicit unavailable status. No negative/zero-EV quote-based recommendations. |
| Operational | Quote corrections, result corrections and model/config changes refresh the right outputs; fresh feed status and abstentions are visible; completed reports are reproducible. |

The shortlist target is evaluated on both the legacy-common cohort (policy comparison) and the full eligible universe (coverage change). Publish both; do not use an easier expanded cohort to disguise weaker predictions. If uncertainty cannot support an accuracy/precision claim, label it inconclusive and leave the challenger experimental. If no candidate meets the targets, keep the corrected incumbent and document the measured tradeoff; do not lower quality gates after inspecting the holdout.

### Meaningful automated acceptance tests

1. Adding/changing future results, quotes or calibration artifacts cannot change an earlier replay forecast. Same-day outcomes cannot enter a date-only historical forecast.
2. Calibration trains on earlier raw out-of-fold predictions of the matching model recipe; report generation never changes those inputs. Final-holdout data cannot affect parameter/policy selection.
3. Repeated evaluation of a fixed manifest yields identical predictions/metrics. Incomplete runs cannot replace the current published evaluation.
4. Model/market paired comparison has identical IDs; adding an unpriced row does not alter its delta. Empty cohorts are unavailable; invalid outcomes/distributions are rejected with counts.
5. Extreme expected-goal pairings respect feasible rho; every score cell and H/D/A probability is finite/nonnegative and the distribution sums to one. Fair odds are computed from unrounded probabilities, rounding only for display.
6. Sparse/new teams exercise total-history, prior and market-fallback paths. A completely unknown team cannot obtain a strong flag merely from fabricated default ratings. Include a newcomer holdout segment in real-data evaluation.
7. H/A mode cannot choose D, preserves draw probability, produces at most one win selection per fixture, handles ties and exact .65/.70 boundaries, and counts disjoint tiers correctly.
8. The negative-EV example in section 4 never produces a quote-based recommendation. Test zero EV, missing/invalid/stale quotes, absent provenance and any commission calculation.
9. A price-only update refreshes EV/selection; a corrected result refreshes dependent forecasts; changing policy/calibration versions invalidates the appropriate projection. Archived pre-match forecasts remain unchanged.
10. Fixture lists expose unsupported/pending rows and feed status; labels match configured thresholds; UI distinguishes fair odds, average market prices and available offers.
11. Companion importer tests cover missing `Time`, `BbAv*`/`Avg*`/closing/max distinctions, quoted commas, Unicode names, BOM/header variations, blank/truncated rows, invalid dates, nullable half-time data and corrected re-import idempotence. The E0 2018/19 source case must reconcile.

Run ordinary tests with `dotnet test PowerLinesWeb.Tests/PowerLinesWeb.Tests.csproj --configuration Release`; add a documented evaluation command and isolated .NET 8 container smoke check for the implementation. Integration tests use an isolated database/network/queues with workers controlled. Never run the shared development Compose stack as though it were a disposable test fixture.

## 7. Validation completed for this review and limits

- Read current code, tests, configuration and active Docker services; inspected sibling importer code.
- Queried PostgreSQL read-only; saved repeatable-read counts and recommendation/history diagnostics.
- Reconciled the complete E0 2018/19 price triplets to the source CSV.
- Ran the ordinary test command: **81 passed**. Explicit real-database half-life/edge sweeps were not executed. Local SDK was .NET 10 with project-permitted major-runtime roll-forward; this was not a .NET 8 production container smoke test.
- Did **not** run a new complete real-data model tournament, prove predictive improvement/profitability, repair imports, change application logic, or open the implementation PR. These are implementation/evaluation tasks below, not review findings.

README numbers such as 21,076 matches, -9.8% ROI and historical tuning claims were not reproduced. Do not use them to select a model or claim improvement, especially given the chronology/cohort defects. The README also links to older plan files absent from this checkout; this handover is self-contained and does not assume those tasks were completed.

## 8. Copy-ready prompt for Sol

```text
Implement the Power Lines win-recommendation and fair-odds improvement plan in
docs/WIN_RECOMMENDATIONS_AND_ODDS_PLAN.md. Read the entire plan and its three
review-evidence files before editing. The user wants more useful HOME/AWAY win
recommendations and more accurate fair odds, with model changes allowed when
supported by evidence. Prepare a new reviewable PR; do not merge or deploy it.

Workspace: power-lines-web, reviewed at 468d913. Inspect the current branch,
diff and applicable repository instructions first. Preserve unrelated user
edits, especially the already-modified README.md. The old .github architecture
description is stale: the web app owns DB persistence, RabbitMQ consumers,
ratings, analysis and accuracy. Keep applicable style/security conventions.

Start a feature branch/worktree without discarding existing work. Implement
sections 5 A–E and the acceptance tests in section 6 as coherent commits:

1. Read-only data audit/snapshot and deterministic offline evaluator. Do not
   launch the web host against the full DB for experiments: startup migrates
   and starts workers. Use explicit dates/data fingerprints, chronological
   development folds and the protected final holdout specified in the plan.
   Keep all eligible matches in coverage denominators. Save manifest,
   per-match predictions, exclusions and comparison reports.
2. Immutable live/replay forecast records; separate raw/final probabilities;
   model/input/calibration/policy versions and cutoffs; calibration trained
   only on earlier raw out-of-fold predictions; same-ID model/market scores.
   Do not reuse current accuracy_calibration buckets as historical training
   truth or overwrite pre-match forecasts when settling results.
3. Implement and compare the small first-round model set: repaired raw goals
   model, rho=0 ablation, chronological low-parameter calibration, market-only
   baseline and validation-fitted convex market blend with goals-only fallback.
   Keep no-calibration as a candidate. Validate probability/rho invariants.
   Choose production defaults by protected evaluation, not by model novelty.
   The multinomial/Elo challenger and joint Dixon–Coles fitting are secondary
   experiments if justified, not mandatory complexity.
4. Separate forecast eligibility from recommendation eligibility. Evaluate
   total-history/shrinkage and explicit sparse-team/market fallback paths.
   The reviewed fixture snapshot gains NOTHING merely from changing the gate
   to ten or five total matches: 134/165 qualify either way. Measure newcomer
   performance before advertising more supported coverage.
5. Add a ranked H/A win shortlist with disjoint configurable probability tiers
   and validated wider threshold. Keep draw risk in the full distribution,
   at most one selected team per fixture, no forced pick quota. Use legacy
   unique >.65 picks as the comparison, not strong+lower double counting.
6. Distinguish fair odds, market disagreement and offered-price EV. Require
   p*offered_odds-1 > a separately named nonnegative EV floor for actionable
   price-based recommendations, valid quote provenance/time, and positive
   stake if a stake is shown. Historical average quotes are indicative
   comparisons, not fabricated bookmaker offers. Default win mode excludes D.
7. Fix invalidation for changed quotes, corrected results and model/calibration/
   policy changes. Preserve Created as ingestion time and introduce proper
   revisions. Show all fixtures, support/abstention reasons, source freshness,
   probability, model fair odds and distinct market/available prices. Render
   labels from configuration. Publish only completed evaluation runs.

Known facts to reproduce rather than assume away:
- Local DB has 241,380 results/22 divisions, latest 2026-08-20, and 165 August
  21–24 fixtures; no current fixtures on review date 2026-10-31. This does not
  prove a separate production feed is stale. Identify the actual import job.
- 123 stored fixture forecasts, 6 strong and 11 lower INCLUDING strong picks.
  31 fail the history gate; 11 others predate the stored calculation date.
- All result dates lack kickoff time; exclude same-day outcomes in replay.
- E0 2018/19: all 380 'average' price triplets are actually source maxima.
  The sibling power-lines-data-service ResultReader inserts Time into row
  values without shifting header indexes. Repair there in a linked companion
  PR with source reconciliation; do not pretend a web-only change repairs it.
- Existing saved calibration/backtests are not reliable out-of-sample evidence.
- Stored homeXg/awayXg are model outputs, not observed shot-quality xG.

Upstream coordination: inspect the sibling data-service checkout/instructions
and preserve its changes. Prepare the header-mapped parser/provenance fix and
staged idempotent reconciliation as a companion PR when that repository is
available. Keep unqualified odds out of claims meanwhile. Continue independent
web work if external feed deployment or missing source metadata blocks live
coverage; document the exact dependency instead of inventing data. Any live
database migration/re-import/deployment is a separate rollout step after the
reviewable code, reconciliation report and backup/rollback plan exist.

Validation is part of the implementation, not an optional follow-up. Run
meaningful unit/regression tests, isolated persistence/job integration tests,
the fixed-snapshot chronological evaluation and a .NET 8 container smoke test.
Report exact cohorts, forecast/selection coverage, log loss/Brier, calibration,
home/away hit rates, newcomer/incremental selections and uncertainty. Compare
market on the same IDs. Quote-timing-unknown ROI remains a simulation.

Use section 6's predeclared targets and freeze configurations before looking
at final scores. If no challenger passes, retain the corrected incumbent and
keep new candidates experimental; do not relax the targets after the fact or
claim that more low-confidence rows prove better betting recommendations.

Deliver: reviewable code/migrations/configuration, automated tests, reproducible
evaluation command and artifacts, before/after metrics with limitations, clear
data-service dependencies, and PR description explaining behavior, validation,
data backfill/rollout and rollback. No unrelated redesign, paid feed purchase,
bet placement, merge or deployment. Explicitly report any check not run.
```
