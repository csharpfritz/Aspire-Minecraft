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
}
