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
            "ASPIRE_FEATURE_REDSTONE_GRAPH",
            "ASPIRE_FEATURE_SWITCHES",
            "ASPIRE_FEATURE_PEACEFUL",
            "ASPIRE_FEATURE_REDSTONE_DASHBOARD",
            "ASPIRE_FEATURE_GRAND_VILLAGE",
            "ASPIRE_FEATURE_MINECART_RAILS",
        };

        foreach (var envVar in expectedFeatureVars)
        {
            Assert.True(envVars.ContainsKey(envVar),
                $"Expected environment variable '{envVar}' to be set but it was not found.");
            Assert.Equal("true", envVars[envVar]);
        }

        // 19 ASPIRE_FEATURE_ env vars + 1 debug logging env var = 20 total from WithAllFeatures
        var featureVars = envVars.Keys.Where(k => k.StartsWith("ASPIRE_FEATURE_")).ToList();
        Assert.Equal(19, featureVars.Count);
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
}
