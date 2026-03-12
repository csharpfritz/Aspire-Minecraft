using Aspire.Hosting.ApplicationModel;
using Xunit;

namespace Aspire.Hosting.Minecraft.Tests;

public class WithExternalAccessTests
{
    private static IResourceBuilder<MinecraftServerResource> CreateMinecraftBuilder()
    {
        var builder = DistributedApplication.CreateBuilder();
        return builder.AddMinecraftServer("mc");
    }

    private static EndpointAnnotation? GetEndpointAnnotation(IResource resource, string endpointName)
    {
        return resource.Annotations
            .OfType<EndpointAnnotation>()
            .FirstOrDefault(a => a.Name == endpointName);
    }

    [Fact]
    public void WithExternalAccess_MarksGameEndpointAsExternal()
    {
        var mc = CreateMinecraftBuilder();

        mc.WithExternalAccess();

        var endpoint = GetEndpointAnnotation(mc.Resource, MinecraftServerResource.GameEndpointName);
        Assert.NotNull(endpoint);
        Assert.True(endpoint.IsExternal, "Game endpoint should be marked as external after WithExternalAccess().");
    }

    [Fact]
    public void WithExternalAccess_MarksRconEndpointAsExternal()
    {
        var mc = CreateMinecraftBuilder();

        mc.WithExternalAccess();

        var endpoint = GetEndpointAnnotation(mc.Resource, MinecraftServerResource.RconEndpointName);
        Assert.NotNull(endpoint);
        Assert.True(endpoint.IsExternal, "RCON endpoint should be marked as external after WithExternalAccess().");
    }

    [Fact]
    public void WithExternalAccess_MarksBlueMapEndpointAsExternal_WhenPresent()
    {
        var mc = CreateMinecraftBuilder();
        mc.WithBlueMap();

        mc.WithExternalAccess();

        var endpoint = GetEndpointAnnotation(mc.Resource, MinecraftServerResource.BlueMapEndpointName);
        Assert.NotNull(endpoint);
        Assert.True(endpoint.IsExternal, "BlueMap endpoint should be marked as external when configured before WithExternalAccess().");
    }

    [Fact]
    public void WithExternalAccess_DoesNotThrow_WhenBlueMapNotConfigured()
    {
        var mc = CreateMinecraftBuilder();

        // Should not throw — BlueMap endpoint simply doesn't exist, so nothing to mark
        var exception = Record.Exception(() => mc.WithExternalAccess());

        Assert.Null(exception);
    }

    [Fact]
    public void DefaultEndpoints_AreNotExternal()
    {
        var mc = CreateMinecraftBuilder();

        var gameEndpoint = GetEndpointAnnotation(mc.Resource, MinecraftServerResource.GameEndpointName);
        var rconEndpoint = GetEndpointAnnotation(mc.Resource, MinecraftServerResource.RconEndpointName);

        Assert.NotNull(gameEndpoint);
        Assert.NotNull(rconEndpoint);
        Assert.False(gameEndpoint.IsExternal, "Game endpoint should NOT be external by default.");
        Assert.False(rconEndpoint.IsExternal, "RCON endpoint should NOT be external by default.");
    }

    [Fact]
    public void WithExternalAccess_ReturnsBuilderForChaining()
    {
        var mc = CreateMinecraftBuilder();

        var result = mc.WithExternalAccess();

        Assert.Same(mc, result);
    }

    [Fact]
    public void WithExternalAccess_CalledTwice_DoesNotThrow()
    {
        var mc = CreateMinecraftBuilder();

        var exception = Record.Exception(() =>
        {
            mc.WithExternalAccess();
            mc.WithExternalAccess();
        });

        Assert.Null(exception);
    }

    [Fact]
    public void WithExternalAccess_CalledTwice_EndpointsStillExternal()
    {
        var mc = CreateMinecraftBuilder();

        mc.WithExternalAccess();
        mc.WithExternalAccess();

        var gameEndpoint = GetEndpointAnnotation(mc.Resource, MinecraftServerResource.GameEndpointName);
        var rconEndpoint = GetEndpointAnnotation(mc.Resource, MinecraftServerResource.RconEndpointName);
        Assert.NotNull(gameEndpoint);
        Assert.NotNull(rconEndpoint);
        Assert.True(gameEndpoint.IsExternal, "Game endpoint should remain external after double call.");
        Assert.True(rconEndpoint.IsExternal, "RCON endpoint should remain external after double call.");
    }

    [Fact]
    public void WithExternalAccess_BeforeBlueMap_BlueMapNotMarkedExternal()
    {
        // WithExternalAccess iterates current annotations — BlueMap endpoint
        // doesn't exist yet when called first, so it can't be marked external.
        var mc = CreateMinecraftBuilder();

        mc.WithExternalAccess();
        mc.WithBlueMap();

        var blueMapEndpoint = GetEndpointAnnotation(mc.Resource, MinecraftServerResource.BlueMapEndpointName);
        Assert.NotNull(blueMapEndpoint);
        Assert.False(blueMapEndpoint.IsExternal,
            "BlueMap endpoint should NOT be external when WithExternalAccess() is called before WithBlueMap() — the endpoint doesn't exist yet.");
    }

    [Fact]
    public void WithExternalAccess_AfterBlueMap_AllThreeEndpointsExternal()
    {
        var mc = CreateMinecraftBuilder();

        mc.WithBlueMap();
        mc.WithExternalAccess();

        var gameEndpoint = GetEndpointAnnotation(mc.Resource, MinecraftServerResource.GameEndpointName);
        var rconEndpoint = GetEndpointAnnotation(mc.Resource, MinecraftServerResource.RconEndpointName);
        var blueMapEndpoint = GetEndpointAnnotation(mc.Resource, MinecraftServerResource.BlueMapEndpointName);

        Assert.NotNull(gameEndpoint);
        Assert.NotNull(rconEndpoint);
        Assert.NotNull(blueMapEndpoint);
        Assert.True(gameEndpoint.IsExternal, "Game endpoint should be external.");
        Assert.True(rconEndpoint.IsExternal, "RCON endpoint should be external.");
        Assert.True(blueMapEndpoint.IsExternal, "BlueMap endpoint should be external when WithBlueMap() called first.");
    }

    [Fact]
    public void FluentChain_WithBlueMapThenExternalAccess_Works()
    {
        var builder = DistributedApplication.CreateBuilder();

        var mc = builder.AddMinecraftServer("mc")
            .WithBlueMap()
            .WithExternalAccess();

        var gameEndpoint = GetEndpointAnnotation(mc.Resource, MinecraftServerResource.GameEndpointName);
        var rconEndpoint = GetEndpointAnnotation(mc.Resource, MinecraftServerResource.RconEndpointName);
        var blueMapEndpoint = GetEndpointAnnotation(mc.Resource, MinecraftServerResource.BlueMapEndpointName);

        Assert.NotNull(gameEndpoint);
        Assert.NotNull(rconEndpoint);
        Assert.NotNull(blueMapEndpoint);
        Assert.True(gameEndpoint.IsExternal);
        Assert.True(rconEndpoint.IsExternal);
        Assert.True(blueMapEndpoint.IsExternal);
    }

    [Fact]
    public void FluentChain_WithExternalAccessThenBlueMap_GameAndRconExternal()
    {
        var builder = DistributedApplication.CreateBuilder();

        var mc = builder.AddMinecraftServer("mc")
            .WithExternalAccess()
            .WithBlueMap();

        var gameEndpoint = GetEndpointAnnotation(mc.Resource, MinecraftServerResource.GameEndpointName);
        var rconEndpoint = GetEndpointAnnotation(mc.Resource, MinecraftServerResource.RconEndpointName);

        Assert.NotNull(gameEndpoint);
        Assert.NotNull(rconEndpoint);
        Assert.True(gameEndpoint.IsExternal, "Game endpoint should be external regardless of chain order.");
        Assert.True(rconEndpoint.IsExternal, "RCON endpoint should be external regardless of chain order.");
    }

    [Fact]
    public void WithExternalAccess_IgnoresUnrelatedEndpoints()
    {
        var mc = CreateMinecraftBuilder();

        // Add a custom endpoint that WithExternalAccess should NOT touch
        mc.WithEndpoint(targetPort: 9999, name: "custom-metrics", scheme: "http");
        mc.WithExternalAccess();

        var customEndpoint = GetEndpointAnnotation(mc.Resource, "custom-metrics");
        Assert.NotNull(customEndpoint);
        Assert.False(customEndpoint.IsExternal,
            "WithExternalAccess should only affect game, RCON, and BlueMap endpoints — not custom ones.");
    }
}
