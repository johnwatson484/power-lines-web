---
description: "Use when adding a new domain, controller, API client, model, or view to Power Lines Web. Covers project structure, naming, C# style, configuration binding, and Docker/Helm conventions for this ASP.NET Core 8 MVC app."
applyTo: "PowerLinesWeb/**/*.cs, PowerLinesWeb/**/*.cshtml, helm/**, Dockerfile"
---

# Power Lines Web — Project Conventions

## Domain Folder Structure

Each new domain (e.g. `Predictions`) gets its own folder under `PowerLinesWeb/`:

```
PowerLinesWeb/
  {Domain}/
    {Domain}Api.cs          # Concrete HTTP client implementation
    I{Domain}Api.cs         # Interface
    {Domain}Options.cs      # IOptions<T> config class
  Controllers/
    {Domain}Controller.cs
  Models/
    {Entity}.cs
  Views/
    {Domain}/
      Index.cshtml
```

## C# Style

- Use **file-scoped namespaces**: `namespace PowerLinesWeb.{Domain};`
- Use **primary constructors** with explicit readonly field backing:
  ```csharp
  public class FixturesController(IFixtureApi fixtureApi) : Controller
  {
      readonly IFixtureApi fixtureApi = fixtureApi;
  }
  ```
- Root namespace: `PowerLinesWeb`; domain sub-namespaces: `PowerLinesWeb.{Domain}`

## Configuration (Options Pattern)

1. Add a section to `appsettings.json`:
   ```json
   "{Domain}Url": {
     "Endpoint": "http://localhost:500X",
     "{Domain}": "{route-path}"
   }
   ```
2. Create `{Domain}Options.cs` with matching public string properties.
3. Register in `Program.cs`:
   ```csharp
   builder.Services.Configure<{Domain}Options>(
       builder.Configuration.GetSection("{Domain}Url"));
   builder.Services.AddScoped<I{Domain}Api, {Domain}Api>();
   ```

## API Client Pattern

- Inject `IOptions<{Domain}Options>` in the constructor; store `options.Value`.
- Create `HttpClient` per request.
- Deserialize with `JsonConvert.DeserializeObject<T>(content)` (Newtonsoft.Json).
- Wrap calls in `try/catch`; log with `Console.WriteLine` and return empty list on failure.

```csharp
public async Task<List<MyModel>> GetAsync()
{
    try
    {
        var client = new HttpClient();
        var response = await client.GetAsync($"{options.Endpoint}/{options.Route}");
        var content = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<List<MyModel>>(content) ?? [];
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
        return [];
    }
}
```

## Controllers

- Inherit from `Controller`; use primary constructor.
- Action methods return `IActionResult`.
- Call API via `Task.Run(() => api.GetAsync()).Result` (consistent with existing pattern).
- Pass model directly to `View()`.

## Models

- Plain POCOs — properties only, no business logic.
- Use `[Display(Name = "...")]` for user-facing labels.
- Use `[DisplayFormat(DataFormatString = "...")]` for formatting (dates: `{0:dd/MM/yyyy HH:mm}`, percentages: `{0:P0}`).

## Views

- Razor views use `@model` directive and Bootstrap 4 for layout.
- Shared layout in `Views/Shared/_Layout.cshtml`.
- Keep domain-specific JS in `wwwroot/js/{domain}.js`; use jQuery for event handling.

## Security

- Do **not** remove the security headers middleware in `Program.cs` (CSP, HSTS, X-Frame-Options, etc.).
- Do not loosen `AllowedHosts` beyond `"*"` in development appsettings.

## Docker

- Use multi-stage Alpine builds: `sdk:8.0-alpine` (build) → `aspnet:8.0-alpine` (runtime).
- Run as non-root user (`dotnet:1000`).
- Expose port 5000; set `ASPNETCORE_URLS=http://+:5000`.

## Helm

- Chart inherits from `helm-library`; add new environment variables under `values.yaml` `env:` block.
- Service endpoints follow the pattern: `http://power-lines-{service}-service`.
