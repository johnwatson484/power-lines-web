using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using PowerLinesWeb.Configuration;

namespace PowerLinesWeb.Tests.Configuration;

[NonParallelizable]
public class LocalEnvironmentTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void Root_env_is_loaded_from_repo_and_project_directories_with_platform_and_cli_precedence(bool projectDirectory)
    {
        var previousDirectory = Directory.GetCurrentDirectory();
        const string key = "POWERLINES_ENV_TEST";
        var previous = Environment.GetEnvironmentVariable(key);
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(path, "PowerLinesWeb"));
        try
        {
            System.IO.File.WriteAllText(Path.Combine(path, ".env"), key + "=from-file\n");
            System.IO.File.WriteAllText(Path.Combine(path, "PowerLinesWeb", "PowerLinesWeb.csproj"), "");
            Directory.SetCurrentDirectory(projectDirectory ? Path.Combine(path, "PowerLinesWeb") : path);
            Environment.SetEnvironmentVariable(key, null);
            LocalEnvironment.Load();
            Assert.That(Environment.GetEnvironmentVariable(key), Is.EqualTo("from-file"));
            Environment.SetEnvironmentVariable(key, "from-platform");
            LocalEnvironment.Load();
            Assert.That(Environment.GetEnvironmentVariable(key), Is.EqualTo("from-platform"));
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables()
                .AddCommandLine(["--" + key + "=from-cli"]).Build();
            Assert.That(configuration[key], Is.EqualTo("from-cli"));
        }
        finally
        {
            Directory.SetCurrentDirectory(previousDirectory);
            Environment.SetEnvironmentVariable(key, previous);
            Directory.Delete(path, true);
        }
    }

    [Test]
    public void Platform_environment_wins_when_key_casing_differs()
    {
        const string key = "POWERLINES_ENV_CASE_TEST";
        var lower = key.ToLowerInvariant();
        var previous = Environment.GetEnvironmentVariable(key);
        var previousLower = Environment.GetEnvironmentVariable(lower);
        var previousDirectory = Directory.GetCurrentDirectory();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            System.IO.File.WriteAllText(Path.Combine(path, ".env"), lower + "=from-file\n");
            Directory.SetCurrentDirectory(path);
            Environment.SetEnvironmentVariable(lower, null);
            Environment.SetEnvironmentVariable(key, "from-platform");
            LocalEnvironment.Load();
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            Assert.That(configuration[key], Is.EqualTo("from-platform"));
        }
        finally
        {
            Directory.SetCurrentDirectory(previousDirectory);
            Environment.SetEnvironmentVariable(key, previous);
            Environment.SetEnvironmentVariable(lower, previousLower);
            Directory.Delete(path, true);
        }
    }

    [Test]
    public void Missing_env_is_optional_and_parent_env_is_not_loaded()
    {
        var previousDirectory = Directory.GetCurrentDirectory();
        const string key = "POWERLINES_PARENT_ENV_TEST";
        var previous = Environment.GetEnvironmentVariable(key);
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(path, "child"));
        try
        {
            System.IO.File.WriteAllText(Path.Combine(path, ".env"), key + "=unrelated\n");
            Directory.SetCurrentDirectory(Path.Combine(path, "child"));
            Environment.SetEnvironmentVariable(key, null);
            Assert.DoesNotThrow(LocalEnvironment.Load);
            Assert.That(Environment.GetEnvironmentVariable(key), Is.Null);
        }
        finally
        {
            Directory.SetCurrentDirectory(previousDirectory);
            Environment.SetEnvironmentVariable(key, previous);
            Directory.Delete(path, true);
        }
    }
}
