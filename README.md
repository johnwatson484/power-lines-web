[![Build Status](https://github.com/johnwatson484/power-lines-web/actions/workflows/build.yaml/badge.svg)](https://github.com/johnwatson484/power-lines-web/actions/workflows/build.yaml)

# Power Lines Web

An ASP.NET Core 8 MVC app that consumes football fixtures/results from RabbitMQ, stores them in PostgreSQL, fits goals models and serves fixture forecasts and published evaluation reports. [Power Lines Data Service](https://github.com/johnwatson484/power-lines-data-service) supplies Football-Data imports; this repo owns persistence, forecasting and the web UI.

## How the repos work together

```mermaid
flowchart LR
    Source[Football-Data CSV files] --> Importer[Power Lines Data Service]
    Importer --> Broker[RabbitMQ: fixtures, results, imports]
    Broker --> Ingestion[MessageService and IngestionService]
    Ingestion --> DB[(PostgreSQL)]
    DB --> Job[FixtureProjectionJob: every minute]
    Job --> Ratings[Division ratings and score probabilities]
    Ratings --> Policy[Calibration and recommendation policies]
    Policy --> Evidence[Forecast snapshots and current projections]
    Evidence --> DB
    DB --> MVC[MVC controllers and Razor views]
    MVC --> Browser[Browser]
    Reports[Completed evaluation report] --> Publisher[EvaluationPublisher]
    Publisher --> DB
```

- At startup, `Program.cs` applies Entity Framework Core migrations before running the host. PostgreSQL must be reachable even with workers disabled.
- `MessageService` subscribes to the configured fixture/result fanout exchanges and the fixed `imports` exchange (`imports-web` subscription). `IngestionService` validates and upserts matches, tracks revisions and archives price observations. Repeated imports preserve stable identities; conflicting identities require explicit resolution.
- `FixtureAnalysisBackgroundService` runs `FixtureProjectionJob` immediately and every minute. The job checks kickoff time, result freshness and history support, then saves append-only forecast evidence alongside the latest `MatchOdds` projection. An input/configuration/quote fingerprint avoids repeating unchanged analysis. Matching completed results create settlement records.
- `/` serves the home page; `/Fixtures` reads current forecasts and feed/price status; `/Accuracy` reads the latest completed evaluation report. Live workers do not run historical experiments or regenerate the accuracy report.

## Forecasting models

The implementation is in [Analysis](PowerLinesWeb/Analysis), with fitted ratings in [Analysis/Ratings](PowerLinesWeb/Analysis/Ratings).

```mermaid
flowchart TD
    History[Earlier results in the same division] --> Fit[Time-weighted attack and defence fit]
    Fit --> XG[Home and away expected goals]
    XG --> Poisson[Poisson score grid]
    Poisson --> DC[Optional Dixon-Coles low-score correction]
    DC --> Normalise[Renormalise and sum home / draw / away]
    Normalise --> Calibration[Optional versioned temperature scaling]
    Calibration --> Fair[Final probabilities and reciprocal fair odds]
    Fair --> Win[Strong or broader home/away shortlist]
    Fair --> Price[Available-offer expected return and Kelly stake]
```

The default model is a Maher/Dixon-Coles goals model. `RatingsFitter` fits attack, defence and a division-wide home advantage by coordinate ascent on the weighted Poisson likelihood, then fits a bounded low-score correlation. Older matches receive exponentially smaller weights (`HalfLifeDays`), and a `PriorMatches` prior pulls sparse ratings towards the division average.

Expected goals are:

```text
home = divisionAverageGoals × homeAttack × awayDefence × homeAdvantage
away = divisionAverageGoals × awayAttack × homeDefence
```

Poisson probabilities form a score grid from zero to `MaxGoals` per side. The Dixon-Coles adjustment affects 0–0, 0–1, 1–0 and 1–1. The truncated grid is renormalised, and summing by result produces home/draw/away probabilities. Fair decimal odds are the reciprocal of each final probability. Expected goals and modal goal counts are also exposed.

| Model option/variant | Behavior |
| --- | --- |
| `UseRho=true` (default) | Apply fitted Dixon-Coles low-score correlation. `false` uses independent Poisson probabilities. |
| `HistoryStrategy=LegacyVenue` (default) | Require at least `MinTeamMatches` home appearances for the home team and away appearances for the away team in the division. |
| `HistoryStrategy=TotalHistory` | Require at least `MinTeamMatches` total appearances per team, regardless of venue. |
| `EnableCalibration=false` (default) | Publish raw probabilities unchanged. When enabled, apply an activated temperature artifact matching the model fingerprint, fitted no later than the forecast cutoff from `EarlierRawOutOfFold` examples. With no eligible artifact, use identity scaling. |

Ratings use results inside `YearsToAnalyse`, played strictly before the information cutoff's calendar date, and created/updated no later than that cutoff. Live forecasts use the current information time. `TemperatureScaling.Fit` supports fitting a temperature from earlier raw out-of-fold examples; the web workers do not train or activate calibration artifacts automatically.

### Recommendations and prices

`RecommendationPolicy.Win` shortlists a unique home or away favorite only when it exceeds the draw probability and has sufficient history support. A probability at least `Threshold__Higher` is **Strong**; one at least `Threshold__Lower` is **Broader**. `X` means no selection. Draws remain part of the probability distribution but are not shortlist selections.

Price recommendations also consider home/away outcomes only. They require a fresh, verified `AvailableOffer` with source, revision, observation time and availability. The policy computes commission-adjusted expected return (`probability × net decimal price − 1`), requires it to exceed `MinExpectedReturn`, and selects the highest eligible return. Stakes use fractional Kelly capped by `MaxStakeFraction`; equal returns abstain. De-margined market probabilities provide a disagreement diagnostic, not the selection threshold.

Imported Football-Data averages/closing/max/bookmaker columns are indicative observations with unknown quote times. Ingestion archives them with `Available=false`; they do not qualify as available offers. `Betting__MinEdge` is retained in configuration/fingerprints but does not gate the current price policy.

### Forecast availability and accuracy

`Feed__FixtureTimeZoneId` defaults to null. Set it only after verifying the source's kickoff timezone, for example `Europe/London` if that is the established feed contract. Missing times, invalid/ambiguous local times or an unset timezone produce “Kickoff time or timezone unverified” rather than a live forecast. Stale result history and insufficient team support also suppress forecasts. Feed freshness checks remain internal; the public fixtures page omits source and import diagnostics.

Accuracy publication uses `EvaluationPublisher`: it accepts completed report JSON only when its SHA-256 fingerprint matches, and stores dataset/manifest/code versions and the test period. `/Accuracy` displays coverage, log loss and Brier score from the latest completed report; lower scoring-rule values are better. A fresh database shows an empty accuracy page until a report is published. This repo contains the publication service, not a bundled evaluator CLI or HTTP publication endpoint.

## Stored and view models

Persistent entities are in [Data](PowerLinesWeb/Data) and [Forecasts/Records.cs](PowerLinesWeb/Forecasts/Records.cs). MVC view models are in [Models](PowerLinesWeb/Models).

```mermaid
erDiagram
    FIXTURE ||--o| MATCH_ODDS : "current projection"
    FIXTURE ||..o{ FORECAST_SNAPSHOT : "fixture identity"
    FORECAST_SNAPSHOT ||..o{ FORECAST_SETTLEMENT : "forecast id"
    RESULT ||..o{ FORECAST_SETTLEMENT : "result revision"
    FIXTURE ||..o{ MARKET_QUOTE : "match identity"
    RESULT ||..o{ MARKET_QUOTE : "match identity"
```

Only the fixture-to-odds link is an EF relationship here; dotted links describe the IDs/identity strings used by application code.

| Entity/table | Purpose |
| --- | --- |
| `Data.Fixture` / `fixtures` | Scheduled matches, source provenance, prices, lifecycle/analysis status and revision. |
| `Data.Result` / `results` | Full-time outcome, optional half-time observations, source metadata, row revision and separate score revision. |
| `Data.MatchOdds` / `match_odds` | Mutable latest fixture probabilities, fair odds, expected/modal goals and shortlist/price decisions. |
| `ImportStatus` / `import_statuses` | Completed source imports and row counts used for feed status. |
| `MarketQuote` / `market_quotes` | Append-only source/price revisions, observation/ingestion times, availability and commission. |
| `ForecastSnapshot` / `forecast_snapshots` | Append-only raw/final probabilities, cutoff, model/input/calibration/policy versions and decision provenance. |
| `ForecastSettlement` / `forecast_settlements` | Append-only links between a forecast and each settled result revision. |
| `CalibrationArtifact` / `calibration_artifacts` | Append-only versioned temperatures, training provenance and activation state. |
| `EvaluationRun` / `evaluation_runs` | Versioned report JSON and test period; completed reports are immutable. |
| `Models.Fixture`, `FixturePage`, `DivisionFeedStatus` | Presentation models combining forecasts, market observations, support and feed status. |
| `AccuracyPage`, `EvaluationCandidateSummary` | Presentation models for a completed report's candidate coverage and scores. |

`AnalysisFixture`, `TeamRatings`/`TeamRating`, `MatchProbabilities`, `GoalDistribution` and `AnalysisMatchOdds` are in-memory analysis models. `IncomingPrice` carries original source price columns during ingestion. The historical `accuracy`, `accuracy_calibration` and `result_match_odds` tables are removed by migrations; current reporting uses evaluation runs and snapshots.

## Dependencies

- Docker Engine/Desktop and Docker Compose v2 or later for the supplied container workflow.
- Alternatively, .NET 8 SDK plus reachable PostgreSQL and RabbitMQ instances for local development.
- Development Compose supplies PostgreSQL (`postgres:11.4-alpine`) and RabbitMQ (`rabbitmq:3-management-alpine`); Dockerfiles use .NET 8 Alpine images and install timezone data.
- NuGet restores ASP.NET Core MVC/Razor runtime compilation, EF Core 8/Npgsql, FlexLabs Upsert, Newtonsoft.Json and `PowerLinesMessaging` 3.1.0. Tests use NUnit. There is no Node/npm build step.
- Builds need registry/NuGet access. The browser layout references external CDN assets. The data service needs outbound HTTPS to Football-Data when importing.

## Run in development

From this repo:

```sh
docker compose up --build -d
docker compose logs -f power-lines-web
```

Compose merges the base and development override, creates the `power-lines` bridge network and starts the web app after RabbitMQ/PostgreSQL health checks pass. The app migrates the database, starts consumers and analyses fixtures every minute. Source is bind-mounted and runs under `dotnet watch` for rebuilds.

| Service | Host address | Development credentials |
| --- | --- | --- |
| Web | `http://localhost:5000` (`/Fixtures`, `/Accuracy`) | No app login configured |
| RabbitMQ AMQP | `localhost:5672` | `rabbitmq` / `rabbitmq` |
| RabbitMQ management | `http://localhost:15672` | `rabbitmq` / `rabbitmq` |
| PostgreSQL | `localhost:6000`, database `power_lines_web` | `postgres` / `postgres` |

Inside Docker, PostgreSQL is `power-lines-web-postgres:5432` and RabbitMQ is `power-lines-message:5672`. Data persists in the `postgres_data` named volume. `docker compose down` stops services and retains the database volume.

Supply additional app settings through the web service's Compose `environment`/`env_file` configuration. For instance, add `FEED__FIXTURETIMEZONEID` after verifying the feed timezone, then recreate the web container. The default null timezone intentionally leaves live forecasts unavailable.

### Import data from the companion repo

With sibling clones and the web stack already running:

```sh
cd ../power-lines-data-service
docker compose build power-lines-data-service
docker compose run --rm --no-deps --entrypoint dotnet power-lines-data-service \
  run --project ./PowerLinesDataService -- --results --all
docker compose run --rm --no-deps --entrypoint dotnet power-lines-data-service \
  run --project ./PowerLinesDataService -- --fixtures
```

Both repos use Compose project name `power-lines`, network `power-lines` and broker container name `power-lines-message`. Let this repo own the running broker and use `--no-deps` for the importer. Start web consumers before imports so their subscription queues exist. Importing results first supplies training history; the next minute's analysis picks up new fixture/input revisions.

### Run locally with .NET

Start only the dependencies through Compose, then override the database port used by the host process:

```sh
docker compose up -d power-lines-message power-lines-web-postgres
ConnectionStrings__PowerLinesWeb='Server=localhost;Port=6000;Database=power_lines_web;User Id=postgres;Password=postgres;' \
ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS=http://localhost:5000 \
  dotnet run --project PowerLinesWeb
```

The checked-in appsettings already point RabbitMQ to `localhost:5672`. Stop any existing web container first to free port 5000 and avoid duplicate workers. `Workers__Enabled=false` disables both consumers and live analysis when running a read-only UI against an existing development database; startup migrations still run.

The Docker attach configuration in [.vscode/launch.json](.vscode/launch.json) connects the VS Code C# debugger to `/vsdbg/vsdbg` in the development container. Select the web process when prompted.

### Production image

The base Compose file builds only the web image; provision PostgreSQL/RabbitMQ and configure reachable hosts separately:

```sh
docker compose -f docker-compose.yaml build
docker run --rm -p 5000:5000 --env-file /path/to/web.env power-lines-web
```

Set the database connection string and message credentials in `web.env`. The image listens on HTTP port 5000 and uses the Production environment. Put it behind the deployment's HTTPS termination/proxy configuration. The base file supplies no dependencies, published host ports or development overrides. The `master` workflow publishes Docker Hub images and a versioned GitHub release, then triggers Render deployment.

## Environment variables and defaults

.NET maps `__` to nested keys, case-insensitively: `Model__MaxGoals` and `MODEL__MAXGOALS` both bind to `Model:MaxGoals`. Environment settings override `appsettings.json` and optional `appsettings.{Environment}.json`; command-line configuration has higher precedence. The tables show effective checked-in app defaults. A shell `.env` is not loaded automatically by .NET; Compose interpolation also does not pass arbitrary variables through to the app. Declare them in `environment`/`env_file`, or use `docker run --env-file`.

### Connections and hosting

| Environment variable | Default | Purpose / development override |
| --- | --- | --- |
| `ConnectionStrings__PowerLinesWeb` | `Server=localhost;Port=5432;Database=power_lines_web;User Id=postgres;Password=postgres;` | PostgreSQL connection. Compose changes host to `power-lines-web-postgres`; a host process using Compose PostgreSQL needs port `6000`. |
| `Message__Host` | `localhost` | Compose sets `power-lines-message`. |
| `Message__Port` | `5672` | AMQP port. |
| `Message__Username` | `rabbitmq` | Broker username. |
| `Message__Password` | `rabbitmq` | Broker password. |
| `Message__FixtureQueue` | `fixtures` | Fixture fanout exchange; match the importer. |
| `Message__FixtureSubscription` | `fixtures-web` | Fixture consumer subscription queue. |
| `Message__ResultQueue` | `results` | Result fanout exchange; match the importer. |
| `Message__ResultSubscription` | `results-web` | Result consumer subscription queue. |
| `Workers__Enabled` | `true` | Enable RabbitMQ consumers and the one-minute fixture analysis worker. |
| `AllowedHosts` | `*` | ASP.NET Core host filtering. |
| `ASPNETCORE_ENVIRONMENT` | `Production` when unset | Dockerfiles set `development` / `production`; selects environment-specific settings and error handling. |
| `DOTNET_ENVIRONMENT` | Unset | Also selects the web host environment; takes precedence over `ASPNETCORE_ENVIRONMENT` with `WebApplication.CreateBuilder`. |
| `ASPNETCORE_URLS` | No repo override for local CLI; framework default is `http://localhost:5000` | Both image stages explicitly set `http://*:5000`. |

The `imports` exchange and `imports-web` subscription are fixed in code. Dockerfile build argument `PORT` defaults to `5000` and controls exposed-port metadata; it does not change `ASPNETCORE_URLS` or the Compose `5000:5000` mapping. Development also exports `PORT=5000`, but application code does not read it to choose the listener.

### Goals model

| Environment variable | Default | Meaning |
| --- | --- | --- |
| `Model__Version` | `goals-corrected-v1` | Version label included with the full options fingerprint. |
| `Model__HistoryStrategy` | `LegacyVenue` | Venue-specific support; `TotalHistory` uses total appearances. |
| `Model__UseRho` | `true` | Apply fitted Dixon-Coles correction. |
| `Model__EnableCalibration` | `false` | Use an eligible activated temperature artifact if available. |
| `Model__MaxGoals` | `10` | Maximum goals per side in the renormalised score grid. |
| `Model__MinTeamMatches` | `10` | Minimum history for each team under the chosen strategy. |
| `Model__YearsToAnalyse` | `6` | Training-history lookback in years. |
| `Model__BacktestYears` | `3` | Lookback used by the manual half-life diagnostic; does not schedule report generation. |
| `Model__MaxOdds` | `1000` | Zero-probability fallback in the odds calculator; positive fair odds use exact reciprocals. |
| `Model__HalfLifeDays` | `365` | Exponential weighting half-life; `0` disables decay. |
| `Model__PriorMatches` | `5` | Prior strength pulling ratings towards the league average. |
| `Model__MaxIterations` | `100` | Maximum coordinate-ascent iterations. |
| `Model__Tolerance` | `0.000001` | Fit/search convergence tolerance. |
| `Model__MinCalibrationPredictions` | `30` | Retained only in serialized fingerprints; no longer controls calibration. |

### Recommendation policy

| Environment variable | Default | Meaning |
| --- | --- | --- |
| `Threshold__Version` | `win-v1-conservative` | Shortlist policy version label. |
| `Threshold__Higher` | `0.70` | Inclusive Strong probability threshold. |
| `Threshold__Lower` | `0.65` | Inclusive Broader probability threshold. |
| `Betting__MinExpectedReturn` | `0` | Strict minimum commission-adjusted expected return for an available offer. |
| `Betting__MinEdge` | `0.08` | Legacy configuration/fingerprint value; current policy reports disagreement but does not gate on this value. The class initializer alone is `0.05`; appsettings overrides it to `0.08`. |
| `Betting__KellyFraction` | `0.25` | Fraction of full Kelly stake. |
| `Betting__MaxStakeFraction` | `0.05` | Maximum fraction of bankroll for a selection. |
| `Betting__MinOdds` | `1.2` | Minimum eligible decimal offer price. |
| `Betting__MaxOdds` | `10` | Maximum eligible decimal offer price. |
| `Betting__MaxQuoteAgeMinutes` | `30` | Maximum age of an observed available offer. |

Policy validation requires `0.5 <= Lower <= Higher <= 1`, nonnegative minimum expected return, Kelly/stake fractions in `(0, 1]`, `MinOdds > 1`, `MaxOdds >= MinOdds` and positive quote age.

### Feed and logging

| Environment variable | Default | Meaning |
| --- | --- | --- |
| `Feed__FixtureTimeZoneId` | null/unset | Verified source kickoff timezone; required for live prematch forecasts. |
| `Feed__MaxImportAgeHours` | `48` | Successful-import age threshold for displayed feed status. |
| `Feed__MaxResultAgeDaysInSeason` | `21` | Result freshness threshold; also suppresses analysis outside season-break months. |
| `Feed__SeasonBreakMonths__0` | `6` | First month exempt from in-season result-age checks. |
| `Feed__SeasonBreakMonths__1` | `7` | Second exempt month. |
| `Feed__SeasonBreakMonths__2` | `8` | Third exempt month. Array entries bind by numeric index. |
| `Logging__LogLevel__Default` | `Information` | Default log level. |
| `Logging__LogLevel__Microsoft` | `Warning` | Microsoft category level. |
| `Logging__LogLevel__Microsoft.Hosting.Lifetime` | `Information` | Host lifecycle level. |
| `Logging__LogLevel__Npgsql.EntityFrameworkCore.PostgreSQL` | `Critical` | Npgsql EF category level. |
| `Logging__LogLevel__Microsoft.EntityFrameworkCore` | `Critical` | EF category level. |

Logging category names containing dots require an env-file entry, Compose mapping or `env 'Logging__LogLevel__Microsoft.EntityFrameworkCore=Information' ...`, rather than a normal shell assignment.

The development broker sets `RABBITMQ_DEFAULT_USER=rabbitmq`, `RABBITMQ_DEFAULT_PASS=rabbitmq`, `RABBITMQ_DEFAULT_VHOST=/` and `RABBITMQ_ERLANG_COOKIE=rabbitmq`. PostgreSQL sets `POSTGRES_DB=power_lines_web` and `POSTGRES_PASSWORD=postgres`; the Compose file also sets `POSTGRES_USERNAME=postgres`, but the image's user setting is `POSTGRES_USER`, whose default is already `postgres`. These are dependency-container settings, separate from the app connection variables.

## Test

Run the normal NUnit suite with the .NET SDK:

```sh
dotnet test PowerLinesWeb.Tests --configuration Release
```

The normal suite covers ratings/Poisson/Dixon-Coles calculations, synthetic holdout forecasts, scoring and recommendation policy. PostgreSQL integration tests skip unless `POWERLINES_TEST_DB` is set; messaging integration tests additionally require `POWERLINES_TEST_RABBITMQ` pointing to a disposable broker. The explicit real-data half-life diagnostic is excluded by default.

For the SDK-container workflow:

```sh
docker compose -f docker-compose.test.yaml build
docker compose -f docker-compose.test.yaml up --abort-on-container-exit --exit-code-from power-lines-web-test
```

This test container runs the normal suite without starting RabbitMQ or PostgreSQL. Both projects write outputs to repository-level `bin/` and `obj/` via `Directory.Build.props`.

### PostgreSQL integration tests

`POWERLINES_TEST_DB` defaults to unset. Persistence tests truncate application tables and migration tests exercise upgrades/rollbacks, so point it at a disposable database whose name contains `power_lines_test`. Use a separate PostgreSQL container, for example:

```sh
docker run --name power-lines-readme-tests -d \
  -e POSTGRES_DB=power_lines_test -e POSTGRES_PASSWORD=postgres \
  -p 127.0.0.1:6001:5432 postgres:11.4-alpine
# Wait until this reports that PostgreSQL is accepting connections.
docker exec power-lines-readme-tests pg_isready -U postgres -d power_lines_test

POWERLINES_TEST_DB='Host=localhost;Port=6001;Database=power_lines_test;Username=postgres;Password=postgres' \
  dotnet test PowerLinesWeb.Tests --configuration Release

docker rm -f power-lines-readme-tests
```

The optional `[Explicit]` `HalfLifeDaysSweepTests` diagnostic reads historical results using a hard-coded `localhost:6000/power_lines_web` connection, not `POWERLINES_TEST_DB`. It is excluded from normal tests and is not the evaluator that publishes completed accuracy reports. Inspect its source before deliberately invoking it against a separate historical-data copy.
