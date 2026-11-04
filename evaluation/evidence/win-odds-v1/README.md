# Fixed-snapshot evidence

`report.md` and `report.json` contain the protected results; `development-comparison.json` records selection before holdout scoring. The original `frozen-selection.json` and its SHA-256 were reused unchanged for subsequent reproducibility checks. `reproducibility.json` pins uncompressed prediction/report/reliability hashes and the tested model-code commit. `validation.json`, `broker-smoke.json` and `staged-repair-test.json` distinguish isolated checks from shared rollout actions.

`snapshot.jsonl.gz` and `qualified-quotes.json.gz` are the exact frozen inputs; the manifest hashes refer to their decompressed bytes. `predictions.csv.gz` is the deterministic per-match output, including abstentions, raw/final probabilities, cutoffs, support and quote exclusions. Historical source averages have unknown observation time and are not available bookmaker offers.

The full raw development/final caches are intentionally retained locally under `artifacts/` rather than duplicated here. The original run's `checksums.json` describes that complete generated directory, including uncompressed predictions and raw caches. Reproduce into a new output directory before using `--publish`; this compressed review bundle is not itself a publishable complete run directory.

From a .NET 8 checkout root, run `python3 scripts/reproduce-evaluation.py --output artifacts/reproduce-locked-final`. It reproduces the frozen final policy and verifies hashes without launching the web host or touching a database. See `docs/WIN_ODDS_IMPLEMENTATION.md` for full protocol, metrics, limits and rollout.
