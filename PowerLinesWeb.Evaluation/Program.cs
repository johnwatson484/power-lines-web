using System.Diagnostics;
using System.IO.Compression;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using PowerLinesWeb.Evaluation;
using PowerLinesWeb.Forecasts;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

if (args.Length == 2 && args[0] == "--publish") { PublishCommand.Run(args[1]); return; }

var arguments = args.Chunk(2).ToDictionary(x => x[0], x => x.Length == 2 ? x[1] : throw new ArgumentException("Arguments require values"));
string Required(string name) => arguments.TryGetValue(name, out var value) ? value : throw new ArgumentException($"Missing {name}");
var snapshotPath = Required("--snapshot"); var manifestPath = Required("--manifest"); var output = Required("--output"); var quotesPath = Required("--quotes");
var phase = arguments.GetValueOrDefault("--phase", "full");
if (phase is not ("full" or "development" or "final")) throw new ArgumentException("phase: full, development, final");
Directory.CreateDirectory(output);
if (System.IO.File.Exists(Path.Combine(output, "COMPLETE"))) throw new InvalidOperationException("Completed runs are immutable; choose a new output directory");
var manifestText = System.IO.File.ReadAllText(manifestPath);
var manifest = JsonSerializer.Deserialize<EvaluationManifest>(manifestText, Replay.Json) ?? throw new FormatException("Invalid manifest");
string Sha(string path) => Convert.ToHexString(SHA256.HashData(System.IO.File.ReadAllBytes(path))).ToLowerInvariant();
string DataSha(string path)
{
    using var file = System.IO.File.OpenRead(path);
    using Stream stream = path.EndsWith(".gz") ? new GZipStream(file, CompressionMode.Decompress) : file;
    return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
}
string ReadText(string path)
{
    using var file = System.IO.File.OpenRead(path);
    using Stream stream = path.EndsWith(".gz") ? new GZipStream(file, CompressionMode.Decompress) : file;
    using var reader = new StreamReader(stream); return reader.ReadToEnd();
}
if (DataSha(snapshotPath) != manifest.SnapshotSha256 || DataSha(quotesPath) != manifest.QuotesSha256) throw new InvalidOperationException("Snapshot/quote fingerprint differs from frozen manifest");
if (manifest.DevelopmentFrom >= manifest.HoldoutFrom || manifest.HoldoutFrom > manifest.HoldoutThrough || manifest.DevelopmentBlocks.Any(x => x >= manifest.HoldoutFrom)) throw new ArgumentException("Invalid chronological boundaries");
var manifestHash = Sha(manifestPath);
string ReadCodeSha()
{
    try
    {
        using var code = Process.Start(new ProcessStartInfo("git", "rev-parse HEAD") { RedirectStandardOutput = true, RedirectStandardError = true });
        var sha = code.StandardOutput.ReadToEnd().Trim(); code.WaitForExit();
        return code.ExitCode == 0 ? sha : "Unavailable; provide --code-sha";
    }
    catch (System.ComponentModel.Win32Exception) { return "Unavailable; provide --code-sha"; }
}
var codeSha = arguments.GetValueOrDefault("--code-sha") ?? ReadCodeSha();
var sourceFiles = Directory.GetFiles("PowerLinesWeb/Analysis", "*.cs", SearchOption.AllDirectories).Concat(Directory.GetFiles("PowerLinesWeb.Evaluation", "*.cs")).Order(StringComparer.Ordinal).ToList();
var numericalEnvironment = new { Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, Platform = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier, Architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString() };
var implementationHash = ForecastVersions.Hash(sourceFiles.Select(x => new { Path = x, Sha = Sha(x) }));
Tournament.WriteJson(Path.Combine(output, "run.json"), new { NumericalEnvironment = numericalEnvironment, Status = "Running", DatasetHash = manifest.SnapshotSha256, ManifestHash = manifestHash, Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, CodeSha = codeSha, ImplementationHash = implementationHash });
System.IO.File.WriteAllText(Path.Combine(output, "manifest.json"), manifestText);
var data = Replay.ReadSnapshot(snapshotPath);
var quotes = JsonSerializer.Deserialize<List<QualifiedQuote>>(ReadText(quotesPath), Replay.Json).ToDictionary(x => x.Id);
Tournament.WriteJson(Path.Combine(output, "cohort-ids.json"), new { Development = data.Where(x => x.Date >= manifest.DevelopmentFrom && x.Date < manifest.HoldoutFrom).Select(x => x.ResultId), Final = data.Where(x => x.Date >= manifest.HoldoutFrom && x.Date <= manifest.HoldoutThrough).Select(x => x.ResultId), Diagnostic = data.Where(x => x.Date > manifest.HoldoutThrough && x.Date <= manifest.DiagnosticThrough).Select(x => x.ResultId) });
var tournament = new Tournament(manifest, quotes, manifestHash);
List<RawPrediction> Raw(string stage, DateTime from, DateTime through, IReadOnlyList<Recipe> recipes)
{
    var path = Path.Combine(output, stage + "-raw.jsonl.gz");
    var fingerprint = ForecastVersions.Hash(new { numericalEnvironment, implementationHash, manifest.SnapshotSha256, from, through, recipes });
    var keyPath = path + ".sha256";
    if (System.IO.File.Exists(path) && System.IO.File.Exists(keyPath) && System.IO.File.ReadAllText(keyPath) == fingerprint) return Replay.ReadLines<RawPrediction>(path);
    var rows = Replay.Generate(data, manifest, from, through, recipes);
    Replay.WriteLines(path, rows); System.IO.File.WriteAllText(keyPath, fingerprint); return rows;
}
FrozenSelection frozen;
if (phase != "final")
{
    var dev = Raw("development", manifest.RawFrom, manifest.HoldoutFrom.AddDays(-1), manifest.Recipes);
    frozen = tournament.Freeze(dev, output) with { FinalIds = data.Where(x => x.Date >= manifest.HoldoutFrom && x.Date <= manifest.HoldoutThrough).Select(x => x.ResultId.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray() };
    Tournament.WriteJson(Path.Combine(output, "frozen-selection.json"), frozen);
    System.IO.File.WriteAllText(Path.Combine(output, "frozen-selection.sha256"), Sha(Path.Combine(output, "frozen-selection.json")));
    Console.WriteLine($"Development frozen: {frozen.Recipe}, T={frozen.Temperature}, alpha={frozen.BlendWeight}, threshold={frozen.ShortlistThreshold}");
    if (phase == "development") return;
}
else
{
    var path = Path.Combine(output, "frozen-selection.json");
    if (Sha(path) != System.IO.File.ReadAllText(Path.Combine(output, "frozen-selection.sha256"))) throw new InvalidOperationException("Frozen selection changed");
    frozen = JsonSerializer.Deserialize<FrozenSelection>(System.IO.File.ReadAllText(path), Replay.Json);
    if (frozen.ManifestHash != manifestHash) throw new InvalidOperationException("Manifest changed after selection");
}
var finalRecipes = manifest.Recipes.Where(x => x.Id == frozen.Recipe.Replace("-rho0", "") || x.Id is "goals365p5" or "goals365p20").ToList();
var final = Raw("final", manifest.HoldoutFrom, manifest.DiagnosticThrough, finalRecipes);
var report = tournament.Report(final, frozen, output, data);
var checksums = Directory.GetFiles(output).Where(x => Path.GetFileName(x) is not ("run.json" or "COMPLETE" or "checksums.json")).Order(StringComparer.Ordinal).ToDictionary(x => Path.GetFileName(x), Sha);
Tournament.WriteJson(Path.Combine(output, "checksums.json"), checksums);
Tournament.WriteJson(Path.Combine(output, "run.json"), new { ArtifactChecksumsHash = Sha(Path.Combine(output, "checksums.json")), Id = ForecastVersions.Hash(new { manifestHash, implementationHash, numericalEnvironment }), NumericalEnvironment = numericalEnvironment, Status = "Completed", DatasetHash = manifest.SnapshotSha256, ManifestHash = manifestHash, Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, CodeSha = codeSha, ImplementationHash = implementationHash, TestFrom = manifest.HoldoutFrom, TestThrough = manifest.HoldoutThrough, CompletedAt = DateTime.UtcNow });
System.IO.File.WriteAllText(Path.Combine(output, "COMPLETE"), Sha(Path.Combine(output, "report.json")) + "\n");
Console.WriteLine(System.IO.File.ReadAllText(Path.Combine(output, "report.md")));
