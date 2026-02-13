# v0.5.0: Grand Village — Walk Inside Your Infrastructure

> **Author:** Jeffrey T. Fritz  
> **Date:** February 2026  
> **Milestone:** 5 — Grand Village  
> **Package:** [Fritz.Aspire.Hosting.Minecraft](https://www.nuget.org/packages/Fritz.Aspire.Hosting.Minecraft)  
> **Repo:** [csharpfritz/Aspire-Minecraft](https://github.com/csharpfritz/Aspire-Minecraft)

---

## The Village Got An Upgrade

Four releases ago, we built you a village. Themed buildings, redstone graphs, service switches, heartbeat pulses — it was the turning point. But the buildings were 7×7. Functional. Not exactly welcoming.

**v0.5.0 changes that.**

`WithGrandVillage()` swaps every 7×7 structure for a 15×15 walkable masterpiece. Step through the grand watchtower's oak doors and climb a spiral staircase three floors up. Duck into the warehouse's cargo bay, browse the barrel storage. Stand inside the workshop's loft, surrounded by crafting stations. The silo? Now a two-story polished deepslate cylinder with a central copper pillar.

And if your watchtower goes down? The minecart rail network on every side stops running. Chest minecarts halt mid-track. It's not just data anymore — it's *geography*.

---

## Grand Village: Building by Building

### Grand Watchtower (for .NET Projects)

- **Size:** 15×15 footprint, 20 blocks tall
- **Entry:** Double oak doors at ground level
- **Interior:** 3-story tower with spiral staircase (Minecraft redstone-powered spiral pattern)
- **Top Floor:** Observation deck with crenellated battlements (for looking down on your deployed code)
- **Furnishing:** Enchanting table on the middle floor, bookshelves, torches, oak trapdoors for the spiral steps
- **Health Indicator:** Glow block directly above the door (sits flush now, thanks to our `DoorPosition` refactor)

### Grand Warehouse (for Docker Containers)

- **Size:** 15×15 footprint, 8 blocks tall  
- **Entry:** Double iron doors with cargo theme
- **Interior:** Loading dock with hanging lanterns, barrels stacked as "cargo," iron blocks forming structural support
- **Signature Feature:** Deepslate brick accent walls for industrial flair
- **Rail Connection:** L-shaped powered rail path along the south and west sides, detector rail station at the dock entrance

### Grand Workshop (for Executables)

- **Size:** 15×15 footprint, peaked A-frame roof reaching 12 blocks
- **Entry:** Oak doors with a small porch
- **Interior:** Loft with ladder access, crafting table and smithing table on the ground floor, furnaces for "production"
- **Roofing:** Angled oak beams with dark oak accent (classic crafting aesthetic)
- **Chimney:** Smoke coming from the forge roof

### Grand Silo (for Databases)

- **Size:** 15×15 footprint, cylindrical structure (Minecraft approximation), 14 blocks tall
- **Material:** Polished deepslate for sleek, technical feel
- **Interior:** Two floors with a central copper pillar
- **Signature Feature:** Copper ladder spiraling around the central pillar, lanterns on each floor
- **Rail Integration:** Detector rail at the silo entrance, fully integrated into the dependency network

### Grand Azure Pavilion (for Azure Resources)

- **Size:** 15×15 footprint, light blue concrete frame, glass panels
- **Entry:** Open pavilion design with no doors — welcoming cloud aesthetic
- **Interior:** Skylight overhead with blue glass panes, azure banners on all four corners
- **Furnishing:** Minimal but elegant — benches for sitting, lanterns for atmosphere

### Grand Cottage (for Everything Else)

- **Size:** 15×15 footprint, 6 blocks tall pitched roof
- **Materials:** Cobblestone foundation, oak wood frame (rustic feel)
- **Interior:** Furnished home with bed, bookshelves, flower pots, crafting area
- **Details:** Split design — cobblestone on one half, oak on the other; warm and inviting

---

## Minecart Rails: Your Dependencies on Track

Every grand village building is now connected by a powered rail network. This isn't just decoration — it's a live visualization of your dependency architecture *in motion*.

### How It Works

```csharp
builder.AddMinecraftServer("minecraft")
    .WithGrandVillage()      // Enable 15×15 buildings
    .WithMinecartRails()     // Add powered rail connections
    .WithMonitoredResource(api)
    .WithMonitoredResource(redis)
    .WithMonitoredResource(postgres);
```

That's it. The worker automatically:

1. **Builds L-shaped rail paths** along cobblestone streets connecting each building
2. **Places powered rails** every 8 blocks with redstone torches underneath (keeps minecarts moving)
3. **Stations detector rails** at each building entrance — minecarts stop when they arrive
4. **Spawns chest minecarts** on each track, loaded with colored wool blocks matching the resource type
5. **Breaks rails when a parent resource fails** — if your API goes down, minecarts heading to dependent services stop dead in their tracks

### What You See

- **All healthy:** Minecarts circulate smoothly through the village, showing your system's dependency flow
- **One service fails:** The rail segments upstream of the failure go dark (powered rails turn off), and minecarts stop
- **Service recovers:** Rails re-power immediately, minecarts resume their journey

It's a hypnotic visual. In a demo, people will stand there watching the minecarts move. Try explaining a dependency graph in a slide deck — minecarts get it instantly.

---

## The Architecture Behind the Scenes: DoorPosition

Here's a subtle refactor that had outsized impact: **all building elements now derive from a single `DoorPosition` record.**

Before v0.5.0, each building had separate placement logic for:
- The glow block health indicator (floating above the door)
- The service switch (lever to the side of the door)
- The sign (hanging above the door)

Result? Floating switches. Health indicators in weird spots. Elements getting out of sync when building dimensions changed.

In v0.5.0, everything is defined relative to the door:

```csharp
public record DoorPosition(BlockPos DoorBlock, BlockFace DoorFace, int DoorWidth, int DoorHeight)
{
    public BlockPos GetHealthIndicatorPos() => DoorBlock + (0, DoorHeight + 1, 0);
    public BlockPos GetServiceSwitchPos() => DoorBlock + DoorFace.ToOffset() * 2;
    public BlockPos GetSignPos() => DoorBlock + (0, DoorHeight + 2, 0);
}
```

Now when we introduce a new building (or update an existing one), we define the door once and everything else snaps into place. The health indicators sit directly above. The switches align to the door face. The signs hover at consistent heights.

**Service adaptation is automatic.** Every existing service — switches, guardians, redstone, holograms — already works with grand villages because they all query `DoorPosition` instead of hardcoding coordinates.

---

## Bug Fixes in v0.5.0

### Watchtower Floating Switches
**Before:** Service switches on tall watchtower buildings (3-block doors) were hovering at inconsistent heights.  
**After:** All switches now derive from `DoorPosition`. Consistent placement across all building types.

### Glow Block Health Indicators
**Before:** Health indicators were appearing in random spots, sometimes inside the door frame, sometimes mid-wall.  
**After:** Health indicators sit exactly one block above the door (adjusted for door height), visible and clean on all buildings.

### Silo Entrance Accessibility
**Before:** The silo entrance was placed at a wonky height, making it hard to enter.  
**After:** Entrance is ground-level, flush with the surrounding terrain. Copper ladder spirals from inside — proper architectural flow.

### Service Adaptation Edge Cases
**Before:** Some services didn't fully adapt to non-standard building dimensions.  
**After:** All services now use `DoorPosition` as the source of truth. Dimension changes cascade automatically.

---

## Code Example: Small vs. Grand Village Toggle

Here's the pattern from the demo AppHost:

```csharp
using Aspire.Hosting.Minecraft;

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("cache");
var postgres = builder.AddPostgres("db-host");
var db = postgres.AddDatabase("db");
var api = builder.AddProject<Projects.MyApi>("api")
    .WithReference(redis);
var web = builder.AddProject<Projects.MyWeb>("web")
    .WithReference(api)
    .WithExternalHttpEndpoints();

var minecraft = builder.AddMinecraftServer("minecraft", gamePort: 25565, rconPort: 25575)
    .WithMaxPlayers(10)
    .WithMotd("Aspire Fleet Monitor")
    .WithPersistentWorld()
    .WithPeacefulMode()
    .WithBlueMap(port: 8100)
    .WithOpenTelemetry()
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    
    // Village configuration
    .WithBossBar("Aspire Status")
    .WithWeatherEffects()
    .WithWorldBorderPulse()
    .WithParticleEffects()
    .WithGuardianMobs()
    .WithBeaconTowers()
    .WithServiceSwitches()
    .WithRedstoneDependencyGraph()
    .WithHeartbeat()
    .WithSoundEffects()
    .WithFireworks()
    .WithDeploymentFanfare()
    .WithAchievements()
    .WithTitleAlerts()
    .WithActionBarTicker()
    
    // v0.5.0: Grand village with minecart rails
    .WithGrandVillage()     // Toggle this to enable 15×15 buildings
    .WithMinecartRails()    // Add powered rail network
    
    // Monitored resources
    .WithMonitoredResource(api)
    .WithMonitoredResource(web)
    .WithMonitoredResource(redis)
    .WithMonitoredResource(postgres);

builder.Build().Run();
```

Remove `.WithGrandVillage()` and `.WithMinecartRails()` and you get the small village. Add them back and watch the world expand. Both modes are fully supported.

---

## Performance & Compatibility

**Grand villages are more intensive than small villages.** More blocks, more rail network, more minecarts to move. But:

- **Chunk loading is optimized** — all grand village chunks are force-loaded once at startup
- **RCON command batching** — rail placement uses bulk `fill` commands instead of individual `setblock`
- **Minecart physics are stable** — we use powered rails (not ever-accelerating carts), and they pause cleanly when rails disable
- **All existing services adapt** — your monitor count doesn't change, performance impact is minimal

**Backwards compatibility:** Existing code calling `.WithMonitoredResource()` works unchanged. Grand villages are opt-in.

---

## What's Next

We're eyeing the Azure integration next — a separate `Fritz.Aspire.Hosting.Minecraft.Azure` package that renders Azure resources as a distant citadel visible from the village. Think "The Pan" from the village to the cloud.

We're also prepping for the conference circuit. If you're presenting .NET Aspire at a conference and want your audience to *lean forward*, a live Minecraft world showing a failed deployment, a cascading redstone graph, minecart rails grinding to a halt, and then a recovery fanfare happening in real time is how you do it.

---

## Install It

```shell
dotnet add package Fritz.Aspire.Hosting.Minecraft --version 0.5.0
```

- 📦 **NuGet:** [Fritz.Aspire.Hosting.Minecraft](https://www.nuget.org/packages/Fritz.Aspire.Hosting.Minecraft)
- 🐙 **GitHub:** [csharpfritz/Aspire-Minecraft](https://github.com/csharpfritz/Aspire-Minecraft)
- 📖 **Architecture Deep-Dive:** [Behind the Build](behind-the-build.md)
- 📚 **User Guide:** [Grand Village Features](../user-docs/grand-village-features.md)

---

> _Walk through your infrastructure. Watch it react to your deployments. That's what Minecraft was made for._
