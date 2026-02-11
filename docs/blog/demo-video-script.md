# Demo Video Script: Fritz.Aspire.Hosting.Minecraft

> **Target Duration:** 3–5 minutes  
> **Format:** Narrator script with visual cues  
> **Platform:** YouTube, social media, conference talks

---

## Opening (0:00–0:30)

**[VISUAL: Minecraft world, sweeping camera pan across a village with beacons shooting colored beams into the sky. Boss bar at top showing "100%", clear weather]**

**Narrator:**
What if you could *feel* your distributed system? Not look at a dashboard — actually feel it.

**[VISUAL: Weather suddenly shifts to thunderstorm, boss bar drops to red, title alert "⚠ SERVICE DOWN" fills screen]**

This is Fritz.Aspire.Hosting.Minecraft — a NuGet package that turns your .NET Aspire infrastructure into a living Minecraft world.

---

## Setup (0:30–1:15)

**[VISUAL: Switch to VS Code showing AppHost Program.cs]**

**Narrator:**
Adding Minecraft monitoring to your Aspire app takes five lines of code.

**[SHOW: Highlight this code block]**
```csharp
var builder = DistributedApplication.CreateBuilder(args);
var api = builder.AddProject<Projects.MyApi>("api");

builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithBossBar()
    .WithWeatherEffects()
    .WithMonitoredResource(api);
```

**[VISUAL: Terminal running `dotnet run`, Aspire dashboard opening with Minecraft server resource highlighted]**

That's it. The Minecraft server appears in your Aspire dashboard. Under the hood, a worker service connects via RCON and monitors your resources every 10 seconds.

---

## Village Tour (1:15–2:45)

**[VISUAL: Minecraft client, player flying into the resource village]**

**Narrator:**
Connect to the server and fly to the village. Each Aspire resource gets its own building.

**[SHOW: Close-up of a stone brick watchtower with blue banner]**

.NET projects become watchtowers — tall stone towers with colored banners.

**[SHOW: Iron block warehouse with purple glass]**

Docker containers are warehouses — wide industrial buildings with purple glass.

**[SHOW: Oak wood workshop with chimney]**

Executables get workshops — small buildings with chimneys.

**[SHOW: Sign on building reading "api (Healthy)"]**

Every building has a sign showing the resource name and current health.

**[SHOW: Cobblestone paths between structures, oak fence perimeter with gate]**

The village is laid out in a grid with cobblestone pathways and an oak fence perimeter. Resources that depend on each other are placed next to each other.

**[SHOW: Fly up to reveal beacon towers above each structure, colored beams shooting into sky]**

Each resource also gets a beacon tower. The beam color matches your Aspire dashboard — blue for projects, purple for containers, cyan for executables.

**[SHOW: Service switch — lever on building front]**

These levers show the service state. Up means healthy. Down means unhealthy.

**[SHOW: Redstone wires connecting buildings]**

And look — redstone wires connecting dependent services. A visual dependency graph built in blocks.

---

## Health Transitions — The Climax (2:45–3:45)

**[VISUAL: Split screen — Aspire dashboard on left, Minecraft world on right]**

**Narrator:**
Now let's break something.

**[SHOW: Click "Stop" button on the API resource in the dashboard]**

**[VISUAL: Multiple simultaneous effects in Minecraft]**

Watch what happens.

**[SHOW: Weather changes from clear to rain]**

The weather darkens. Rain starts falling.

**[SHOW: Boss bar drops from 100% green to 75% yellow]**

The boss bar drops to 75 percent and turns yellow.

**[SHOW: Full-screen title alert "⚠ SERVICE DOWN — api" in red]**

Full-screen alert: Service Down.

**[SHOW: Beacon beam turns from blue to red]**

The API's beacon turns red.

**[SHOW: Particles — smoke and flame at the watchtower]**

Smoke and flames at the structure.

**[SHOW: Lever on building flips down, redstone wire turns off]**

The service switch flips. The redstone goes dark.

**[VISUAL: Pause on the atmospheric shot — rain, red beacon, smoke]**

**Narrator:**
You don't just *see* the failure. You feel it.

---

## Cool Features (3:45–4:30)

**[VISUAL: Achievement popup "First Blood"]**

**Narrator:**
You even get achievements for infrastructure milestones.

**[SHOW: Action bar ticker above hotbar cycling through metrics]**

The action bar cycles through live metrics — TPS, milliseconds per tick, healthy service count.

**[SHOW: Heartbeat audio waveform visualization, slow bass pulse]**

Hear that slow pulse? That's the heartbeat — a note block sound whose tempo reflects fleet health. Right now it's slow because we're degraded.

**[SHOW: World border shrinking with red tint]**

When more than half your services go down, the world border starts shrinking with a red tint. It's a visual pressure that builds as things get worse.

**[VISUAL: Restart the API service in the Aspire dashboard]**

Now let's bring it back.

**[SHOW: Weather clears to blue sky]**

Weather clears.

**[SHOW: Green title alert "✅ BACK ONLINE — api"]**

Back online.

**[SHOW: Fireworks launching, level-up chime sound]**

Fireworks celebrate the recovery.

**[SHOW: Happy villager particles at the structure, beacon turns back to blue]**

Happy particles. Beacon back to blue.

**[SHOW: Boss bar returns to 100% green]**

Boss bar back to green.

**[SHOW: Achievement popup "Clean Sweep"]**

And an achievement: Clean Sweep — all services healthy.

---

## Closing (4:30–5:00)

**[VISUAL: GitHub repo page showing README with NuGet badge]**

**Narrator:**
Fritz.Aspire.Hosting.Minecraft is available on NuGet today. It's MIT-licensed, fully open source, and every feature is opt-in via a fluent API.

**[SHOW: Code snippet of full feature demo from README]**

Boss bar, weather, beacons, particles, heartbeat, achievements — you choose what you want.

**[VISUAL: Teaser — concept art or mockup of Azure citadel next to the village]**

Coming soon: Azure Resource Group integration. Your Azure infrastructure visualized alongside your local services.

**[SHOW: GitHub star button, NuGet download count]**

Try it now. Star the repo. Build something cool.

**[VISUAL: Final shot — player standing at the village gate, beacons glowing in the distance, clear sky]**

**Narrator:**
Your distributed system just got a lot more interesting.

**[END CARD: Fritz.Aspire.Hosting.Minecraft / github.com/csharpfritz/Aspire-Minecraft / NuGet logo]**
