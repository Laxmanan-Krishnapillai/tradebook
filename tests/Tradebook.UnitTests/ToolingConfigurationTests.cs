using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Tradebook.UnitTests;

public sealed class ToolingConfigurationTests
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    [Fact]
    public void StrykerUsesTheRepositoryScopeReleaseBuildAndSingle80PercentGate()
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
            config
                .GetProperty("test-projects")
                .EnumerateArray()
                .Select(item => item.GetString()!)
                .ToArray()
        );
        Assert.Equal(
            ["Features/**/*.cs", "!Features/**/Models/*.cs", "!Features/**/Dto/*.cs"],
            config
                .GetProperty("mutate")
                .EnumerateArray()
                .Select(item => item.GetString()!)
                .ToArray()
        );
    }

    [Fact]
    public void AllProjectsTargetNet10AndPackageReferencesHaveNoVersionsOrOverrides()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectFiles = FindProjectFiles(repositoryRoot);

        Assert.Equal(6, projectFiles.Length);
        foreach (var projectFile in projectFiles)
        {
            var project = XDocument.Load(projectFile);
            Assert.Equal(
                ["net10.0"],
                project.Descendants("TargetFramework").Select(element => element.Value).ToArray()
            );
            Assert.Empty(project.Descendants("TargetFrameworks"));

            foreach (var reference in project.Descendants("PackageReference"))
            {
                Assert.Null(reference.Attribute("Version"));
                Assert.Null(reference.Attribute("VersionOverride"));
            }
        }
    }

    [Fact]
    public void AllOperationalDotnetEntryPointsUseNet10()
    {
        var repositoryRoot = FindRepositoryRoot();
        var operationalFiles = FindOperationalDotnetFiles(repositoryRoot);

        Assert.NotEmpty(operationalFiles);
        AssertOperationalFilesTargetNet10(repositoryRoot, operationalFiles);
        AssertWorkflowsUseGlobalJson(repositoryRoot);
        AssertTypegenTargetsNet10(repositoryRoot);
        AssertDockerRestoreStagesCopyRepositoryInputs(repositoryRoot, operationalFiles);
    }

    private static void AssertOperationalFilesTargetNet10(
        string repositoryRoot,
        string[] operationalFiles
    )
    {
        foreach (var path in operationalFiles)
        {
            var relativePath = Path.GetRelativePath(repositoryRoot, path);
            var contents = File.ReadAllText(path);
            Assert.False(
                contents.Contains("net9.0", StringComparison.OrdinalIgnoreCase),
                $"{relativePath} still references net9.0."
            );

            foreach (
                Match image in Regex.Matches(
                    contents,
                    @"mcr\.microsoft\.com/dotnet/(?:sdk|aspnet):(?<version>\d+\.\d+)",
                    RegexOptions.IgnoreCase,
                    RegexTimeout
                )
            )
            {
                Assert.True(
                    string.Equals(image.Groups["version"].Value, "10.0", StringComparison.Ordinal),
                    $"{relativePath} uses .NET container tag {image.Value} instead of 10.0."
                );
            }
        }
    }

    private static void AssertWorkflowsUseGlobalJson(string repositoryRoot)
    {
        var workflowDirectory = Path.Combine(repositoryRoot, ".github", "workflows");
        var workflows = Directory
            .GetFiles(workflowDirectory, "*.yml")
            .Concat(Directory.GetFiles(workflowDirectory, "*.yaml"));
        foreach (var workflowPath in workflows)
        {
            var workflowLines = File.ReadAllLines(workflowPath);
            const string setupActionPattern =
                @"(?m)^[ \t]*(?:-[ \t]+)?uses:[ \t]*actions/setup-dotnet@";
            var setupLineCount = workflowLines.Count(line =>
                Regex.IsMatch(line, setupActionPattern, RegexOptions.None, RegexTimeout)
            );
            var setupSteps = FindWorkflowSteps(workflowLines)
                .Where(step =>
                    Regex.IsMatch(step, setupActionPattern, RegexOptions.None, RegexTimeout)
                )
                .ToArray();

            Assert.Equal(setupLineCount, setupSteps.Length);
            foreach (var step in setupSteps)
            {
                Assert.True(
                    Regex.IsMatch(
                        step,
                        @"(?m)^[ \t]+global-json-file:[ \t]*global\.json(?:[ \t]+#.*)?[ \t]*\r?$",
                        RegexOptions.None,
                        RegexTimeout
                    ),
                    $"{Path.GetRelativePath(repositoryRoot, workflowPath)} has a setup-dotnet step without global.json."
                );
                Assert.DoesNotContain("dotnet-version:", step, StringComparison.Ordinal);
            }
        }
    }

    private static void AssertTypegenTargetsNet10(string repositoryRoot)
    {
        using var typegen = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(repositoryRoot, "tgconfig.json"))
        );
        var typegenAssembly = Assert.Single(
            typegen
                .RootElement.GetProperty("assemblies")
                .EnumerateArray()
                .Select(item => item.GetString()!)
        );
        Assert.True(
            typegenAssembly.Replace('\\', '/').Contains("/net10.0/", StringComparison.Ordinal),
            $"TypeGen assembly path is not net10.0: {typegenAssembly}"
        );
    }

    private static void AssertDockerRestoreStagesCopyRepositoryInputs(
        string repositoryRoot,
        string[] operationalFiles
    )
    {
        foreach (
            var dockerfilePath in operationalFiles.Where(path =>
                Path.GetFileName(path).StartsWith("Dockerfile", StringComparison.OrdinalIgnoreCase)
            )
        )
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
                StringComparison.OrdinalIgnoreCase
            );
            stageStart = stageStart < 0 ? 0 : stageStart + 1;
            var restoreStagePrefix = dockerfile[stageStart..restoreIndex];
            foreach (var restoreInput in new[] { "global.json", "Directory.Packages.props" })
            {
                var copy = Regex.Match(
                    restoreStagePrefix,
                    $@"(?m)^COPY[^\r\n]*\b{Regex.Escape(restoreInput)}\b",
                    RegexOptions.None,
                    RegexTimeout
                );
                Assert.True(
                    copy.Success,
                    $"{Path.GetRelativePath(repositoryRoot, dockerfilePath)} must copy {restoreInput} in the restore stage before dotnet restore."
                );
            }
        }
    }

    [Fact]
    public void CentralPackageManifestEnablesCpmAndPinsEveryReferenceOnce()
    {
        var repositoryRoot = FindRepositoryRoot();
        var expectedVersions = ReadTargetPackageVersions(repositoryRoot);
        var manifest = XDocument.Load(Path.Combine(repositoryRoot, "Directory.Packages.props"));

        Assert.Equal("true", manifest.Descendants("ManagePackageVersionsCentrally").Single().Value);
        Assert.Equal(
            "true",
            manifest.Descendants("CentralPackageTransitivePinningEnabled").Single().Value
        );

        var packageVersions = manifest.Descendants("PackageVersion").ToArray();
        AssertPackageVersions(expectedVersions, packageVersions);

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

        Assert.Empty(
            referencedPackages.Except(
                expectedVersions.Keys.Append("Vogen").Append("Microsoft.Identity.Web"),
                StringComparer.Ordinal
            )
        );
        Assert.Empty(
            expectedVersions
                .Keys.Except(referencedPackages, StringComparer.Ordinal)
                .Except(transitiveOnlyPins, StringComparer.Ordinal)
        );
    }

    private static void AssertPackageVersions(
        Dictionary<string, string> expectedVersions,
        XElement[] packageVersions
    )
    {
        Assert.Equal(expectedVersions.Count + 2, packageVersions.Length);
        var vogen = Assert.Single(
            packageVersions,
            element =>
                string.Equals(
                    element.Attribute("Include")?.Value,
                    "Vogen",
                    StringComparison.Ordinal
                )
        );
        Assert.StartsWith("8.", vogen.Attribute("Version")?.Value, StringComparison.Ordinal);
        Assert.Equal(
            packageVersions.Length,
            packageVersions
                .Select(element => element.Attribute("Include")!.Value)
                .Distinct(StringComparer.Ordinal)
                .Count()
        );
        foreach (var expected in expectedVersions)
        {
            var entry = Assert.Single(
                packageVersions,
                element =>
                    string.Equals(
                        element.Attribute("Include")?.Value,
                        expected.Key,
                        StringComparison.Ordinal
                    )
            );
            // TypeGen 5 cannot inspect net10 assemblies; Task 15 requires 7 for Vogen contract mappings.
            var expectedVersion = string.Equals(expected.Key, "TypeGen", StringComparison.Ordinal)
                ? "7.0.0"
                : expected.Value;
            Assert.Equal(expectedVersion, entry.Attribute("Version")?.Value);
        }
    }

    [Fact]
    public void CompilationDefaultsAreEnabledOnlyInDirectoryBuildProps()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sharedProperties = XDocument.Load(
            Path.Combine(repositoryRoot, "Directory.Build.props")
        );

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
    public void GlobalJsonPinsDotnet10WithLatestFeatureRollForward()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(FindRepositoryFile("global.json"))
        );
        var sdk = document.RootElement.GetProperty("sdk");

        Assert.Matches("^10\\.0\\.\\d+$", sdk.GetProperty("version").GetString()!);
        Assert.Equal("latestFeature", sdk.GetProperty("rollForward").GetString());
        Assert.False(sdk.GetProperty("allowPrerelease").GetBoolean());
    }

    [Fact]
    public void CiUsesGlobalJsonForBothDotnetSdkInstallations()
    {
        var workflow = File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), ".github", "workflows", "ci.yml")
        );

        Assert.Equal(
            2,
            Regex.Count(
                workflow,
                @"global-json-file:\s*global\.json",
                RegexOptions.None,
                RegexTimeout
            )
        );
        Assert.DoesNotContain("dotnet-version:", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void GlobalPackageReferenceIsReservedForTask14WithoutAddingAnalyzers()
    {
        var manifestPath = FindRepositoryFile("Directory.Packages.props");
        var manifestText = File.ReadAllText(manifestPath);
        var manifest = XDocument.Parse(manifestText);

        Assert.Contains(
            "Reserved for Task 14: GlobalPackageReference",
            manifestText,
            StringComparison.Ordinal
        );
        Assert.Empty(manifest.Descendants("GlobalPackageReference"));
        Assert.DoesNotContain(
            manifest.Descendants("PackageVersion"),
            element =>
                element
                    .Attribute("Include")!
                    .Value.Contains("Analyzer", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void DecisionLogRecordsDotnet10AndCentralPackageManagement()
    {
        var decisionLog = File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "docs", "architecture", "decision-log.md")
        );

        Assert.Contains(
            "D15 — Adopt .NET 10 LTS and NuGet Central Package Management",
            decisionLog,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "CentralPackageTransitivePinningEnabled",
            decisionLog,
            StringComparison.Ordinal
        );
        Assert.Contains("GlobalPackageReference", decisionLog, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> ReadTargetPackageVersions(string repositoryRoot)
    {
        var taskSpec = File.ReadAllLines(
            Path.Combine(
                repositoryRoot,
                "docs",
                "tasks",
                "task-13-platform-currency-and-central-package-management.md"
            )
        );
        var start = Array.IndexOf(taskSpec, "<!-- PLAT-05-PINS-START -->");
        var end = Array.IndexOf(taskSpec, "<!-- PLAT-05-PINS-END -->");

        Assert.True(
            start >= 0 && end > start,
            "The PLAT-05 package target table markers are missing or invalid."
        );

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
                $"PLAT-05 lists package '{package}' more than once."
            );
        }

        Assert.NotEmpty(versions);
        return versions;
    }

    private static List<string> FindWorkflowSteps(string[] lines)
    {
        var steps = new List<string>();
        var lineIndex = 0;
        while (lineIndex < lines.Length)
        {
            var header = MatchYamlPattern(
                lines[lineIndex],
                @"^(?<indent>[ \t]*)steps:[ \t]*(?:#.*)?$"
            );
            if (!header.Success)
            {
                lineIndex++;
                continue;
            }

            var headerIndent = header.Groups["indent"].Value.Length;
            var cursor = lineIndex + 1;
            while (cursor < lines.Length)
            {
                if (
                    IsYamlContentLine(lines[cursor])
                    && GetIndentLength(lines[cursor]) <= headerIndent
                )
                {
                    break;
                }

                var stepStart = MatchYamlPattern(lines[cursor], @"^(?<indent>[ \t]*)-[ \t]+");
                if (!stepStart.Success || stepStart.Groups["indent"].Value.Length <= headerIndent)
                {
                    cursor++;
                    continue;
                }

                var stepIndent = stepStart.Groups["indent"].Value.Length;
                var stepEnd = FindStepEnd(lines, cursor, headerIndent, stepIndent);

                steps.Add(string.Join(Environment.NewLine, lines[cursor..stepEnd]));
                cursor = stepEnd;
            }

            lineIndex = cursor;
        }

        return steps;
    }

    private static int FindStepEnd(string[] lines, int cursor, int headerIndent, int stepIndent)
    {
        var stepEnd = cursor + 1;
        while (stepEnd < lines.Length)
        {
            if (
                IsYamlContentLine(lines[stepEnd])
                && GetIndentLength(lines[stepEnd]) <= headerIndent
            )
            {
                break;
            }

            var nextStep = MatchYamlPattern(lines[stepEnd], @"^(?<indent>[ \t]*)-[ \t]+");
            if (nextStep.Success && nextStep.Groups["indent"].Value.Length == stepIndent)
            {
                break;
            }

            stepEnd++;
        }

        return stepEnd;
    }

    private static Match MatchYamlPattern(string input, string pattern) =>
        Regex.Match(input, pattern, RegexOptions.None, RegexTimeout);

    private static bool IsYamlContentLine(string line) =>
        !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith('#');

    private static int GetIndentLength(string line) =>
        line.TakeWhile(character => character is ' ' or '\t').Count();

    private static string[] FindOperationalDotnetFiles(string repositoryRoot)
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
            PushIncludedDirectories(repositoryRoot, directory, excludedDirectories, directories);
            AddOperationalFiles(repositoryRoot, directory, scriptExtensions, files);
        }

        return files.Order(StringComparer.Ordinal).ToArray();
    }

    private static void PushIncludedDirectories(
        string repositoryRoot,
        string directory,
        HashSet<string> excludedDirectories,
        Stack<string> directories
    )
    {
        foreach (var subdirectory in Directory.EnumerateDirectories(directory))
        {
            var name = Path.GetFileName(subdirectory);
            var isNestedBuildOutput =
                name.Equals("bin", StringComparison.OrdinalIgnoreCase)
                && !directory.Equals(repositoryRoot, StringComparison.OrdinalIgnoreCase);
            if (!excludedDirectories.Contains(name) && !isNestedBuildOutput)
            {
                directories.Push(subdirectory);
            }
        }
    }

    private static void AddOperationalFiles(
        string repositoryRoot,
        string directory,
        HashSet<string> scriptExtensions,
        HashSet<string> files
    )
    {
        foreach (var path in Directory.EnumerateFiles(directory))
        {
            var relativePath = Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');
            var fileName = Path.GetFileName(path);
            var extension = Path.GetExtension(path);
            var isRootConfiguration =
                !relativePath.Contains('/') && IsOperationalConfigurationExtension(extension);
            var isDotnetConfiguration =
                relativePath.StartsWith(".config/", StringComparison.OrdinalIgnoreCase)
                && extension.Equals(".json", StringComparison.OrdinalIgnoreCase);
            var isWorkflow =
                relativePath.StartsWith(".github/workflows/", StringComparison.OrdinalIgnoreCase)
                && (
                    extension.Equals(".yml", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase)
                );

            if (
                isRootConfiguration
                || isDotnetConfiguration
                || isWorkflow
                || scriptExtensions.Contains(extension)
                || fileName.StartsWith("Dockerfile", StringComparison.OrdinalIgnoreCase)
            )
            {
                files.Add(path);
            }
        }
    }

    private static bool IsOperationalConfigurationExtension(string extension) =>
        extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".props", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".targets", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase);

    private static string FindRepositoryRoot() =>
        Path.GetDirectoryName(FindRepositoryFile("Directory.Packages.props"))!;

    private static string[] FindProjectFiles(string repositoryRoot) =>
        Directory
            .GetFiles(
                Path.Combine(repositoryRoot, "src", "Backend", "src"),
                "*.csproj",
                SearchOption.AllDirectories
            )
            .Concat(
                Directory.GetFiles(
                    Path.Combine(repositoryRoot, "tests"),
                    "*.csproj",
                    SearchOption.AllDirectories
                )
            )
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string FindRepositoryFile(string fileName)
    {
        for (
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            directory is not null;
            directory = directory.Parent
        )
        {
            var candidate = Path.Combine(directory.FullName, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            $"Could not locate repository file '{fileName}'.",
            fileName
        );
    }
}
