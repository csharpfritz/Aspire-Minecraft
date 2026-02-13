using Xunit;

namespace Aspire.Hosting.Minecraft.Integration.Tests.Fixtures;

/// <summary>
/// xUnit collection definition that ensures all integration tests share
/// a single <see cref="MinecraftAppFixture"/> instance (one Minecraft server per test run).
/// </summary>
[CollectionDefinition("Minecraft")]
public class MinecraftCollection : ICollectionFixture<MinecraftAppFixture>
{
}
