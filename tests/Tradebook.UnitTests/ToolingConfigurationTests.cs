using System.Text.Json;

namespace Tradebook.UnitTests;

public sealed class ToolingConfigurationTests
{
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
