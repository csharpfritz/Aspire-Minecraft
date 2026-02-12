# BlueMap-Based Integration Testing Strategy

**Author:** Rhodey (Lead)
**Date:** 2026-02-12
**Status:** Proposed
**Requested by:** Jeffrey T. Fritz

## Problem Statement

We need integration tests that verify Minecraft structures are built correctly by the worker after the sample app starts. The village is constructed at known coordinates via RCON fill commands, and BlueMap renders the world as a 3D web map on port 8100. We want confidence that the world-building pipeline works end-to-end.

## Approaches Evaluated

### A. BlueMap REST API — Block-Level Queries

**Verdict: Not viable as primary approach.**

BlueMap does not expose block-level query endpoints. Its web server serves pre-rendered tile files (`/maps/<id>/<lod>/<x>_<z>.json` for geometry, `.png` for images). There is no `/api/block?x=10&y=-59&z=0` endpoint. The Java API (`BlueMapAPI`) provides server-side access but requires a Java plugin — not callable from .NET test code.

BlueMap tile files could theoretically be parsed, but:
- The tile format is undocumented binary/compressed geometry data
- Tile coordinates don't map 1:1 to block coordinates
- Parsing would be fragile and break across BlueMap versions

### B. Playwright Screenshot Comparison

**Verdict: Good for visual regression, poor for correctness assertions.**

Playwright can navigate to `http://localhost:8100`, position the camera over the village, and take screenshots. Pros:
- Tests what users actually see
- Catches rendering regressions
- We already have Playwright MCP tools configured

Cons:
- BlueMap needs 30–60s after `bluemap update` to render chunks
- 3D rendering is non-deterministic (lighting, rotation, anti-aliasing)
- Screenshot comparison requires reference images, tolerance thresholds
- Fragile across BlueMap version updates
- Cannot assert "block at (10, -59, 0) is oak_planks"

### C. RCON-Based Verification

**Verdict: Recommended primary approach.**

Minecraft's `execute if block` command can check block types at exact coordinates:

```
execute if block 10 -59 0 minecraft:oak_planks run say match
```

Response is empty string (success) or error message (no match). For block entities with NBT data, `data get block X Y Z` returns the full NBT.

Pros:
- Exact block-level assertions at known coordinates from `VillageLayout`
- Zero rendering delay — blocks exist immediately after RCON `fill` completes
- Uses our existing `RconClient` library
- Deterministic, fast, reliable
- Tests the actual game state, not a rendering of it

Cons:
- Tests RCON commands, not the visual experience
- Cannot verify BlueMap itself is working

### D. Hybrid — RCON + BlueMap Screenshots

**Verdict: This is the recommended approach.**

Use RCON for authoritative block-level verification (primary) and Playwright for visual smoke tests (secondary). This gives us both correctness guarantees and visual regression coverage.

## Recommended Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                Integration Test Project                      │
│  Aspire.Hosting.Minecraft.IntegrationTests                  │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌──────────────────────────────────────────┐               │
│  │  MinecraftAppFixture : IAsyncLifetime    │               │
│  │                                          │               │
│  │  1. DistributedApplicationTestingBuilder │               │
│  │     → Builds AppHost (MC + Worker)       │               │
│  │  2. Waits for MC server healthy          │               │
│  │  3. Waits for worker to build village    │               │
│  │  4. Exposes RconClient + BlueMap URL     │               │
│  └──────────────┬───────────────────────────┘               │
│                 │                                            │
│        ┌────────┴────────┐                                  │
│        │                 │                                   │
│  ┌─────▼──────┐   ┌─────▼──────────┐                       │
│  │ RCON Tests │   │ BlueMap Tests   │                       │
│  │ (Primary)  │   │ (Visual Smoke)  │                       │
│  │            │   │                 │                       │
│  │ • Block    │   │ • Playwright    │                       │
│  │   checks   │   │   screenshots   │                       │
│  │ • execute  │   │ • Page loads    │                       │
│  │   if block │   │ • Map renders   │                       │
│  │ • Coord    │   │ • Village area  │                       │
│  │   math     │   │   visible       │                       │
│  └────────────┘   └────────────────┘                       │
│                                                              │
└─────────────────────────────────────────────────────────────┘

         Talks to:
         ┌─────────────────────────────────┐
         │  Docker (itzg/minecraft-server)  │
         │  ├─ Minecraft Paper Server       │
         │  │  ├─ RCON on :25575           │
         │  │  ├─ Game on :25565           │
         │  │  └─ BlueMap on :8100         │
         │  └─ Worker (.NET project)        │
         │     └─ Builds village via RCON   │
         └─────────────────────────────────┘
```

## Test Infrastructure Design

### Shared Fixture

All integration tests share a single `MinecraftAppFixture` that starts the full Aspire app once per test run. Starting a Minecraft server takes 30–60s; we cannot afford per-test startup.

```csharp
[CollectionDefinition("Minecraft")]
public class MinecraftCollection : ICollectionFixture<MinecraftAppFixture> { }

public class MinecraftAppFixture : IAsyncLifetime
{
    public DistributedApplication App { get; private set; } = null!;
    public RconClient Rcon { get; private set; } = null!;
    public string BlueMapUrl { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        // 1. Build the Aspire app host
        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.MinecraftAspireDemo_AppHost>();

        App = await builder.BuildAsync();
        await App.StartAsync();

        // 2. Wait for the Minecraft server to be healthy (RCON responds)
        var mc = App.GetResource<MinecraftServerResource>("minecraft");
        Rcon = new RconClient();
        var rconEndpoint = mc.GetEndpoint(MinecraftServerResource.RconEndpointName);
        await Rcon.ConnectAsync(rconEndpoint.Host, rconEndpoint.Port);
        await Rcon.AuthenticateAsync(mc.RconPassword);

        // 3. Wait for the worker to build the village
        await WaitForVillageBuildAsync();

        // 4. Capture BlueMap URL
        var blueMapEndpoint = mc.GetEndpoint(MinecraftServerResource.BlueMapEndpointName);
        BlueMapUrl = $"http://{blueMapEndpoint.Host}:{blueMapEndpoint.Port}";
    }

    private async Task WaitForVillageBuildAsync()
    {
        // Poll for a known block from the first structure (watchtower at index 0)
        // VillageLayout: index 0 → origin (10, SurfaceY+1, 0)
        // Watchtower outer wall uses cobblestone at the origin corner
        var timeout = TimeSpan.FromMinutes(3);
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < timeout)
        {
            var result = await Rcon.SendCommandAsync(
                "execute if block 10 -59 0 minecraft:cobblestone");

            if (string.IsNullOrEmpty(result))
                return; // Block exists — village is built

            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        throw new TimeoutException("Village was not built within timeout.");
    }

    public async Task DisposeAsync()
    {
        await Rcon.DisposeAsync();
        await App.StopAsync();
        await App.DisposeAsync();
    }
}
```

### RCON Block Assertion Helper

```csharp
public static class RconAssertions
{
    /// <summary>
    /// Asserts that the block at (x, y, z) matches the expected block type.
    /// Uses 'execute if block' which returns empty string on match.
    /// </summary>
    public static async Task AssertBlockAsync(
        RconClient rcon, int x, int y, int z, string expectedBlock)
    {
        var result = await rcon.SendCommandAsync(
            $"execute if block {x} {y} {z} {expectedBlock}");

        // Empty response = match. Non-empty = the condition failed.
        Assert.True(
            string.IsNullOrEmpty(result),
            $"Expected {expectedBlock} at ({x}, {y}, {z}) but got mismatch. Response: {result}");
    }

    /// <summary>
    /// Asserts a rectangular region is filled with the expected block.
    /// Checks corners and center to avoid excessive RCON calls.
    /// </summary>
    public static async Task AssertRegionContainsAsync(
        RconClient rcon,
        int x1, int y1, int z1,
        int x2, int y2, int z2,
        string expectedBlock)
    {
        // Check 5 key points: 4 corners + center
        var points = new[]
        {
            (x1, y1, z1), (x2, y2, z2),
            (x1, y2, z2), (x2, y1, z1),
            ((x1 + x2) / 2, (y1 + y2) / 2, (z1 + z2) / 2)
        };

        foreach (var (x, y, z) in points)
        {
            await AssertBlockAsync(rcon, x, y, z, expectedBlock);
        }
    }
}
```

## First 5 Tests

### Test 1: Village Fence Exists

Verifies the oak fence perimeter is placed around the village. Uses `VillageLayout.GetFencePerimeter(4)` to compute expected coordinates and checks corner posts.

```csharp
[Collection("Minecraft")]
public class VillageFenceTests(MinecraftAppFixture fixture)
{
    [Fact]
    public async Task Fence_Perimeter_HasOakFenceAtCorners()
    {
        var (minX, minZ, maxX, maxZ) = VillageLayout.GetFencePerimeter(4);
        var fenceY = VillageLayout.SurfaceY + 1;

        await RconAssertions.AssertBlockAsync(fixture.Rcon, minX, fenceY, minZ, "minecraft:oak_fence");
        await RconAssertions.AssertBlockAsync(fixture.Rcon, maxX, fenceY, minZ, "minecraft:oak_fence");
        await RconAssertions.AssertBlockAsync(fixture.Rcon, minX, fenceY, maxZ, "minecraft:oak_fence");
        await RconAssertions.AssertBlockAsync(fixture.Rcon, maxX, fenceY, maxZ, "minecraft:oak_fence");
    }
}
```

### Test 2: Cobblestone Paths Exist

Verifies the village interior paths are cobblestone at `SurfaceY`.

```csharp
[Fact]
public async Task Paths_Interior_IsCobblestone()
{
    var (fMinX, fMinZ, fMaxX, fMaxZ) = VillageLayout.GetFencePerimeter(4);
    var pathY = VillageLayout.SurfaceY;

    // Check center of path area
    var centerX = (fMinX + fMaxX) / 2;
    var centerZ = (fMinZ + fMaxZ) / 2;

    await RconAssertions.AssertBlockAsync(fixture.Rcon, centerX, pathY, centerZ, "minecraft:cobblestone");
}
```

### Test 3: First Structure (Watchtower) Built at Correct Origin

Verifies the Project-type resource at index 0 has cobblestone walls at the expected coordinates.

```csharp
[Fact]
public async Task Structure_Index0_WatchtowerHasCobblestoneWalls()
{
    var (x, y, z) = VillageLayout.GetStructureOrigin(0);

    // Watchtower: 7x7 cobblestone outer shell at origin
    await RconAssertions.AssertBlockAsync(fixture.Rcon, x, y, z, "minecraft:cobblestone");
    await RconAssertions.AssertBlockAsync(fixture.Rcon, x + 6, y, z, "minecraft:cobblestone");
    await RconAssertions.AssertBlockAsync(fixture.Rcon, x, y, z + 6, "minecraft:cobblestone");
    await RconAssertions.AssertBlockAsync(fixture.Rcon, x + 6, y, z + 6, "minecraft:cobblestone");
}
```

### Test 4: Health Indicator Exists on Structure

Verifies that the health indicator block (green wool for healthy, red for unhealthy) is placed on top of the first structure.

```csharp
[Fact]
public async Task HealthIndicator_HealthyResource_HasGreenWool()
{
    var (x, y, z) = VillageLayout.GetStructureOrigin(0);

    // Health indicator is placed at roof center (x+3, y+height, z+3)
    // Watchtower height is 10 blocks
    var indicatorY = y + 10;
    var indicatorX = x + 3;
    var indicatorZ = z + 3;

    await RconAssertions.AssertBlockAsync(
        fixture.Rcon, indicatorX, indicatorY, indicatorZ, "minecraft:green_wool");
}
```

### Test 5: BlueMap Web UI Loads Successfully

Visual smoke test — verifies BlueMap is running and renders something.

```csharp
[Fact]
public async Task BlueMap_WebUI_LoadsAndRenders()
{
    using var httpClient = new HttpClient();

    // 1. BlueMap root page returns 200
    var response = await httpClient.GetAsync(fixture.BlueMapUrl);
    Assert.True(response.IsSuccessStatusCode, $"BlueMap returned {response.StatusCode}");

    // 2. BlueMap settings endpoint returns JSON with map list
    var settingsResponse = await httpClient.GetAsync($"{fixture.BlueMapUrl}/settings.json");
    Assert.True(settingsResponse.IsSuccessStatusCode);
    var settings = await settingsResponse.Content.ReadAsStringAsync();
    Assert.Contains("maps", settings);
}
```

> **Note:** A future Playwright-based test could navigate to the BlueMap URL, position the camera at the village coordinates, take a screenshot, and compare against a reference image. This is deferred until the RCON tests are stable, because screenshot comparison adds test infrastructure complexity (reference image management, tolerance tuning, BlueMap render timing).

## Test Project Structure

```
tests/
  Aspire.Hosting.Minecraft.IntegrationTests/
    Aspire.Hosting.Minecraft.IntegrationTests.csproj
    Fixtures/
      MinecraftAppFixture.cs
    Helpers/
      RconAssertions.cs
    Village/
      VillageFenceTests.cs
      VillagePathTests.cs
      VillageStructureTests.cs
      HealthIndicatorTests.cs
    BlueMap/
      BlueMapSmokeTests.cs
```

### Project File

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Aspire.Hosting.Testing" Version="10.*" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\samples\MinecraftAspireDemo\MinecraftAspireDemo.AppHost\MinecraftAspireDemo.AppHost.csproj" />
    <ProjectReference Include="..\..\src\Aspire.Hosting.Minecraft.Rcon\Aspire.Hosting.Minecraft.Rcon.csproj" />
  </ItemGroup>
</Project>
```

## CI Pipeline Considerations

### Docker Requirement

These tests require Docker to run `itzg/minecraft-server`. CI runners must have Docker available.

| CI Platform | Docker Support | Notes |
|---|---|---|
| GitHub Actions (ubuntu) | ✅ Native | Preferred. Use `services:` or let Aspire manage containers. |
| GitHub Actions (windows) | ⚠️ Limited | Windows containers only, or WSL2. Not recommended. |
| Azure DevOps (hosted) | ✅ Ubuntu agents | Standard Docker available. |

### Recommended CI Configuration

```yaml
integration-tests:
  runs-on: ubuntu-latest
  timeout-minutes: 10
  needs: build  # Only run after unit tests pass
  steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.0.x'
    - name: Run integration tests
      run: |
        dotnet test tests/Aspire.Hosting.Minecraft.IntegrationTests \
          --configuration Release \
          --logger "trx" \
          --results-directory ./test-results
      timeout-minutes: 8
    - uses: actions/upload-artifact@v4
      if: always()
      with:
        name: integration-test-results
        path: ./test-results/
```

### Key CI Decisions

1. **Separate job, not separate workflow.** Integration tests run in the same `build.yml` but as a dependent job after unit tests pass. This prevents wasting Docker time on broken code.

2. **Linux only.** The Minecraft Docker container is Linux-based. Running integration tests on Windows CI would require WSL2 and adds complexity for no value.

3. **8-minute timeout per job.** Minecraft server startup (~45s) + worker build (~30s) + test execution (~60s) + buffer = ~3 minutes total. 8-minute timeout gives generous headroom.

4. **Test results as artifacts.** Upload TRX files so failures are diagnosable without re-running.

5. **No Playwright in CI initially.** BlueMap screenshot tests are deferred from CI until we have stable reference images and a screenshot comparison strategy.

## Timing and Wait Strategy

```
Timeline (approximate):
  0s    DistributedApplicationTestingBuilder starts containers
  5s    Docker pull itzg/minecraft-server (cached after first run)
  15s   Container starts, Minecraft server JVM initializes
  45s   Paper server finishes loading, RCON becomes available
  50s   Aspire health check passes, worker starts
  55s   Worker discovers resources, begins village build
  75s   Village build complete (4 resources × ~5s each)
  78s   save-all + bluemap update triggered
  80s   RCON tests begin (blocks exist immediately)
  110s  BlueMap finishes re-rendering (if screenshot tests run)
```

**Wait strategy:** The fixture polls with `execute if block` on a known coordinate (first structure corner) every 5 seconds until it succeeds or 3-minute timeout expires. This is more reliable than a fixed `Task.Delay` because build time varies with server load.

## Risks and Mitigations

| Risk | Impact | Likelihood | Mitigation |
|---|---|---|---|
| Minecraft server fails to start in CI | Tests don't run | Low | Retry logic in fixture, increase timeout. Pre-pull Docker image in CI cache. |
| RCON `execute if block` command format changes | All block assertions break | Very Low | Pin Minecraft server version in `itzg/minecraft-server` tag. Command is stable since 1.13. |
| BlueMap render timing inconsistent | Screenshot tests flaky | Medium | Defer screenshot tests until RCON tests are stable. Use HTTP health checks, not rendering completeness. |
| Port conflicts in CI | Connection failures | Low | Use Aspire auto-assigned ports (remove hardcoded `gamePort: 25565` in test AppHost). |
| Tests are too slow for PR CI | Developer friction | Medium | Run integration tests only on `main` branch and release PRs. PRs only run unit tests. |
| Worker changes coordinate math | Tests assert wrong positions | Medium | Tests import `VillageLayout` constants directly. If constants change, tests adapt automatically. |
| Docker image download times out in CI | Spurious failures | Low | Use GitHub Actions Docker layer caching. Pin image tag to avoid pulling `:latest` each time. |

## Future Extensions

1. **Playwright Visual Regression (Sprint 5+):** Navigate to BlueMap, position camera at village center, screenshot comparison with tolerance. Requires: reference image pipeline, image diff library (e.g., `Codeuctivity.ImageSharpCompare`), BlueMap render wait logic.

2. **RCON Structure Snapshot:** Dump all blocks in a region (`/fill ... replace`) and compare against a "golden" snapshot. More comprehensive than spot checks but slower.

3. **Dynamic Resource Tests:** Start with 4 resources, verify village, then add a 5th resource at runtime and verify the village expands.

4. **Failure Scenario Tests:** Kill a container (Redis), verify health indicator changes from green to red wool.

5. **Performance Benchmarks:** Measure time from worker start to village completion. Track regression across releases.
