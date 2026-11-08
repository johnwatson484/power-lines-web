# Power Lines Web — Project Guidelines

## Architecture

ASP.NET Core 10 MVC application that owns PostgreSQL persistence, RabbitMQ consumers, ratings, live analysis and accuracy publication. The web host applies migrations and starts workers; never use it against the full database for offline experiments. Use a separate worktree when a development container watches the source checkout. Tests and smoke runs must use isolated databases/networks/queues.

Forecast evidence is append-only; latest fixture projections, score revisions and quote revisions are separate. Calibration reporting buckets are never historical training truth. Only completed, fingerprint-checked evaluation runs may be published.

See `.github/instructions/aspnet-conventions.instructions.md` for full conventions when adding domains, controllers, API clients, models, or views.

## Code Style

- C# with file-scoped namespaces (`namespace PowerLinesWeb.{Domain};`)
- Primary constructors with explicit readonly field backing — no `private readonly` boilerplate
- Root namespace: `PowerLinesWeb`; domain sub-namespaces: `PowerLinesWeb.{Domain}`
- Models are plain POCOs with `[Display]` / `[DisplayFormat]` attributes — no logic

## Configuration

Service endpoints are injected via the Options pattern:
- Each service has a section in `appsettings.json` with `Endpoint` and a path property
- Bound through `{Domain}Options` and registered with `builder.Services.Configure<T>()`

## Build & Run

```bash
# Native development with dependency containers
cp -n .env.example .env
./scripts/local

# Optional Docker app (development — hot reload)
docker compose --profile app up --build power-lines-web

# Tests own disposable dependencies through Testcontainers
dotnet test
dotnet test --filter 'TestCategory!=Integration'
```

## Security

- Do not remove or weaken the security headers middleware in `Program.cs` (CSP, HSTS, X-Frame-Options, X-XSS-Protection, Referrer-Policy, Permissions-Policy)
- Alpine-based Docker images; always run as non-root user (`dotnet:1000`)
