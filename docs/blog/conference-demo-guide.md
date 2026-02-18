# Conference Demo Guide: Fritz.Aspire.Hosting.Minecraft

> **Target:** Conference talks, meetups, and live-coding sessions  
> **Duration:** 10–15 minutes  
> **Audience:** .NET developers, Aspire users, conference attendees  
> **Package:** [Fritz.Aspire.Hosting.Minecraft](https://www.nuget.org/packages/Fritz.Aspire.Hosting.Minecraft)

---

## Pre-Show Setup

### Requirements

| Item | Notes |
|---|---|
| .NET 10.0 SDK | `dotnet --version` to verify |
| Docker Desktop | Running and healthy. Pre-pull `itzg/minecraft-server:latest` to avoid demo-day download delays |
| Minecraft Java Edition | Any recent version. Set to fullscreen or windowed at a resolution visible to the audience |
| Two monitors (or picture-in-picture) | One for code/Aspire dashboard, one for Minecraft |

### Pre-Pull the Docker Image

Do this **the night before**. The image is ~800 MB:

```bash
docker pull itzg/minecraft-server:latest
```

### Clone and Test-Run

```bash
git clone https://github.com/csharpfritz/Aspire-Minecraft.git
cd Aspire-Minecraft/samples/GrandVillageDemo/GrandVillageDemo.AppHost
dotnet run
```

Wait for the Minecraft server to finish loading (watch Docker logs for "Done" or check the Aspire dashboard for the health check going green). Connect with Minecraft and verify you can see structures near spawn.

**Kill the demo** (`Ctrl+C`) and re-run it fresh for the actual presentation. Each run starts with a brand-new world — no leftover state.

### Pre-Configure Minecraft Client

1. Add server `localhost:25565` to your server list
2. Set render distance to 12+ (so beacons are visible)
3. Set music volume to 0, master volume to 50% (you want sound effects audible through speakers but not overwhelming)
4. Turn on subtitles if presenting without sound

---

## Demo Script

### Act 1: "What If Your Distributed System Was a Minecraft World?" (3 min)

**Setup:** Have the AppHost `Program.cs` open in your editor.

**Talking Points:**

> "What if you could *feel* your distributed system? Not look at a dashboard — actually feel it. Today I'm going to show you what happens when you connect .NET Aspire to Minecraft."

Show the code. Walk through the key lines:

```csharp
// "This is a standard Aspire app — API, web frontend, Redis, Postgres."
var redis = builder.AddRedis("cache");
var pg = builder.AddPostgres("db-host");
var api = builder.AddProject<Projects.GrandVillageDemo_ApiService>("api");
var web = builder.AddProject<Projects.GrandVillageDemo_Web>("web");

// "And then we add a Minecraft server. One line."
var minecraft = builder.AddMinecraftServer("minecraft", gamePort: 25565, rconPort: 25575)
    .WithBlueMap(port: 8100)
    .WithOpenTelemetry()
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>();
```

> "That's it. Minecraft is now an Aspire resource — it shows up in the dashboard, it has health checks, it has OpenTelemetry metrics."

**Action:** Run `dotnet run`. While waiting for boot, continue talking.

> "The `.WithAspireWorldDisplay()` call creates an internal worker service that connects to the Minecraft server via RCON — the Remote Console protocol. This worker polls your other services and renders their state in the Minecraft world."

### Act 2: "The Village" (3 min)

**Setup:** Connect to the Minecraft server. Fly to the village near spawn (~coordinates 10, -60, 0).

**What to Show:**

1. **Village structures** — Point out the different building types:
   > "Each Aspire resource gets its own building. Projects like our API are Watchtowers — tall stone brick towers with blue banners. Docker containers like Redis are Warehouses — wide iron buildings with purple glass. The building style tells you the resource type at a glance."

2. **Signs** — Walk up to a structure and read the sign:
   > "Each building has a sign showing the resource name and its current health status."

3. **Health indicators** — Point out the glowstone in the walls:
   > "Glowstone means healthy. If a service goes down, this switches to an unlit redstone lamp."

4. **Cobblestone paths** — Show the boulevard:
   > "The village is laid out in a grid with cobblestone paths connecting everything. Resources that depend on each other are placed next to each other."

5. **Beacon towers** — Fly up to see the beacons:
   > "Each resource also gets a beacon tower. The beam color matches the Aspire dashboard — blue for .NET projects, purple for containers. If a service goes unhealthy, the beam turns red."

6. **Fence and gate** — Show the perimeter:
   > "The whole village is enclosed in an oak fence with a gate. It's a proper settlement."

### Act 3: "The Features" (3 min)

**Show each feature in sequence. These are all visible without breaking anything yet:**

1. **Boss bar** — Point at the top of the screen:
   > "See that bar at the top? That's the fleet health boss bar. Right now it says 100%, green. It tracks the percentage of healthy resources in real time."

2. **Action bar ticker** — Watch the HUD above the hotbar:
   > "The text cycling above my hotbar shows live metrics — TPS, milliseconds per tick, healthy resource count, and RCON latency. This rotates every few seconds."

3. **Heartbeat** — Listen:
   > "Hear that steady bass pulse? That's the heartbeat. It's playing a note block sound at 1-second intervals because all services are healthy. The tempo changes with fleet health."

4. **BlueMap** — Switch to the Aspire dashboard, click the BlueMap endpoint:
   > "And here's the 3D web map. You can see the entire village from a bird's-eye view, right in the Aspire dashboard."

### Act 4: "Break Something" (3 min) ⭐ THE CLIMAX

**This is the moment the audience remembers. Practice this transition.**

> "Now let's see what happens when things go wrong."

**Action:** In the Aspire dashboard, stop the `api` resource (or `docker stop` the Redis container).

**What happens simultaneously (point these out to the audience):**

1. 🌧️ **Weather changes** — Sky darkens, rain starts falling
   > "The weather just changed. Clear skies meant everything was healthy. Rain means we're degraded."

2. 📊 **Boss bar drops** — Bar turns yellow, percentage decreases
   > "The boss bar dropped to 75% and turned yellow."

3. 📢 **Title alert** — Giant red "⚠ SERVICE DOWN" fills the screen
   > "Full-screen alert — SERVICE DOWN, and it tells us which service: api."

4. 🔊 **Sound effect** — Wither ambient sound plays
   > "That ominous sound? That's the wither ambient noise. It plays whenever a service goes down."

5. 💨 **Particles** — Smoke and flame at the API's structure
   > "Look at the API's watchtower — smoke and flames."

6. 🔴 **Beacon turns red** — The API's beacon beam changes color
   > "The beacon beam went from blue to red."

7. 💓 **Heartbeat slows** — The pulse gets slower and lower-pitched
   > "Listen — the heartbeat slowed down. It's running at half speed now."

8. 🌐 **World border** (if > 50% unhealthy) — Border starts shrinking with red tint
   > "If enough services go down, the world border actually starts shrinking toward you."

**Pause for effect.** Let the audience absorb the atmosphere.

### Act 5: "Recovery" (2 min)

**Action:** Restart the stopped service.

**What happens:**

1. ☀️ **Weather clears** — Sun comes back
2. ✅ **Title alert** — Green "✅ BACK ONLINE" with the service name
3. 🎵 **Level-up sound** — The recovery chime plays
4. 🎆 **Fireworks** — If all services are back to healthy, fireworks launch
5. ✨ **Happy particles** — Green sparkles at the recovered resource
6. 🏆 **Achievement** — "Survived a Crash" achievement pops up
7. 📊 **Boss bar returns to green** — 100% again
8. 💓 **Heartbeat speeds back up** — Steady fast pulse resumes

> "Everything recovers. The weather clears, fireworks celebrate the full recovery, and we even get an achievement — 'Survived a Crash.' Your distributed system didn't just recover. Your Minecraft world celebrated."

### Act 6: "How It Works" (1 min, optional)

If you have time, briefly explain:

> "Under the hood, a .NET BackgroundService connects via RCON and polls your resources every 10 seconds. Each feature — weather, boss bar, particles — is opt-in via a `.With*()` method that sets an environment variable. The worker checks those variables at startup and only registers the services you asked for. Zero overhead for disabled features."

Show the `Program.cs` one more time with the full feature list.

---

## Troubleshooting

### "The Minecraft server won't start"

- Check Docker Desktop is running
- Ensure ports 25565 and 25575 aren't in use: `netstat -an | findstr 25565`
- Check the Docker container logs in the Aspire dashboard

### "I can't connect to the server"

- The server takes 60–90 seconds to fully start. Wait for the health check to go green in the dashboard
- Ensure you're using `localhost:25565`
- Set `ONLINE_MODE` to `FALSE` if you don't have a Mojang account

### "Structures aren't appearing"

- Fly to coordinates ~10, -60, 0 (the village spawn point)
- The worker needs all resources to be discovered before building. Check the worker's logs for "Discovered Aspire resource" messages

### "Sound effects aren't working"

- Check Minecraft audio settings — master volume and "Blocks" category must be non-zero
- Verify `.WithSoundEffects()` is in your code

### "The demo is slow / laggy"

- Pre-pull the Docker image before the demo
- Close other Docker containers
- The first run downloads plugins (BlueMap, DecentHolograms) — do a test run beforehand

### "Weather isn't changing when I stop a service"

- Weather only changes on state transitions. It polls every 10 seconds, so there's a short delay
- Check the worker logs for "Weather changed" entries

---

## Talking Points Cheat Sheet

| Feature | One-Liner | RCON Command |
|---|---|---|
| Boss Bar | "Persistent health percentage at the top of the screen" | `bossbar set aspire:fleet_health value 75` |
| Weather | "Clear = healthy, rain = degraded, thunder = critical" | `weather rain` |
| Beacon Towers | "Colored beams per resource — type colors match the Aspire dashboard" | `setblock ... minecraft:beacon` |
| Title Alerts | "Full-screen notification on service failure and recovery" | `title @a title {"text":"⚠ SERVICE DOWN"}` |
| Sound Effects | "Wither growl on failure, level-up chime on recovery" | `playsound minecraft:entity.wither.ambient` |
| Particles | "Smoke on crash, green sparkles on recovery" | `particle minecraft:large_smoke` |
| Heartbeat | "Audible pulse whose tempo reflects fleet health" | `playsound minecraft:block.note_block.bass` |
| Fireworks | "Celebratory fireworks when all services recover" | `summon minecraft:firework_rocket` |
| Achievements | "Infrastructure milestones as in-game achievements" | `title @a title {"text":"🏆 Achievement!"}` |
| Action Bar | "Rotating HUD: TPS, MSPT, healthy count, latency" | `title @a actionbar "TPS: 20.0/20.0"` |
| World Border | "Border shrinks under critical failure" | `worldborder set 100 10` |
| Deployment Fanfare | "Lightning + fireworks when a service finishes starting" | `summon minecraft:lightning_bolt` |
| Village Structures | "Themed buildings per resource type" | `fill ... minecraft:stone_bricks hollow` |

---

## Slide Suggestions

If you're building a slide deck around this demo:

1. **Title slide:** "What If Your Infrastructure Was a Minecraft World?"
2. **Problem:** "Dashboards are passive. What if monitoring was visceral?"
3. **The code:** Show the 10-line `Program.cs` with `AddMinecraftServer`
4. **Live demo:** Switch to the live demo (Acts 1–5)
5. **Architecture:** The 3-layer diagram (AppHost → Minecraft Server → Worker)
6. **Features grid:** 2×6 grid of feature icons with one-liners
7. **How to try it:** `dotnet add package Fritz.Aspire.Hosting.Minecraft`
8. **QR code:** Link to the GitHub repo

---

*The demo project is in `samples/GrandVillageDemo/`. Every feature shown here is already wired up — just `dotnet run` and go.*
