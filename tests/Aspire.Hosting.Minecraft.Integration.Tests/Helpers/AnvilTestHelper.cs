using Aspire.Hosting.Minecraft.Integration.Tests.Fixtures;
using Xunit;

namespace Aspire.Hosting.Minecraft.Integration.Tests.Helpers;

/// <summary>
/// Convenience wrapper that combines RCON world-save flushing with <see cref="AnvilRegionReader"/>
/// for verifying block placement via MCA file inspection. All methods handle a null
/// <see cref="MinecraftAppFixture.WorldSaveDirectory"/> gracefully.
///
/// <para><b>Usage pattern:</b> Call <c>VerifyBlockAsync</c> or <c>VerifyBlockRangeAsync</c> to
/// assert blocks were placed correctly. Each method sends <c>save-all flush</c> via RCON first,
/// waits for the disk write, then reads the Anvil region file directly.</para>
/// </summary>
public static class AnvilTestHelper
{
    /// <summary>Delay after <c>save-all flush</c> to allow the server to write chunks to disk.</summary>
    private static readonly TimeSpan SaveFlushDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Flushes the world save via RCON and reads a single block from the MCA region file.
    /// Returns null if the world save directory is unavailable or the block cannot be read.
    /// </summary>
    /// <param name="fixture">The shared Minecraft app fixture.</param>
    /// <param name="worldX">World X coordinate.</param>
    /// <param name="worldY">World Y coordinate.</param>
    /// <param name="worldZ">World Z coordinate.</param>
    /// <returns>The <see cref="BlockState"/> at the given position, or null.</returns>
    public static async Task<BlockState?> GetBlockAsync(
        MinecraftAppFixture fixture, int worldX, int worldY, int worldZ)
    {
        if (fixture.WorldSaveDirectory is null)
            return null;

        // Force the server to write all chunks to disk
        await fixture.Rcon.SendCommandAsync("save-all flush");
        await Task.Delay(SaveFlushDelay);

        var regionPath = AnvilRegionReader.GetRegionFilePath(
            fixture.WorldSaveDirectory, worldX, worldZ);

        if (!File.Exists(regionPath))
            return null;

        using var reader = AnvilRegionReader.Open(regionPath);
        return reader.GetBlockAt(worldX, worldY, worldZ);
    }

    /// <summary>
    /// Flushes the world save and verifies that the block at the given coordinates matches
    /// the expected type. Accepts block names with or without the <c>minecraft:</c> prefix.
    /// Skips the assertion if the world save directory is unavailable.
    /// </summary>
    /// <param name="fixture">The shared Minecraft app fixture.</param>
    /// <param name="worldX">World X coordinate.</param>
    /// <param name="worldY">World Y coordinate.</param>
    /// <param name="worldZ">World Z coordinate.</param>
    /// <param name="expectedBlock">Expected block name (e.g. "stone_bricks" or "minecraft:stone_bricks").</param>
    public static async Task VerifyBlockAsync(
        MinecraftAppFixture fixture, int worldX, int worldY, int worldZ,
        string expectedBlock)
    {
        if (fixture.WorldSaveDirectory is null)
            return; // Graceful skip — world save not accessible

        var block = await GetBlockAsync(fixture, worldX, worldY, worldZ);
        Assert.NotNull(block);

        var expected = NormalizeBlockName(expectedBlock);
        Assert.Equal(expected, block.Name);
    }

    /// <summary>
    /// Flushes the world save and verifies that all blocks in the given axis-aligned box
    /// match the expected type. Useful for checking walls, floors, or roofs.
    /// Skips if the world save directory is unavailable.
    /// </summary>
    /// <param name="fixture">The shared Minecraft app fixture.</param>
    /// <param name="x1">Minimum X.</param>
    /// <param name="y1">Minimum Y.</param>
    /// <param name="z1">Minimum Z.</param>
    /// <param name="x2">Maximum X.</param>
    /// <param name="y2">Maximum Y.</param>
    /// <param name="z2">Maximum Z.</param>
    /// <param name="expectedBlock">Expected block name (e.g. "stone_bricks" or "minecraft:stone_bricks").</param>
    public static async Task VerifyBlockRangeAsync(
        MinecraftAppFixture fixture,
        int x1, int y1, int z1, int x2, int y2, int z2,
        string expectedBlock)
    {
        if (fixture.WorldSaveDirectory is null)
            return; // Graceful skip — world save not accessible

        // Flush once for the entire range
        await fixture.Rcon.SendCommandAsync("save-all flush");
        await Task.Delay(SaveFlushDelay);

        var expected = NormalizeBlockName(expectedBlock);
        var minX = Math.Min(x1, x2);
        var maxX = Math.Max(x1, x2);
        var minY = Math.Min(y1, y2);
        var maxY = Math.Max(y1, y2);
        var minZ = Math.Min(z1, z2);
        var maxZ = Math.Max(z1, z2);

        for (var x = minX; x <= maxX; x++)
        for (var y = minY; y <= maxY; y++)
        for (var z = minZ; z <= maxZ; z++)
        {
            var regionPath = AnvilRegionReader.GetRegionFilePath(
                fixture.WorldSaveDirectory, x, z);

            if (!File.Exists(regionPath))
            {
                Assert.Fail(
                    $"Region file not found for block at ({x}, {y}, {z}): {regionPath}");
                return;
            }

            using var reader = AnvilRegionReader.Open(regionPath);
            var block = reader.GetBlockAt(x, y, z);

            Assert.True(
                block is not null && block.Name == expected,
                $"Expected {expected} at ({x}, {y}, {z}) but found {block?.Name ?? "null"}");
        }
    }

    /// <summary>
    /// Ensures the block name has the <c>minecraft:</c> namespace prefix.
    /// </summary>
    private static string NormalizeBlockName(string blockName) =>
        blockName.Contains(':') ? blockName : $"minecraft:{blockName}";
}
