using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Tradebook.UnitTests;

public sealed class ToolingConfigurationTests
{
    private static readonly IReadOnlyDictionary<string, string> Task17PackageVersions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["WolverineFx"] = "5.40.1",
            ["WolverineFx.Postgresql"] = "5.40.1",
        };

    [Fact]
    public void Stryker_uses_the_repository_scope_release_build_and_single_80_percent_gate()
    {
        var configPath = FindRepositoryFile("stryker-config.json");
        using var document = JsonDocument.Parse(File.ReadAllText(configPath));
        var config = document.RootElement.GetProperty("stryker-config");

        Assert.Equal("Release", config.GetProperty("configuration").GetString());
        Assert.Equal(80, config.GetProperty("thresholds").GetProperty("break").GetInt32());
        Assert.Equal("src/Backend/Tradebook.sln", config.GetProperty("solution").GetString());
        Assert.Equal("Tradebook.Api.csproj", config.GetProperty("project").GetString());
        Assert.Equal(
            ["tests/Tradebook.UnitTests/Tradebook.UnitTests.csproj"],
            config.GetProperty("test-projects").EnumerateArray().Select(item => item.GetString()!).ToArray());
        Assert.Equal(
            ["Features/**/*.cs", "!Features/**/Models/*.cs", "!Features/**/Dto/*.cs"],
            config.GetProperty("mutate").EnumerateArray().Select(item => item.GetString()!).ToArray());
    }

    [Fact]
    public void All_projects_target_net10_and_package_references_have_no_versions_or_overrides()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectFiles = FindProjectFiles(repositoryRoot);

        Assert.Equal(6, projectFiles.Count);
        foreach (var projectFile in projectFiles)
        {
            var project = XDocument.Load(projectFile);
            Assert.Equal(
                ["net10.0"],
                project.Descendants("TargetFramework").Select(element => element.Value).ToArray());
            Assert.Empty(project.Descendants("TargetFrameworks"));

            foreach (var reference in project.Descendants("PackageReference"))
            {
                Assert.Null(reference.Attribute("Version"));
                Assert.Null(reference.Attribute("VersionOverride"));
            }
        }
    }

    [Fact]
    public void All_operational_dotnet_entry_points_use_net10()
    {
        var repositoryRoot = FindRepositoryRoot();
        var operationalFiles = FindOperationalDotnetFiles(repositoryRoot);

        Assert.NotEmpty(operationalFiles);
        foreach (var path in operationalFiles)
        {
            var relativePath = Path.GetRelativePath(repositoryRoot, path);
            var contents = File.ReadAllText(path);
            Assert.False(
                contents.Contains("net9.0", StringComparison.OrdinalIgnoreCase),
                $"{relativePath} still references net9.0.");

            foreach (Match image in Regex.Matches(
                         contents,
                         @"mcr\.microsoft\.com/dotnet/(?:sdk|aspnet):(?<version>\d+\.\d+)",
                         RegexOptions.IgnoreCase))
            {
                Assert.True(
                    image.Groups["version"].Value == "10.0",
                    $"{relativePath} uses .NET container tag {image.Value} instead of 10.0.");
            }
        }

        var workflowDirectory = Path.Combine(repositoryRoot, ".github", "workflows");
        var workflows = Directory.GetFiles(workflowDirectory, "*.yml")
            .Concat(Directory.GetFiles(workflowDirectory, "*.yaml"));
        foreach (var workflowPath in workflows)
        {
            var workflowLines = File.ReadAllLines(workflowPath);
            const string setupActionPattern = @"(?m)^[ \t]*(?:-[ \t]+)?uses:[ \t]*actions/setup-dotnet@";
            var setupLineCount = workflowLines.Count(line => Regex.IsMatch(line, setupActionPattern));
            var setupSteps = FindWorkflowSteps(workflowLines)
                .Where(step => Regex.IsMatch(step, setupActionPattern))
                .ToArray();

            Assert.Equal(setupLineCount, setupSteps.Length);
            foreach (var step in setupSteps)
            {
                Assert.True(
                    Regex.IsMatch(
                        step,
                        @"(?m)^[ \t]+global-json-file:[ \t]*global\.json(?:[ \t]+#.*)?[ \t]*\r?$"),
                    $"{Path.GetRelativePath(repositoryRoot, workflowPath)} has a setup-dotnet step without global.json.");
                Assert.DoesNotContain("dotnet-version:", step);
            }
        }

        using var typegen = JsonDocument.Parse(File.ReadAllText(Path.Combine(repositoryRoot, "tgconfig.json")));
        var typegenAssembly = Assert.Single(
            typegen.RootElement.GetProperty("assemblies").EnumerateArray().Select(item => item.GetString()!));
        Assert.True(
            typegenAssembly.Replace('\\', '/').Contains("/net10.0/", StringComparison.Ordinal),
            $"TypeGen assembly path is not net10.0: {typegenAssembly}");

        foreach (var dockerfilePath in operationalFiles.Where(
                     path => Path.GetFileName(path).StartsWith("Dockerfile", StringComparison.OrdinalIgnoreCase)))
        {
            var dockerfile = File.ReadAllText(dockerfilePath);
            var restoreIndex = dockerfile.IndexOf("RUN dotnet restore", StringComparison.Ordinal);
            if (restoreIndex < 0)
            {
                continue;
            }

            var stageStart = dockerfile.LastIndexOf(
                "\nFROM ",
                restoreIndex,
                StringComparison.OrdinalIgnoreCase);
            stageStart = stageStart < 0 ? 0 : stageStart + 1;
            var restoreStagePrefix = dockerfile[stageStart..restoreIndex];
            foreach (var restoreInput in new[] { "global.json", "Directory.Packages.props" })
            {
                var copy = Regex.Match(
                    restoreStagePrefix,
                    $@"(?m)^COPY[^\r\n]*\b{Regex.Escape(restoreInput)}\b");
                Assert.True(
                    copy.Success,
                    $"{Path.GetRelativePath(repositoryRoot, dockerfilePath)} must copy {restoreInput} in the restore stage before dotnet restore.");
            }
        }
    }

    [Fact]
    public void Central_package_manifest_enables_cpm_and_pins_every_reference_once()
    {
        var repositoryRoot = FindRepositoryRoot();
        var expectedVersions = ReadTargetPackageVersions(repositoryRoot);
        var manifest = XDocument.Load(Path.Combine(repositoryRoot, "Directory.Packages.props"));

        Assert.Equal("true", manifest.Descendants("ManagePackageVersionsCentrally").Single().Value);
        Assert.Equal("true", manifest.Descendants("CentralPackageTransitivePinningEnabled").Single().Value);

        var packageVersions = manifest.Descendants("PackageVersion").ToArray();
        Assert.Equal(expectedVersions.Count, packageVersions.Length);
        Assert.Equal(
            packageVersions.Length,
            packageVersions.Select(element => element.Attribute("Include")!.Value).Distinct(StringComparer.Ordinal).Count());
        foreach (var expected in expectedVersions)
        {
            var entry = Assert.Single(
                packageVersions,
                element => element.Attribute("Include")?.Value == expected.Key);
            Assert.Equal(expected.Value, entry.Attribute("Version")?.Value);
        }

        var referenceFiles = FindProjectFiles(repositoryRoot)
            .Append(Path.Combine(repositoryRoot, "Directory.Build.props"));
        var referencedPackages = referenceFiles
            .SelectMany(path => XDocument.Load(path).Descendants("PackageReference"))
            .Select(element => element.Attribute("Include")!.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        // Pinned only to override a vulnerable transitive resolution (NuGetAudit NU1903:
        // https://github.com/advisories/GHSA-v5pm-xwqc-g5wc via Microsoft.AspNetCore.OpenApi).
        // No project references it directly, so it is exempt from the reference check below.
        var transitiveOnlyPins = new[] { "Microsoft.OpenApi" };

        Assert.Empty(referencedPackages.Except(expectedVersions.Keys, StringComparer.Ordinal));
        Assert.Empty(
            expectedVersions.Keys
                .Except(referencedPackages, StringComparer.Ordinal)
                .Except(transitiveOnlyPins, StringComparer.Ordinal));
    }

    [Fact]
    public void Msg04_hand_rolled_outbox_dispatcher_source_is_absent()
    {
        var repositoryRoot = FindRepositoryRoot();
        var infrastructureRoot = Path.Combine(
            repositoryRoot, "src", "Backend", "src", "Tradebook.Infrastructure");
        var outboxDirectory = Path.Combine(infrastructureRoot, "Outbox");

        Assert.False(
            Directory.Exists(outboxDirectory),
            $"The hand-rolled outbox directory still exists: {outboxDirectory}");

        var forbiddenMarkers = new[]
        {
            "BackgroundService",
            "LISTEN",
            "NOTIFY",
            "SKIP LOCKED",
            "FOR UPDATE",
            "OutboxDispatcher",
            "PostgresOutboxEventReader",
            "IOutboxEventReader",
            "WriteOutboxAsync",
            "outbox_events",
        };
        var matches = FindSourceMarkerMatches(
            repositoryRoot,
            FindProductionFiles(infrastructureRoot),
            forbiddenMarkers);

        Assert.Empty(matches);
    }

    [Fact]
    public void Msg05_wolverine_packages_are_the_only_task17_dependency_additions()
    {
        var repositoryRoot = FindRepositoryRoot();
        var platformPackages = ReadPlatformPackageVersions(repositoryRoot);
        var manifest = XDocument.Load(Path.Combine(repositoryRoot, "Directory.Packages.props"));
        var task17Additions = manifest.Descendants("PackageVersion")
            .Select(element => new
            {
                Package = element.Attribute("Include")!.Value,
                Version = element.Attribute("Version")!.Value,
            })
            .Where(entry => !platformPackages.ContainsKey(entry.Package))
            .ToArray();

        Assert.Equal(Task17PackageVersions.Count, task17Additions.Length);
        foreach (var expected in Task17PackageVersions)
        {
            var addition = Assert.Single(
                task17Additions,
                entry => entry.Package == expected.Key);
            Assert.Equal(expected.Value, addition.Version);
        }
    }

    [Fact]
    public void Msg05_backend_source_and_dependency_graph_contain_no_orm()
    {
        var repositoryRoot = FindRepositoryRoot();
        var backendRoot = Path.Combine(repositoryRoot, "src", "Backend", "src");
        var forbiddenMarkers = new[] { "EntityFrameworkCore", "Marten" };
        var sourceMatches = FindSourceMarkerMatches(
            repositoryRoot,
            FindProductionFiles(backendRoot),
            forbiddenMarkers);

        Assert.Empty(sourceMatches);

        var assetsPath = Path.Combine(
            backendRoot, "Tradebook.Api", "obj", "project.assets.json");
        Assert.True(File.Exists(assetsPath), $"Restored dependency graph is missing: {assetsPath}");
        using var assets = JsonDocument.Parse(File.ReadAllText(assetsPath));
        var forbiddenPackages = assets.RootElement.GetProperty("libraries")
            .EnumerateObject()
            .Select(library => library.Name[..library.Name.LastIndexOf('/')])
            .Where(package => forbiddenMarkers.Any(marker =>
                package.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(forbiddenPackages);
    }

    [Fact]
    public void Compilation_defaults_are_enabled_only_in_directory_build_props()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sharedProperties = XDocument.Load(Path.Combine(repositoryRoot, "Directory.Build.props"));

        Assert.Equal("enable", sharedProperties.Descendants("Nullable").Single().Value);
        Assert.Equal("enable", sharedProperties.Descendants("ImplicitUsings").Single().Value);
        foreach (var projectFile in FindProjectFiles(repositoryRoot))
        {
            var project = XDocument.Load(projectFile);
            Assert.Empty(project.Descendants("Nullable"));
            Assert.Empty(project.Descendants("ImplicitUsings"));
        }
    }

    [Fact]
    public void Global_json_pins_dotnet10_with_latest_feature_roll_forward()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FindRepositoryFile("global.json")));
        var sdk = document.RootElement.GetProperty("sdk");

        Assert.Matches("^10\\.0\\.\\d+$", sdk.GetProperty("version").GetString()!);
        Assert.Equal("latestFeature", sdk.GetProperty("rollForward").GetString());
        Assert.False(sdk.GetProperty("allowPrerelease").GetBoolean());
    }

    [Fact]
    public void Ci_uses_global_json_for_both_dotnet_sdk_installations()
    {
        var workflow = File.ReadAllText(Path.Combine(FindRepositoryRoot(), ".github", "workflows", "ci.yml"));

        Assert.Equal(2, Regex.Matches(workflow, @"global-json-file:\s*global\.json").Count);
        Assert.DoesNotContain("dotnet-version:", workflow);
    }

    [Fact]
    public void Global_package_reference_is_reserved_for_task14_without_adding_analyzers()
    {
        var manifestPath = FindRepositoryFile("Directory.Packages.props");
        var manifestText = File.ReadAllText(manifestPath);
        var manifest = XDocument.Parse(manifestText);

        Assert.Contains("Reserved for Task 14: GlobalPackageReference", manifestText);
        Assert.Empty(manifest.Descendants("GlobalPackageReference"));
        Assert.DoesNotContain(
            manifest.Descendants("PackageVersion"),
            element => element.Attribute("Include")!.Value.Contains("Analyzer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Decision_log_records_dotnet10_and_central_package_management()
    {
        var decisionLog = File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "docs", "architecture", "decision-log.md"));

        Assert.Contains("D15 — Adopt .NET 10 LTS and NuGet Central Package Management", decisionLog);
        Assert.Contains("CentralPackageTransitivePinningEnabled", decisionLog);
        Assert.Contains("GlobalPackageReference", decisionLog);
    }

    private static IReadOnlyDictionary<string, string> ReadTargetPackageVersions(string repositoryRoot)
    {
        var versions = new Dictionary<string, string>(
            ReadPlatformPackageVersions(repositoryRoot),
            StringComparer.Ordinal);
        foreach (var task17Package in Task17PackageVersions)
        {
            Assert.True(
                versions.TryAdd(task17Package.Key, task17Package.Value),
                $"Task 17 package '{task17Package.Key}' already exists in the platform baseline.");
        }

        return versions;
    }

    private static IReadOnlyDictionary<string, string> ReadPlatformPackageVersions(string repositoryRoot)
    {
        var taskSpec = File.ReadAllLines(Path.Combine(
            repositoryRoot,
            "docs",
            "tasks",
            "task-13-platform-currency-and-central-package-management.md"));
        var start = Array.IndexOf(taskSpec, "<!-- PLAT-05-PINS-START -->");
        var end = Array.IndexOf(taskSpec, "<!-- PLAT-05-PINS-END -->");

        Assert.True(start >= 0 && end > start, "The PLAT-05 package target table markers are missing or invalid.");

        var versions = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in taskSpec[(start + 1)..end])
        {
            if (!line.StartsWith("| `", StringComparison.Ordinal))
            {
                continue;
            }

            var cells = line.Split('|', StringSplitOptions.None);
            Assert.True(cells.Length >= 4, $"Invalid PLAT-05 package target row: {line}");

            var package = cells[1].Trim().Trim('`');
            var version = cells[2].Trim().Trim('`');
            Assert.True(
                versions.TryAdd(package, version),
                $"PLAT-05 lists package '{package}' more than once.");
        }

        Assert.NotEmpty(versions);
        return versions;
    }

    private static IReadOnlyList<string> FindProductionFiles(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path) is ".cs" or ".csproj" or ".props")
            .Where(path => !Path.GetRelativePath(root, path)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment is "bin" or "obj"))
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<string> FindSourceMarkerMatches(
        string repositoryRoot,
        IEnumerable<string> sourceFiles,
        IReadOnlyList<string> forbiddenMarkers) =>
        sourceFiles
            .Select(path => new
            {
                Path = Path.GetRelativePath(repositoryRoot, path),
                Contents = File.ReadAllText(path),
            })
            .SelectMany(source => forbiddenMarkers
                .Where(marker => source.Contents.Contains(marker, StringComparison.OrdinalIgnoreCase))
                .Select(marker => $"{source.Path}: {marker}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<string> FindWorkflowSteps(string[] lines)
    {
        var steps = new List<string>();
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var header = Regex.Match(
                lines[lineIndex],
                @"^(?<indent>[ \t]*)steps:[ \t]*(?:#.*)?$");
            if (!header.Success)
            {
                continue;
            }

            var headerIndent = header.Groups["indent"].Value.Length;
            var cursor = lineIndex + 1;
            while (cursor < lines.Length)
            {
                if (IsYamlContentLine(lines[cursor]) && GetIndentLength(lines[cursor]) <= headerIndent)
                {
                    break;
                }

                var stepStart = Regex.Match(lines[cursor], @"^(?<indent>[ \t]*)-[ \t]+");
                if (!stepStart.Success || stepStart.Groups["indent"].Value.Length <= headerIndent)
                {
                    cursor++;
                    continue;
                }

                var stepIndent = stepStart.Groups["indent"].Value.Length;
                var stepEnd = cursor + 1;
                while (stepEnd < lines.Length)
                {
                    if (IsYamlContentLine(lines[stepEnd]) && GetIndentLength(lines[stepEnd]) <= headerIndent)
                    {
                        break;
                    }

                    var nextStep = Regex.Match(lines[stepEnd], @"^(?<indent>[ \t]*)-[ \t]+");
                    if (nextStep.Success && nextStep.Groups["indent"].Value.Length == stepIndent)
                    {
                        break;
                    }

                    stepEnd++;
                }

                steps.Add(string.Join(Environment.NewLine, lines[cursor..stepEnd]));
                cursor = stepEnd;
            }

            lineIndex = cursor - 1;
        }

        return steps;
    }

    private static bool IsYamlContentLine(string line) =>
        !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith('#');

    private static int GetIndentLength(string line) =>
        line.TakeWhile(character => character is ' ' or '\t').Count();

    private static IReadOnlyList<string> FindOperationalDotnetFiles(string repositoryRoot)
    {
        var excludedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".codegraph",
            ".git",
            ".next",
            ".stryker-output",
            ".terraform",
            "coverage",
            "dist",
            "generated",
            "node_modules",
            "obj",
            "TestResults",
        };
        var scriptExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".bat",
            ".cmd",
            ".ps1",
            ".sh",
        };
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var directories = new Stack<string>();
        directories.Push(repositoryRoot);

        while (directories.TryPop(out var directory))
        {
            foreach (var subdirectory in Directory.EnumerateDirectories(directory))
            {
                var name = Path.GetFileName(subdirectory);
                var isNestedBuildOutput =
                    name.Equals("bin", StringComparison.OrdinalIgnoreCase) &&
                    !directory.Equals(repositoryRoot, StringComparison.OrdinalIgnoreCase);
                if (!excludedDirectories.Contains(name) && !isNestedBuildOutput)
                {
                    directories.Push(subdirectory);
                }
            }

            foreach (var path in Directory.EnumerateFiles(directory))
            {
                var relativePath = Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');
                var fileName = Path.GetFileName(path);
                var extension = Path.GetExtension(path);
                var isRootConfiguration =
                    !relativePath.Contains('/') &&
                    (extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
                     extension.Equals(".props", StringComparison.OrdinalIgnoreCase) ||
                     extension.Equals(".targets", StringComparison.OrdinalIgnoreCase) ||
                     extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
                     extension.Equals(".yml", StringComparison.OrdinalIgnoreCase));
                var isDotnetConfiguration =
                    relativePath.StartsWith(".config/", StringComparison.OrdinalIgnoreCase) &&
                    extension.Equals(".json", StringComparison.OrdinalIgnoreCase);
                var isWorkflow =
                    relativePath.StartsWith(".github/workflows/", StringComparison.OrdinalIgnoreCase) &&
                    (extension.Equals(".yml", StringComparison.OrdinalIgnoreCase) ||
                     extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase));

                if (isRootConfiguration ||
                    isDotnetConfiguration ||
                    isWorkflow ||
                    scriptExtensions.Contains(extension) ||
                    fileName.StartsWith("Dockerfile", StringComparison.OrdinalIgnoreCase))
                {
                    files.Add(path);
                }
            }
        }

        return files.Order(StringComparer.Ordinal).ToArray();
    }

    private static string FindRepositoryRoot() =>
        Path.GetDirectoryName(FindRepositoryFile("Directory.Packages.props"))!;

    private static IReadOnlyList<string> FindProjectFiles(string repositoryRoot) =>
        Directory.GetFiles(Path.Combine(repositoryRoot, "src", "Backend", "src"), "*.csproj", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.Combine(repositoryRoot, "tests"), "*.csproj", SearchOption.AllDirectories))
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string FindRepositoryFile(string fileName)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Could not locate repository file '{fileName}'.", fileName);
    }
}
