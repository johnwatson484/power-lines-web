namespace PowerLinesWeb.Configuration;

public static class LocalEnvironment
{
    // dotnet run uses the project directory; VS Code and scripts may use the repo root.
    // Inspect only these two locations, never unrelated ancestor .env files.
    public static void Load()
    {
        var directory = Directory.GetCurrentDirectory();
        if (System.IO.File.Exists(Path.Combine(directory, "PowerLinesWeb.csproj")))
            directory = Path.GetFullPath(Path.Combine(directory, ".."));
        // .NET configuration keys are case-insensitive, including on Unix hosts.
        var platformKeys = Environment.GetEnvironmentVariables().Keys.Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in DotNetEnv.Env.NoEnvVars().Load(Path.Combine(directory, ".env")))
            if (!platformKeys.Contains(entry.Key)) Environment.SetEnvironmentVariable(entry.Key, entry.Value);
    }
}
