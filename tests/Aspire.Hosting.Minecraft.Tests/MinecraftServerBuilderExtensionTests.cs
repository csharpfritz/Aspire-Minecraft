using Aspire.Hosting.ApplicationModel;
using Xunit;

namespace Aspire.Hosting.Minecraft.Tests;

/// <summary>
/// Stub IProjectMetadata for testing extension methods that require WithAspireWorldDisplay.
/// Points to the test project's own .csproj so Aspire launch profile resolution succeeds.
/// </summary>
internal class TestProjectMetadata : IProjectMetadata
{
    public string ProjectPath { get; } = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "Aspire.Hosting.Minecraft.Tests.csproj");
}

public class MinecraftServerBuilderExtensionTests
{
    private static IResourceBuilder<MinecraftServerResource> CreateMinecraftBuilderWithWorker()
    {
        var builder = DistributedApplication.CreateBuilder();
        var mc = builder.AddMinecraftServer("mc")
            .WithAspireWorldDisplay<TestProjectMetadata>();
        return mc;
    }

    /// <summary>
    /// Collects environment variables from a resource by executing all EnvironmentCallbackAnnotation callbacks.
    /// </summary>
    private static async Task<Dictionary<string, string>> GetEnvironmentVariablesAsync(IResource resource)
    {
        var annotations = resource.Annotations.OfType<EnvironmentCallbackAnnotation>();
        var envVars = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(DistributedApplicationOperation.Publish),
            envVars);

        foreach (var annotation in annotations)
        {
            await annotation.Callback(context);
        }

        return envVars.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value?.ToString() ?? "");
    }

    [Fact]
    public async Task WithAllFeatures_SetsAllFeatureEnvVars()
    {
        var mc = CreateMinecraftBuilderWithWorker();

        mc.WithAllFeatures();

        var workerResource = mc.Resource.WorkerBuilder!.Resource;
        var envVars = await GetEnvironmentVariablesAsync(workerResource);

        var expectedFeatureVars = new[]
        {
            "ASPIRE_FEATURE_PARTICLES",
            "ASPIRE_FEATURE_TITLE_ALERTS",
            "ASPIRE_FEATURE_WEATHER",
            "ASPIRE_FEATURE_BOSSBAR",
            "ASPIRE_FEATURE_SOUNDS",
            "ASPIRE_FEATURE_ACTIONBAR",
            "ASPIRE_FEATURE_BEACONS",
            "ASPIRE_FEATURE_FIREWORKS",
            "ASPIRE_FEATURE_GUARDIANS",
            "ASPIRE_FEATURE_FANFARE",
            "ASPIRE_FEATURE_WORLDBORDER",
            "ASPIRE_FEATURE_ACHIEVEMENTS",
            "ASPIRE_FEATURE_HEARTBEAT",
            "ASPIRE_FEATURE_SWITCHES",
            "ASPIRE_FEATURE_PEACEFUL",
            "ASPIRE_FEATURE_REDSTONE_DASHBOARD",
            "ASPIRE_FEATURE_MINECART_RAILS",
            "ASPIRE_FEATURE_ERROR_BOATS",
            "ASPIRE_FEATURE_CANALS",
            "ASPIRE_FEATURE_NEIGHBORHOODS",
        };

        foreach (var envVar in expectedFeatureVars)
        {
            Assert.True(envVars.ContainsKey(envVar),
                $"Expected environment variable '{envVar}' to be set but it was not found.");
            Assert.Equal("true", envVars[envVar]);
        }

        // 20 ASPIRE_FEATURE_ env vars + 1 debug logging env var = 21 total from WithAllFeatures
        var featureVars = envVars.Keys.Where(k => k.StartsWith("ASPIRE_FEATURE_")).ToList();
        Assert.Equal(20, featureVars.Count);
    }

    [Fact]
    public async Task WithRedstoneDashboard_SetsEnvVar()
    {
        var mc = CreateMinecraftBuilderWithWorker();

        mc.WithRedstoneDashboard();

        var workerResource = mc.Resource.WorkerBuilder!.Resource;
        var envVars = await GetEnvironmentVariablesAsync(workerResource);

        Assert.True(envVars.ContainsKey("ASPIRE_FEATURE_REDSTONE_DASHBOARD"),
            "Expected ASPIRE_FEATURE_REDSTONE_DASHBOARD to be set.");
        Assert.Equal("true", envVars["ASPIRE_FEATURE_REDSTONE_DASHBOARD"]);
    }

    [Fact]
    public void ParseSquadAgentNames_ExtractsActiveMembers()
    {
        var content = """
            # Team Roster

            ## Members

            | Name | Role | Charter | Status |
            |------|------|---------|--------|
            | Rhodey | Lead | charter.md | ✅ Active |
            | Shuri | Backend Dev | charter.md | ✅ Active |
            | Rocket | Integration Dev | charter.md | ✅ Active |
            | Scribe | Session Logger | charter.md | 📋 Silent |
            | Ralph | Work Monitor | — | 🔄 Monitor |
            """;

        var names = MinecraftServerBuilderExtensions.ParseSquadAgentNames(content);

        Assert.Equal(3, names.Count);
        Assert.Contains("Rhodey", names);
        Assert.Contains("Shuri", names);
        Assert.Contains("Rocket", names);
        Assert.DoesNotContain("Scribe", names);
        Assert.DoesNotContain("Ralph", names);
    }

    [Fact]
    public void ParseSquadAgentNames_ExcludesSilentAndMonitorStatus()
    {
        var content = """
            ## Members

            | Name | Role | Charter | Status |
            |------|------|---------|--------|
            | Alpha | Dev | c.md | ✅ Active |
            | Beta | Ops | c.md | 📋 Silent Observer |
            | Gamma | Test | c.md | 🔄 Monitor Mode |
            | Delta | Lead | c.md | ✅ Active |
            """;

        var names = MinecraftServerBuilderExtensions.ParseSquadAgentNames(content);

        Assert.Equal(2, names.Count);
        Assert.Contains("Alpha", names);
        Assert.Contains("Delta", names);
        Assert.DoesNotContain("Beta", names);
        Assert.DoesNotContain("Gamma", names);
    }

    [Fact]
    public void ParseSquadAgentNames_ReturnsEmptyForNoMembersSection()
    {
        var content = """
            # Team Roster

            ## Coordinator

            | Name | Role | Notes |
            |------|------|-------|
            | Squad | Coordinator | Routes work. |
            """;

        var names = MinecraftServerBuilderExtensions.ParseSquadAgentNames(content);
        Assert.Empty(names);
    }

    [Fact]
    public void ParseSquadAgentNames_ReturnsEmptyForEmptyContent()
    {
        var names = MinecraftServerBuilderExtensions.ParseSquadAgentNames("");
        Assert.Empty(names);
    }

    [Fact]
    public void ParseSquadAgentNames_HandlesFullTeamFile()
    {
        var content = """
            # Team Roster

            > .NET Aspire integration for Minecraft servers.

            ## Coordinator

            | Name | Role | Notes |
            |------|------|-------|
            | Squad | Coordinator | Routes work. |

            ## Members

            | Name | Role | Charter | Status |
            |------|------|---------|--------|
            | Rhodey | Lead | `.squad/agents/rhodey/charter.md` | ✅ Active |
            | Shuri | Backend Dev | `.squad/agents/shuri/charter.md` | ✅ Active |
            | Rocket | Integration Dev | `.squad/agents/rocket/charter.md` | ✅ Active |
            | Nebula | Tester | `.squad/agents/nebula/charter.md` | ✅ Active |
            | Hawkeye | Playwright Expert | `.squad/agents/hawkeye/charter.md` | ✅ Active |
            | Mantis | Blogger | `.squad/agents/mantis/charter.md` | ✅ Active |
            | Wong | GitHub Ops | `.squad/agents/wong/charter.md` | ✅ Active |
            | Vision | Technical Writer | `.squad/agents/vision/charter.md` | ✅ Active |
            | Scribe | Session Logger | `.squad/agents/scribe/charter.md` | 📋 Silent |
            | Ralph | Work Monitor | — | 🔄 Monitor |

            ## Issue Source

            | Field | Value |
            |-------|-------|
            | **Repository** | csharpfritz/Aspire-Minecraft |
            """;

        var names = MinecraftServerBuilderExtensions.ParseSquadAgentNames(content);

        Assert.Equal(8, names.Count);
        var expected = new[] { "Rhodey", "Shuri", "Rocket", "Nebula", "Hawkeye", "Mantis", "Wong", "Vision" };
        Assert.Equal(expected, names);
    }

    [Fact]
    public void FindSquadTeamFile_ReturnsNullWhenNotFound()
    {
        // Use a temp directory with no .squad folder
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var result = MinecraftServerBuilderExtensions.FindSquadTeamFile(tempDir);
            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void FindSquadTeamFile_FindsFileInParentDirectory()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var squadDir = Path.Combine(tempRoot, ".squad");
        var childDir = Path.Combine(tempRoot, "src", "AppHost");
        Directory.CreateDirectory(squadDir);
        Directory.CreateDirectory(childDir);
        File.WriteAllText(Path.Combine(squadDir, "team.md"), "## Members\n");
        try
        {
            var result = MinecraftServerBuilderExtensions.FindSquadTeamFile(childDir);
            Assert.NotNull(result);
            Assert.EndsWith("team.md", result);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}
