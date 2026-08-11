using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Tradebook.UnitTests;

public sealed class ToolingConfigurationTests
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private static readonly HashSet<string> ExcludedOperationalDirectories = new(
        StringComparer.OrdinalIgnoreCase
    )
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
    private static readonly HashSet<string> OperationalScriptExtensions = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ".bat",
        ".cmd",
        ".ps1",
        ".sh",
    };
    private static readonly string[] ExpectedBannedSymbols =
    [
        "P:System.DateTime.Now",
        "P:System.DateTime.UtcNow",
        "P:System.DateTimeOffset.Now",
        "P:System.DateTimeOffset.UtcNow",
        "M:System.Decimal.op_Explicit(System.Double)~System.Decimal",
        "M:System.Decimal.op_Explicit(System.Decimal)~System.Double",
        "M:System.Decimal.Parse(System.String)",
        "M:System.Decimal.Parse(System.String,System.Globalization.NumberStyles)",
        "M:System.Decimal.TryParse(System.String,System.Decimal@)",
        "M:System.Decimal.TryParse(System.ReadOnlySpan{System.Char},System.Decimal@)",
        "M:System.Decimal.TryParse(System.ReadOnlySpan{System.Byte},System.Decimal@)",
        "M:System.Double.Parse(System.String)",
        "M:System.Double.Parse(System.String,System.Globalization.NumberStyles)",
        "M:System.Double.TryParse(System.String,System.Double@)",
        "M:System.Double.TryParse(System.ReadOnlySpan{System.Char},System.Double@)",
        "M:System.Double.TryParse(System.ReadOnlySpan{System.Byte},System.Double@)",
    ];

    [Fact]
    public void StrykerUsesMtpRepositoryScopeReleaseBuildAndRequiredThresholds()
    {
        var configPath = FindRepositoryFile("stryker-config.json");
        using var document = JsonDocument.Parse(File.ReadAllText(configPath));
        var config = document.RootElement.GetProperty("stryker-config");

        Assert.Equal("Release", config.GetProperty("configuration").GetString());
        Assert.Equal("mtp", config.GetProperty("test-runner").GetString());
        Assert.Equal(85, config.GetProperty("thresholds").GetProperty("high").GetInt32());
        Assert.Equal(80, config.GetProperty("thresholds").GetProperty("low").GetInt32());
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

        Assert.Equal(10, projectFiles.Length);
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
        AssertOperationalFilesUseNet10(repositoryRoot, operationalFiles);
        AssertWorkflowsUseGlobalJson(repositoryRoot);
        AssertDockerRestoreInputsAreCopied(repositoryRoot, operationalFiles);
    }

    private static void AssertOperationalFilesUseNet10(
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

    private static void AssertDockerRestoreInputsAreCopied(
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

    private static void AssertPinnedVersionsMatch(
        IReadOnlyDictionary<string, string> expectedVersions,
        XElement[] packageVersions
    )
    {
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
            Assert.Equal(expected.Value, entry.Attribute("Version")?.Value);
        }
    }

    [Fact]
    public void CentralPackageManifestEnablesCpmAndPinsEveryReferenceOnce()
    {
        var repositoryRoot = FindRepositoryRoot();
        var expectedVersions = ReadExpectedPackageVersions(repositoryRoot);
        var manifest = XDocument.Load(Path.Combine(repositoryRoot, "Directory.Packages.props"));

        Assert.Equal("true", manifest.Descendants("ManagePackageVersionsCentrally").Single().Value);
        Assert.Equal(
            "true",
            manifest.Descendants("CentralPackageTransitivePinningEnabled").Single().Value
        );

        var packageVersions = manifest.Descendants("PackageVersion").ToArray();
        Assert.Equal(expectedVersions.Count + 1, packageVersions.Length);
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
        AssertPinnedVersionsMatch(expectedVersions, packageVersions);

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
            referencedPackages.Except(expectedVersions.Keys.Append("Vogen"), StringComparer.Ordinal)
        );
        Assert.Empty(
            expectedVersions
                .Keys.Except(referencedPackages, StringComparer.Ordinal)
                .Except(transitiveOnlyPins, StringComparer.Ordinal)
        );
    }

    [Fact]
    public void SAFE01CompilationAndAnalyzerBuildPolicyIsRepoWide()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sharedProperties = XDocument.Load(
            Path.Combine(repositoryRoot, "Directory.Build.props")
        );

        Assert.Equal("net10.0", sharedProperties.Descendants("TargetFramework").Single().Value);
        Assert.Equal("enable", sharedProperties.Descendants("Nullable").Single().Value);
        Assert.Equal("enable", sharedProperties.Descendants("ImplicitUsings").Single().Value);
        Assert.Equal("latest", sharedProperties.Descendants("LangVersion").Single().Value);
        Assert.Equal("true", sharedProperties.Descendants("TreatWarningsAsErrors").Single().Value);
        Assert.Equal(
            "true",
            sharedProperties.Descendants("CodeAnalysisTreatWarningsAsErrors").Single().Value
        );
        Assert.Equal("true", sharedProperties.Descendants("EnableNETAnalyzers").Single().Value);
        Assert.Equal(
            "true",
            sharedProperties.Descendants("RunAnalyzersDuringBuild").Single().Value
        );
        Assert.Equal(
            "true",
            sharedProperties.Descendants("RunAnalyzersDuringLiveAnalysis").Single().Value
        );
        Assert.Equal("latest", sharedProperties.Descendants("AnalysisLevel").Single().Value);
        Assert.Equal("Recommended", sharedProperties.Descendants("AnalysisMode").Single().Value);
        Assert.Equal(
            "true",
            sharedProperties.Descendants("EnforceCodeStyleInBuild").Single().Value
        );

        var bannedSymbols = Assert.Single(sharedProperties.Descendants("AdditionalFiles"));
        Assert.Equal(
            "$(MSBuildThisFileDirectory)BannedSymbols.txt",
            bannedSymbols.Attribute("Include")?.Value
        );

        foreach (var projectFile in FindProjectFiles(repositoryRoot))
        {
            var project = XDocument.Load(projectFile);
            Assert.Empty(project.Descendants("Nullable"));
            Assert.Empty(project.Descendants("ImplicitUsings"));
            Assert.Empty(project.Descendants("TreatWarningsAsErrors"));
            Assert.Empty(project.Descendants("CodeAnalysisTreatWarningsAsErrors"));
            Assert.Empty(project.Descendants("EnableNETAnalyzers"));
            Assert.Empty(project.Descendants("RunAnalyzersDuringBuild"));
            Assert.Empty(project.Descendants("RunAnalyzersDuringLiveAnalysis"));
            Assert.Empty(project.Descendants("AnalysisLevel"));
            Assert.Empty(project.Descendants("AnalysisMode"));
            Assert.Empty(project.Descendants("EnforceCodeStyleInBuild"));
            Assert.Empty(project.Descendants("CSharpier_Check"));
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
    public void CiUsesGlobalJsonForDotnetSdkInstallation()
    {
        var workflow = File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), ".github", "workflows", "ci.yml")
        );

        Assert.Equal(
            1,
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
    public void ProductionDeploymentRunsOnlyFromTheOrganizationFork()
    {
        var workflow = File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), ".github", "workflows", "deploy.yml")
        );

        Assert.Equal(
            2,
            Regex.Count(
                workflow,
                @"(?m)^\s+github\.repository == 'Fremsyn/tradebook' &&\s*$",
                RegexOptions.None,
                RegexTimeout
            )
        );
        foreach (
            var argument in new[]
            {
                "VITE_ENTRA_TENANT_ID=$ENTRA_TENANT_ID",
                "VITE_ENTRA_SPA_CLIENT_ID=$ENTRA_SPA_CLIENT_ID",
                "VITE_ENTRA_API_CLIENT_ID=$ENTRA_API_CLIENT_ID",
                "VITE_ENTRA_REDIRECT_ORIGIN=$ENTRA_REDIRECT_ORIGIN",
            }
        )
        {
            Assert.Contains($"--build-arg \"{argument}\"", workflow, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RuntimeImagePublishesViteAssetsWithTheApiStaticAssetManifest()
    {
        var dockerfile = File.ReadAllText(FindRepositoryFile("Dockerfile"));
        var frontendCopy = dockerfile.IndexOf(
            "COPY --from=frontend /src/Frontend/dist ./src/Backend/src/Tradebook.Api/wwwroot",
            StringComparison.Ordinal
        );
        var apiPublishStage = dockerfile.IndexOf(
            "FROM backend AS api-publish",
            StringComparison.Ordinal
        );
        var apiPublish = dockerfile.IndexOf(
            "RUN dotnet publish src/Backend/src/Tradebook.Api/Tradebook.Api.csproj",
            StringComparison.Ordinal
        );

        Assert.InRange(frontendCopy, apiPublishStage + 1, apiPublish - 1);
        Assert.DoesNotContain(
            "COPY --from=frontend /src/Frontend/dist ./wwwroot",
            dockerfile,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "COPY --from=api-publish /app/publish ./",
            dockerfile,
            StringComparison.Ordinal
        );

        var databaseOperationsStage = dockerfile[
            dockerfile.IndexOf(
                "FROM postgres:17-bookworm AS database-ops",
                StringComparison.Ordinal
            )..dockerfile.IndexOf(
                "FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime",
                StringComparison.Ordinal
            )
        ];
        Assert.Contains(
            "COPY --from=backend /app/migrator/",
            databaseOperationsStage,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("--from=frontend", databaseOperationsStage, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "--from=api-publish",
            databaseOperationsStage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void ApiMapsStaticAssetsBeforeTheSpaFallback()
    {
        var program = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "Backend",
                "src",
                "Tradebook.Api",
                "Program.cs"
            )
        );
        var staticAssets = program.IndexOf(
            "app.MapStaticAssets().AllowAnonymous();",
            StringComparison.Ordinal
        );
        var spaFallback = program.IndexOf("app.MapFallbackToFile", StringComparison.Ordinal);
        Assert.InRange(staticAssets, 0, spaFallback - 1);
    }

    [Fact]
    public void SAFE09GlobalAnalyzersAreExactPrivateAndRepoWide()
    {
        var repositoryRoot = FindRepositoryRoot();
        var manifest = XDocument.Load(Path.Combine(repositoryRoot, "Directory.Packages.props"));
        var expected = ExpectedGlobalAnalyzerVersions();
        var globalReferences = manifest.Descendants("GlobalPackageReference").ToArray();

        AssertGlobalAnalyzerReferences(expected, globalReferences);
        AssertNoProjectAnalyzerOverrides(repositoryRoot, manifest, expected);
        AssertTestProjectsUseCentralXunitAnalyzer(repositoryRoot);

        var editorConfig = File.ReadAllText(Path.Combine(repositoryRoot, ".editorconfig"));
        Assert.Matches(@"(?m)^dotnet_diagnostic\.RS0030\.severity\s*=\s*error\s*$", editorConfig);
        Assert.DoesNotMatch(@"(?im)^\s*[^#\r\n]*severity\s*=\s*none\s*$", editorConfig);

        var buildFiles = FindProjectFiles(repositoryRoot)
            .Append(Path.Combine(repositoryRoot, "Directory.Build.props"));
        Assert.Empty(buildFiles.SelectMany(path => XDocument.Load(path).Descendants("NoWarn")));
        Assert.Empty(
            buildFiles.SelectMany(path => XDocument.Load(path).Descendants("WarningsNotAsErrors"))
        );
    }

    private static void AssertGlobalAnalyzerReferences(
        Dictionary<string, string> expected,
        XElement[] globalReferences
    )
    {
        Assert.Equal(expected.Count, globalReferences.Length);
        Assert.Equal(
            globalReferences.Length,
            globalReferences
                .Select(element => element.Attribute("Include")!.Value)
                .Distinct(StringComparer.Ordinal)
                .Count()
        );
        foreach (var package in expected)
        {
            var reference = Assert.Single(
                globalReferences,
                element =>
                    string.Equals(
                        element.Attribute("Include")?.Value,
                        package.Key,
                        StringComparison.Ordinal
                    )
            );
            Assert.Equal(package.Value, reference.Attribute("Version")?.Value);
            Assert.Equal("all", reference.Attribute("PrivateAssets")?.Value);
        }
    }

    private static void AssertNoProjectAnalyzerOverrides(
        string repositoryRoot,
        XDocument manifest,
        Dictionary<string, string> expected
    )
    {
        Assert.DoesNotContain(
            manifest.Descendants("PackageVersion"),
            element => expected.ContainsKey(element.Attribute("Include")!.Value)
        );
        Assert.DoesNotContain(
            FindProjectFiles(repositoryRoot)
                .SelectMany(path => XDocument.Load(path).Descendants("PackageReference")),
            element => expected.ContainsKey(element.Attribute("Include")!.Value)
        );
    }

    private static void AssertTestProjectsUseCentralXunitAnalyzer(string repositoryRoot)
    {
        var testProjects = Directory.GetFiles(
            Path.Combine(repositoryRoot, "tests"),
            "*.csproj",
            SearchOption.AllDirectories
        );
        Assert.Equal(3, testProjects.Length);
        foreach (var testProject in testProjects)
        {
            var xunitAnalyzer = Assert.Single(
                XDocument.Load(testProject).Descendants("PackageReference"),
                element =>
                    string.Equals(
                        element.Attribute("Include")?.Value,
                        "xunit.analyzers",
                        StringComparison.Ordinal
                    )
            );
            Assert.Equal("all", xunitAnalyzer.Attribute("PrivateAssets")?.Value);
            Assert.Null(xunitAnalyzer.Attribute("Version"));
            Assert.Null(xunitAnalyzer.Attribute("VersionOverride"));
        }
    }

    private static Dictionary<string, string> ExpectedGlobalAnalyzerVersions() =>
        new(StringComparer.Ordinal)
        {
            ["Meziantou.Analyzer"] = "3.0.139",
            ["SonarAnalyzer.CSharp"] = "10.30.0.144632",
            ["Microsoft.CodeAnalysis.BannedApiAnalyzers"] = "5.6.0",
            ["Microsoft.VisualStudio.Threading.Analyzers"] = "18.7.23",
            ["CSharpier.MsBuild"] = "1.3.0",
        };

    [Fact]
    public void SAFE02CsharpierIsPinnedAndCheckedByLocalAndCiGates()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var manifest = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(repositoryRoot, ".config", "dotnet-tools.json"))
        );
        var csharpier = manifest.RootElement.GetProperty("tools").GetProperty("csharpier");

        Assert.Equal("1.3.0", csharpier.GetProperty("version").GetString());
        Assert.Equal(
            ["csharpier"],
            csharpier
                .GetProperty("commands")
                .EnumerateArray()
                .Select(value => value.GetString()!)
                .ToArray()
        );

        var sharedProperties = XDocument.Load(
            Path.Combine(repositoryRoot, "Directory.Build.props")
        );
        Assert.Equal("true", sharedProperties.Descendants("CSharpier_Check").Single().Value);

        var verify = File.ReadAllText(Path.Combine(repositoryRoot, "bin", "verify.sh"));
        var workflow = File.ReadAllText(
            Path.Combine(repositoryRoot, ".github", "workflows", "ci.yml")
        );
        Assert.Equal(
            1,
            Regex.Count(
                verify,
                @"(?m)^\s*dotnet tool run csharpier check \.\s*\|\|",
                RegexOptions.None,
                RegexTimeout
            )
        );
        Assert.Equal(
            1,
            Regex.Count(
                workflow,
                @"(?m)^\s*- run: dotnet tool run csharpier check \.\s*$",
                RegexOptions.None,
                RegexTimeout
            )
        );
        Assert.Contains("- '.editorconfig'", workflow, StringComparison.Ordinal);
        Assert.Contains("- 'BannedSymbols.txt'", workflow, StringComparison.Ordinal);
        Assert.Contains("- 'bin/check-banned-api.sh'", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void SAFE07DateTimeNowHasAnExecutableRS0030NegativeProbe()
    {
        var repositoryRoot = FindRepositoryRoot();
        var bannedSymbols = File.ReadAllLines(Path.Combine(repositoryRoot, "BannedSymbols.txt"))
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'))
            .ToArray();
        Assert.Equal(ExpectedBannedSymbols.Length, bannedSymbols.Length);
        foreach (var symbol in ExpectedBannedSymbols)
        {
            Assert.Single(
                bannedSymbols,
                line => line.StartsWith(symbol + ';', StringComparison.Ordinal)
            );
        }

        var probe = File.ReadAllText(Path.Combine(repositoryRoot, "bin", "check-banned-api.sh"));
        Assert.Contains("mktemp -d", probe, StringComparison.Ordinal);
        Assert.Contains("_ = DateTime.Now;", probe, StringComparison.Ordinal);
        Assert.Contains("-ne 11", probe, StringComparison.Ordinal);
        Assert.Contains("dotnet build", probe, StringComparison.Ordinal);
        Assert.Contains("error[[:space:]]+RS0030", probe, StringComparison.Ordinal);

        var verify = File.ReadAllText(Path.Combine(repositoryRoot, "bin", "verify.sh"));
        var workflow = File.ReadAllText(
            Path.Combine(repositoryRoot, ".github", "workflows", "ci.yml")
        );
        Assert.Equal(
            1,
            Regex.Count(
                verify,
                @"(?m)^\s*bash bin/check-banned-api\.sh\s*\|\|",
                RegexOptions.None,
                RegexTimeout
            )
        );
        Assert.Equal(
            1,
            Regex.Count(
                workflow,
                @"(?m)^\s*- run: bash bin/check-banned-api\.sh\s*$",
                RegexOptions.None,
                RegexTimeout
            )
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

    [Fact]
    public void DecisionLogRecordsTask14AnalyzerFormatterMapperAndValidatorChoices()
    {
        var decisionLog = File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "docs", "architecture", "decision-log.md")
        );

        Assert.Contains("D16", decisionLog, StringComparison.Ordinal);
        Assert.Contains(
            "Backend compile-time safety toolchain",
            decisionLog,
            StringComparison.Ordinal
        );
        Assert.Contains("BannedApiAnalyzers", decisionLog, StringComparison.Ordinal);
        Assert.Contains("CSharpier", decisionLog, StringComparison.Ordinal);
        Assert.Contains("Riok.Mapperly", decisionLog, StringComparison.Ordinal);
        Assert.Contains("OptionsValidator", decisionLog, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> ReadExpectedPackageVersions(string repositoryRoot)
    {
        var versions = ReadTargetPackageVersions(repositoryRoot)
            .Where(pair => !string.Equals(pair.Key, "TypeGen", StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        versions.Remove("Microsoft.NET.Test.Sdk");
        versions.Remove("coverlet.collector");
        versions.Remove("xunit");
        versions.Remove("xunit.runner.visualstudio");
        versions.Remove("TngTech.ArchUnitNET.xUnit");
        var task14Versions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Microsoft.Extensions.Options"] = "10.0.10",
            ["Riok.Mapperly"] = "4.3.1",
            ["xunit.analyzers"] = "1.27.0",
        };

        foreach (var package in task14Versions)
        {
            Assert.True(
                versions.TryAdd(package.Key, package.Value),
                $"Task 14 package '{package.Key}' duplicates a Task 13 central pin."
            );
        }

        var task20Versions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["dbup-postgresql"] = "7.0.1",
            ["Microsoft.Identity.Web"] = "4.14.2",
            ["CsCheck"] = "4.4.0",
            ["Microsoft.Testing.Extensions.CodeCoverage"] = "17.14.2",
            ["xunit.v3"] = "3.2.2",
            ["TngTech.ArchUnitNET"] = "0.11.0",
        };
        foreach (var package in task20Versions)
        {
            Assert.True(
                versions.TryAdd(package.Key, package.Value),
                $"{package.Key} is pinned by more than one task"
            );
        }

        AddTask17Versions(versions);
        AddTask21Versions(versions);

        return versions;
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
            var header = Regex.Match(
                lines[lineIndex],
                @"^(?<indent>[ \t]*)steps:[ \t]*(?:#.*)?$",
                RegexOptions.None,
                RegexTimeout
            );
            if (!header.Success)
            {
                lineIndex++;
                continue;
            }

            var headerIndent = header.Groups["indent"].Value.Length;
            lineIndex = AddWorkflowSteps(lines, lineIndex + 1, headerIndent, steps);
        }

        return steps;
    }

    private static int AddWorkflowSteps(
        string[] lines,
        int cursor,
        int headerIndent,
        List<string> steps
    )
    {
        while (cursor < lines.Length)
        {
            if (IsYamlContentLine(lines[cursor]) && GetIndentLength(lines[cursor]) <= headerIndent)
            {
                break;
            }

            var stepStart = Regex.Match(
                lines[cursor],
                @"^(?<indent>[ \t]*)-[ \t]+",
                RegexOptions.None,
                RegexTimeout
            );
            if (!stepStart.Success || stepStart.Groups["indent"].Value.Length <= headerIndent)
            {
                cursor++;
                continue;
            }

            var stepIndent = stepStart.Groups["indent"].Value.Length;
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

                var nextStep = Regex.Match(
                    lines[stepEnd],
                    @"^(?<indent>[ \t]*)-[ \t]+",
                    RegexOptions.None,
                    RegexTimeout
                );
                if (nextStep.Success && nextStep.Groups["indent"].Value.Length == stepIndent)
                {
                    break;
                }

                stepEnd++;
            }

            steps.Add(string.Join(Environment.NewLine, lines[cursor..stepEnd]));
            cursor = stepEnd;
        }

        return cursor;
    }

    private static bool IsYamlContentLine(string line) =>
        !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith('#');

    private static int GetIndentLength(string line) =>
        line.TakeWhile(character => character is ' ' or '\t').Count();

    private static string[] FindOperationalDotnetFiles(string repositoryRoot)
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var directories = new Stack<string>();
        directories.Push(repositoryRoot);

        while (directories.TryPop(out var directory))
        {
            foreach (var subdirectory in Directory.EnumerateDirectories(directory))
            {
                var name = Path.GetFileName(subdirectory);
                var isNestedBuildOutput =
                    name.Equals("bin", StringComparison.OrdinalIgnoreCase)
                    && !directory.Equals(repositoryRoot, StringComparison.OrdinalIgnoreCase);
                if (!ExcludedOperationalDirectories.Contains(name) && !isNestedBuildOutput)
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
                    !relativePath.Contains('/')
                    && (
                        extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
                        || extension.Equals(".props", StringComparison.OrdinalIgnoreCase)
                        || extension.Equals(".targets", StringComparison.OrdinalIgnoreCase)
                        || extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase)
                        || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase)
                    );
                var isDotnetConfiguration =
                    relativePath.StartsWith(".config/", StringComparison.OrdinalIgnoreCase)
                    && extension.Equals(".json", StringComparison.OrdinalIgnoreCase);
                var isWorkflow =
                    relativePath.StartsWith(
                        ".github/workflows/",
                        StringComparison.OrdinalIgnoreCase
                    )
                    && (
                        extension.Equals(".yml", StringComparison.OrdinalIgnoreCase)
                        || extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase)
                    );

                if (
                    isRootConfiguration
                    || isDotnetConfiguration
                    || isWorkflow
                    || OperationalScriptExtensions.Contains(extension)
                    || fileName.StartsWith("Dockerfile", StringComparison.OrdinalIgnoreCase)
                )
                {
                    files.Add(path);
                }
            }
        }

        return files.Order(StringComparer.Ordinal).ToArray();
    }

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
                    Path.Combine(repositoryRoot, "src", "Aspire"),
                    "*.csproj",
                    SearchOption.AllDirectories
                )
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

    private static void AddTask17Versions(Dictionary<string, string> versions)
    {
        var task17Versions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["WolverineFx"] = "5.40.1",
            ["WolverineFx.Postgresql"] = "5.40.1",
        };
        foreach (var package in task17Versions)
        {
            Assert.True(
                versions.TryAdd(package.Key, package.Value),
                $"Task 17 package '{package.Key}' duplicates an existing central pin."
            );
        }
    }

    private static void AddTask21Versions(Dictionary<string, string> versions)
    {
        var task21Versions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Aspire.Hosting.AppHost"] = "13.4.6",
            ["Aspire.Hosting.JavaScript"] = "13.4.6",
            ["Aspire.Hosting.PostgreSQL"] = "13.4.6",
            ["Aspire.Hosting.Testing"] = "13.4.6",
            ["Microsoft.Extensions.ServiceDiscovery"] = "10.8.0",
            ["Npgsql.OpenTelemetry"] = "10.0.3",
            ["OpenTelemetry.Exporter.OpenTelemetryProtocol"] = "1.17.0",
            ["OpenTelemetry.Extensions.Hosting"] = "1.17.0",
            ["OpenTelemetry.Instrumentation.AspNetCore"] = "1.17.0",
            ["OpenTelemetry.Instrumentation.Http"] = "1.17.0",
            ["OpenTelemetry.Instrumentation.Runtime"] = "1.17.0",
        };
        foreach (var package in task21Versions)
        {
            Assert.True(
                versions.TryAdd(package.Key, package.Value),
                $"Task 21 package '{package.Key}' duplicates an existing central pin."
            );
        }
    }
}
