using Aspire.Hosting.Minecraft.Rcon;
using Xunit;

namespace Aspire.Hosting.Minecraft.Integration.Tests.Helpers;

/// <summary>
/// Helper methods for asserting Minecraft block state via RCON.
/// Uses 'execute if block' which returns empty string on match.
/// </summary>
public static class RconAssertions
{
    /// <summary>
    /// Asserts that the block at (x, y, z) matches the expected block type.
    /// </summary>
    public static async Task AssertBlockAsync(
        RconClient rcon, int x, int y, int z, string expectedBlock)
    {
        var result = await rcon.SendCommandAsync(
            $"execute if block {x} {y} {z} {expectedBlock}");

        Assert.True(
            string.IsNullOrEmpty(result),
            $"Expected {expectedBlock} at ({x}, {y}, {z}) but block did not match. RCON response: '{result}'");
    }
}
