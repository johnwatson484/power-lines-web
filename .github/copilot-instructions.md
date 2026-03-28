# Power Lines Web — Project Guidelines

## Architecture

ASP.NET Core 8 MVC app that consumes two downstream microservices (Fixtures, Accuracy) via HTTP. It is purely a UI tier — no database, no business logic. Each domain has its own folder under `PowerLinesWeb/` containing the API client, interface, and options class alongside the corresponding controller, model, and view.

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
# Docker (development — hot reload)
docker-compose -f docker-compose.yaml -f docker-compose.override.yaml up

# dotnet CLI
dotnet run --project PowerLinesWeb/PowerLinesWeb.csproj
```

## Security

- Do not remove or weaken the security headers middleware in `Program.cs` (CSP, HSTS, X-Frame-Options, X-XSS-Protection, Referrer-Policy, Permissions-Policy)
- Alpine-based Docker images; always run as non-root user (`dotnet:1000`)
