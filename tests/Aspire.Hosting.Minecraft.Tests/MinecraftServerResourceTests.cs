using Xunit;

namespace Aspire.Hosting.Minecraft.Tests;

public class MinecraftServerResourceTests
{
    [Fact]
    public void Constructor_SetsName()
    {
        var resource = new MinecraftServerResource("my-mc-server");
        Assert.Equal("my-mc-server", resource.Name);
    }

    [Fact]
    public void GameEndpointName_IsExpectedValue()
    {
        Assert.Equal("game", MinecraftServerResource.GameEndpointName);
    }

    [Fact]
    public void RconEndpointName_IsExpectedValue()
    {
        Assert.Equal("rcon", MinecraftServerResource.RconEndpointName);
    }

    [Fact]
    public void BlueMapEndpointName_IsExpectedValue()
    {
        Assert.Equal("bluemap", MinecraftServerResource.BlueMapEndpointName);
    }

    [Fact]
    public void RconPasswordParameter_DefaultsToNull()
    {
        var resource = new MinecraftServerResource("test");
        Assert.Null(resource.RconPasswordParameter);
    }

    [Fact]
    public void WorkerBuilder_DefaultsToNull()
    {
        var resource = new MinecraftServerResource("test");
        Assert.Null(resource.WorkerBuilder);
    }
}
