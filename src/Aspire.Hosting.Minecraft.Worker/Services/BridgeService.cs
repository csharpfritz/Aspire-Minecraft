using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Builds walkway bridges wherever village streets cross canals, so NPCs and horses
/// can traverse the town without falling into water. Each bridge arches up 2 blocks
/// above the canal water surface to allow boats to pass underneath.
///
/// Bridge locations:
/// - Central boulevard bridges: where the main boulevard between column 0 and column 1 crosses each E-W canal
///
/// Called by MinecraftWorldWorker after canals are built and before rails are placed.
/// </summary>
internal sealed class BridgeService(
    RconService rcon,
    AspireResourceMonitor monitor,
    ILogger<BridgeService> logger)
{
    private bool _bridgesBuilt;

    /// <summary>
    /// One-time initialization: builds walkway bridges at all street-canal intersections.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_bridgesBuilt) return;

        logger.LogInformation("Bridge service initializing...");
        using var burst = rcon.EnterBurstMode(40);
        await BuildAllBridgesAsync(ct);
        _bridgesBuilt = true;
        logger.LogInformation("Bridge service initialized");
    }

    /// <summary>
    /// Updates bridge state each worker cycle. Currently a no-op.
    /// </summary>
    public Task UpdateAsync(CancellationToken ct = default) => Task.CompletedTask;

    private async Task BuildAllBridgesAsync(CancellationToken ct)
    {
        var resources = monitor.Resources;
        var orderedNames = VillageLayout.ReorderByDependency(resources);
        if (orderedNames.Count == 0) return;

        // Boulevard center X — midpoint of the gap between column 0 and column 1
        var boulevardCenterX = VillageLayout.BaseX + VillageLayout.StructureSize
            + (VillageLayout.Spacing - VillageLayout.StructureSize) / 2;

        // Track unique canal Z values to avoid duplicate boulevard bridges (same-row buildings share Z)
        var bridgedCanalZs = new HashSet<int>();

        for (var i = 0; i < orderedNames.Count; i++)
        {
            var (ox, _, oz) = VillageLayout.GetStructureOrigin(orderedNames[i], i);
            var canalCenterZ = oz + VillageLayout.StructureSize + 4;

            // Boulevard bridge — one per unique canal Z
            if (bridgedCanalZs.Add(canalCenterZ))
            {
                await BuildNorthSouthBridgeAsync(boulevardCenterX, canalCenterZ, 5, ct);
                logger.LogInformation("Boulevard walkway bridge at X={X}, Z={Z}", boulevardCenterX, canalCenterZ);
            }
        }
    }

    /// <summary>
    /// Builds a N-S walkway bridge arching over an E-W canal.
    /// Shape: 2-step ramp up → flat deck over canal → 2-step ramp down.
    /// Deck height: SurfaceY+2 (2 blocks of air above canal water at SurfaceY-1).
    /// Materials: stone brick deck, stone brick stairs for ramps, stone brick wall railings.
    /// </summary>
    private async Task BuildNorthSouthBridgeAsync(int centerX, int canalCenterZ, int width, CancellationToken ct)
    {
        var halfW = width / 2;
        var sy = VillageLayout.SurfaceY;
        var deckY = sy + 2;

        var westX = centerX - halfW;
        var eastX = centerX + halfW;

        // Canal wall Z extents (from CanalService geometry)
        var wallZSouth = canalCenterZ - VillageLayout.CanalWaterWidth / 2 - 1;
        var wallZNorth = canalCenterZ + VillageLayout.CanalWaterWidth / 2 + 1;

        // Clear air above bridge area so ramps and deck have room
        await rcon.SendCommandAsync(
            $"fill {westX} {sy + 1} {wallZSouth - 2} {eastX} {deckY + 1} {wallZNorth + 2} minecraft:air",
            CommandPriority.Normal, ct);

        // --- South ramp (approaching from lower Z) ---
        // Step 1: stairs at SurfaceY+1
        await rcon.SendCommandAsync(
            $"fill {westX} {sy + 1} {wallZSouth - 2} {eastX} {sy + 1} {wallZSouth - 2} minecraft:stone_brick_stairs[facing=south,half=bottom]",
            CommandPriority.Normal, ct);
        // Step 2: stairs at SurfaceY+2
        await rcon.SendCommandAsync(
            $"fill {westX} {deckY} {wallZSouth - 1} {eastX} {deckY} {wallZSouth - 1} minecraft:stone_brick_stairs[facing=south,half=bottom]",
            CommandPriority.Normal, ct);

        // --- Bridge deck (spans the full canal width including walls) ---
        await rcon.SendCommandAsync(
            $"fill {westX} {deckY} {wallZSouth} {eastX} {deckY} {wallZNorth} minecraft:stone_bricks",
            CommandPriority.Normal, ct);

        // --- North ramp (exiting toward higher Z) ---
        await rcon.SendCommandAsync(
            $"fill {westX} {deckY} {wallZNorth + 1} {eastX} {deckY} {wallZNorth + 1} minecraft:stone_brick_stairs[facing=north,half=bottom]",
            CommandPriority.Normal, ct);
        await rcon.SendCommandAsync(
            $"fill {westX} {sy + 1} {wallZNorth + 2} {eastX} {sy + 1} {wallZNorth + 2} minecraft:stone_brick_stairs[facing=north,half=bottom]",
            CommandPriority.Normal, ct);

        // --- Support pillars (extend canal walls up to deck under the bridge) ---
        await rcon.SendCommandAsync(
            $"fill {westX} {sy + 1} {wallZSouth} {eastX} {sy + 1} {wallZSouth} minecraft:stone_bricks",
            CommandPriority.Normal, ct);
        await rcon.SendCommandAsync(
            $"fill {westX} {sy + 1} {wallZNorth} {eastX} {sy + 1} {wallZNorth} minecraft:stone_bricks",
            CommandPriority.Normal, ct);

        // --- Railings (stone brick walls on the outer bridge edges) ---
        await rcon.SendCommandAsync(
            $"fill {westX} {deckY + 1} {wallZSouth} {westX} {deckY + 1} {wallZNorth} minecraft:stone_brick_wall",
            CommandPriority.Normal, ct);
        await rcon.SendCommandAsync(
            $"fill {eastX} {deckY + 1} {wallZSouth} {eastX} {deckY + 1} {wallZNorth} minecraft:stone_brick_wall",
            CommandPriority.Normal, ct);
    }
}
