[![Build Status](https://github.com/johnwatson484/power-lines-web/actions/workflows/build.yaml/badge.svg)](https://github.com/johnwatson484/power-lines-web/actions/workflows/build.yaml)

# Power Lines Web
Web front end

# Prerequisites
- Docker
- Docker Compose

# Running the application
The application is intended to be run and developed within a container.  A set of docker-compose files exist to support this.

## Run production application in container

```
docker-compose -f docker-compose.yaml build
docker-compose -f docker-compose.yaml up
```

## Develop application in container

The service is dependent on a message broker. For development a RabbitMQ container is provided.

```
docker network create power-lines
docker-compose build
docker-compose up
```

## Debug application in container
Running the above development container configuration will include a remote debugger that can be connected to using the example VS Code debug configuration within `./vscode`.

The `${command:pickRemoteProcess}` will prompt for which process to connect to within the container.  

Local changes to code files will automatically trigger a rebuild and restart of the application within the container.

## Run tests
Unit tests are written in NUnit.

```
docker-compose -f docker-compose.test.yaml build
docker-compose -f docker-compose.test.yaml up
```

They can also be run directly.

```
dotnet test
```

# How odds are generated

Odds come from a goals model fitted to past results in the same division, not from any market data.

1. **Fit team ratings.** For each division, a Maher/Dixon-Coles model is fitted to results by maximum
   likelihood. Every team gets an attack rating and a defence rating, and the division gets a single home
   advantage. The fit is a coordinate ascent: attack, then defence, then home advantage, each updated to the
   closed-form maximum of the weighted Poisson log-likelihood given the others, repeated until it settles.
   Older matches count for less through an exponential half life, and thin-sample teams are pulled towards
   the league average by a prior worth a few matches.
2. **Expected goals.** Home goals are `average × homeAttack × awayDefence × homeAdvantage`, away goals are
   `average × awayAttack × homeDefence`.
3. **Score grid.** Poisson probabilities for each side are combined into a grid of scorelines up to
   `MaxGoals` each. Independent Poisson understates draws, so the four lowest scorelines are rescaled by the
   Dixon-Coles correction, whose correlation is itself fitted by a bounded search. The grid is truncated, so
   it is renormalised back to a total of one.
4. **Result probabilities and odds.** Summing the grid by result gives the 1X2 probabilities, and the odds
   shown are their reciprocals. A team without enough history in the division is not priced at all, rather
   than being priced from no evidence.

Fixtures and results go through the same code. Results are re-priced using only matches played strictly
before them, so backtested accuracy is measured out of sample.

# Recommendations vs value

Two separate signals are shown, and they answer different questions.

- **Recommended** is the most likely result, flagged when its probability clears `Threshold:Higher`
  (or `Threshold:Lower` for the weaker flag). It says nothing about price. A 75% favourite is a bad bet at
  1.20 and a good one at 1.60.
- **Value** compares the model's probability against the bookmakers' own price with the margin removed. It
  is flagged when the model's edge clears `Betting:MinEdge`, and it carries a fractional Kelly stake as a
  percentage of bankroll. This is the signal that reflects whether a bet is worth making, and it is the only
  route by which a draw can ever be selected, since a draw is rarely the most likely single result.

Value depends on the fixture feed carrying prices. Where prices are missing, no value is flagged and the
rest of the analysis is unaffected.

# Accuracy metrics

Hit rate alone rewards confidence rather than correctness, so the accuracy page also reports proper scoring
rules measured over every analysed match.

- **Log loss** and **Brier score** measure how well calibrated the probabilities are. Lower is better.
- **Market log loss** is the same measure applied to the bookmakers' de-margined prices on the same matches.
  It is the bar to beat, and beating it is hard.
- **Always home** is the share of matches the home side won, which is the do-nothing baseline any set of
  recommendations has to clear.
- **Value ROI** is the return from level stakes on every selection flagged as value.
- **Calibration** buckets every published probability and records how often those forecasts actually came in,
  so a model that says 70% can be checked for being right 70% of the time.

# Configuration

## `Model`
| Setting | Purpose |
| --- | --- |
| `MaxGoals` | Highest scoreline modelled per side. The grid is renormalised after truncation. |
| `MinTeamMatches` | Matches a team needs in the division before it is priced at all. |
| `YearsToAnalyse` | How far back results are read when fitting. |
| `BacktestYears` | How far back results are re-priced to measure accuracy. |
| `MaxOdds` | Price used for an outcome too unlikely to quote, so it reads as unbackable rather than free. |
| `HalfLifeDays` | Days after which a match counts for half as much. `0` disables decay. |
| `PriorMatches` | Strength of the pull towards league average for thin-sample teams, in matches. |
| `MaxIterations` / `Tolerance` | Stopping conditions for the fit. |

## `Betting`
| Setting | Purpose |
| --- | --- |
| `MinEdge` | Model probability must exceed the de-margined market probability by this much. |
| `KellyFraction` | Fraction of the full Kelly stake to advise. |
| `MinOdds` / `MaxOdds` | Price range considered backable. |

## `Threshold`
| Setting | Purpose |
| --- | --- |
| `Higher` | Probability above which the most likely result is recommended. |
| `Lower` | Probability above which it is weakly recommended. |

# Known limitations and outstanding work

1. `HalfLifeDays` defaults to 365 but has not been tuned against real out-of-sample log loss. The backtest
   in the test project runs on synthetic data only.
2. Promoted and relegated teams have no rating in their new division, so odds are suppressed until
   `MinTeamMatches` is met. Transferring ratings across divisions with an offset is the proper fix.
3. Ratings are fitted per division in isolation, so attack and defence values are not comparable between
   divisions.
4. Goals model only. Nothing accounts for injuries, suspensions, fixture congestion, managerial change or
   motivation.
5. Goals are assumed independent beyond the Dixon-Coles low-score correction. Bivariate Poisson and Skellam
   alternatives are untested.
6. Market prices are averages across bookmakers taken at an unknown time. Measuring value against the
   closing best price would be more honest, and there is no closing line value tracking.
7. The margin is removed proportionally, which assumes no favourite-longshot bias. Shin and power methods
   are more accurate and untested here.
8. 1X2 only. Over/under, both teams to score and correct score are all computable from the same grid but
   are not modelled.
9. No bankroll ledger. Kelly stakes are advisory percentages and nothing is tracked.
10. Half time goals are stored but unused.
11. Accuracy is recomputed from scratch for every division on every trigger.
12. Team names are matched by exact string across services, so an upstream rename silently splits a team's
    history.
13. Background service failures surface only as console output, with no alerting.
14. Model parameters are fitted from data but the configuration around them is tuned by hand and not
    revisited as new seasons arrive.

# Responsible gambling

This is a modelling exercise, not betting advice. The model is frequently wrong, the market is efficient,
and a positive expected value on paper is not a guarantee of anything. Never stake money you cannot afford
to lose. Help is available at [BeGambleAware](https://www.begambleaware.org).
