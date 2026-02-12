# Sprint 4 Brainstorm: Aspire Observability Visualization Ideas

> **By:** Rhodey (Lead)  
> **Date:** 2026-02-12  
> **Requested by:** Jeffrey T. Fritz  
> **Status:** 💡 Brainstorm — ideas for team discussion and prioritization

---

## Context

Jeff asked: *"What would be fun to be able to browse through and wander around in Minecraft? Some way to visualize traces?"*

The project already visualizes resource health, dependencies, and status events. These ideas push into **observability data** — traces, metrics, logs, and request flows that Aspire collects via OpenTelemetry.

---

## Idea 1: Trace River

**Name:** Trace River  
**Extension method:** `WithTraceRiver()`  
**What it visualizes:** OpenTelemetry distributed traces — request flows across services  
**How it looks in Minecraft:**

Water channels flow between resource buildings, representing HTTP request paths. Each trace becomes a **boat** (or armor stand riding a boat) that spawns at the originating service's building and floats downstream through connecting channels to each service the request touched. The boat's color/name tag shows the trace ID. **Slow traces** (high latency) cause the water to turn to **honey blocks** (slow movement). **Error traces** turn the channel to **lava** briefly, with smoke particles. Each channel has **soul lanterns** along its banks showing the span count.

The channels are dug 2 blocks below surface level between buildings, with glass floors so you can watch boats from above. At each service building, there's a small "dock" where boats arrive and depart.

**Fun factor:** You literally *watch your requests flow* between services. Seeing a boat hit lava when a 500 error occurs is visceral. Walking along the river and following a single request through your system is the kind of thing you'd show at a conference and people would lose their minds.

**Technical feasibility:** **Medium.** Requires consuming OTLP trace data in the worker (new data source — currently only health polling). Water channel construction is straightforward RCON `/fill`. Boat spawning via `/summon`. The hard part is subscribing to trace data from the Aspire dashboard's OTLP collector — may need to run a secondary OTLP receiver in the worker or poll the dashboard API. Rate limiting boat spawns is critical (busy systems could spawn hundreds per second).

---

## Idea 2: The Enchanting Tower (Metrics Observatory)

**Name:** Enchanting Tower  
**Extension method:** `WithMetricsTower()`  
**What it visualizes:** Key metrics per resource — CPU%, memory, request rate, error rate  
**How it looks in Minecraft:**

A tall central tower (15-20 blocks high) at the village center, built from **enchanting tables, bookshelves, and amethyst**. Each monitored resource gets a floor/level in the tower with 4 indicator columns:

- **CPU:** A column of **magma blocks** (0-100% height). High CPU = tall glowing magma column.  
- **Memory:** A column of **blue ice** that grows upward as memory usage increases. Melts (becomes water) if memory drops.  
- **Request Rate:** **Note blocks** on a redstone clock — the tempo increases with request rate. You literally *hear* how busy a service is.  
- **Error Rate:** **Crying obsidian** column height — errors make the tower weep.

A spiral staircase lets players walk up and visually compare metrics across services. Each floor has a hologram sign with the service name and current values.

**Fun factor:** Standing at the top of the tower and looking down at which floors are glowing hot (CPU) vs weeping (errors) is an incredible overview. The note block tempo creates an ambient soundscape where you can *hear* your system's load.

**Technical feasibility:** **Medium-Hard.** Requires consuming OTLP metrics (gauges, counters). Column height updates via `/fill` are easy. Note block tempo requires careful redstone timing or repeated `/playsound` calls. The main risk is RCON throughput — 4 columns × N resources × update frequency could exceed the 10 cmd/sec budget. Needs batching or selective updates (only update changed values).

---

## Idea 3: Log Campfires

**Name:** Log Campfires  
**Extension method:** `WithLogCampfires()`  
**What it visualizes:** Application logs — especially errors and warnings  
**How it looks in Minecraft:**

Each resource building gets a **campfire** outside its front door. The campfire represents the service's log stream:

- **Normal logs (Info):** Regular campfire with gentle smoke particles — the service is humming along.  
- **Warnings:** Campfire becomes a **soul campfire** (blue flames) — something's off.  
- **Errors:** The campfire is replaced with a **fire block** (spreading flames) and **TNT** particles appear. If errors exceed a threshold, an actual TNT block spawns (doesn't detonate — just the visual threat).  
- **Log volume:** Smoke particle intensity matches log volume. A chatty service has a roaring campfire; a quiet service has gentle wisps.

Behind each building, a **wall of signs** (2-wide, 4-tall) shows the last 8 log lines, scrolling like a terminal. Signs update every poll cycle with the most recent entries, with error lines in red text.

**Fun factor:** Walking through the village and seeing which buildings are on fire vs. gently smoking is an instant error heatmap. The sign wall is the closest thing to `tail -f` in Minecraft. The TNT visual for error storms is *chef's kiss*.

**Technical feasibility:** **Medium.** Campfire block swaps are simple `/setblock` commands. Sign text via `/data merge` is well-supported in Paper. The challenge is log ingestion — need OTLP log receiver or dashboard API access. Rate-limiting sign updates is important (don't hammer RCON with sign changes on every log line). Batch to every 5-10 seconds.

---

## Idea 4: Nether Portal Request Gateway

**Name:** Nether Portal Gateway  
**Extension method:** `WithRequestGateway()`  
**What it visualizes:** HTTP request/response flows — counts, latencies, status codes  
**How it looks in Minecraft:**

Each service building gets a **Nether Portal frame** as its front entrance. The portal represents the service's HTTP endpoint:

- **Active portal (purple swirl):** Service is receiving requests normally.  
- **Portal deactivated (empty obsidian frame):** No traffic / service down.  
- **Frame material changes with status codes:**  
  - 2xx: Standard obsidian frame → purple portal active  
  - 4xx: Frame turns to **blackstone** with occasional enderman particles (client errors)  
  - 5xx: Frame turns to **crying obsidian** with dripping particles (server errors)  

Above each portal, a **hologram** shows: `GET /api/users → 200 (45ms)` for the last request.

Between connected services, **End Gateway blocks** (the starry portal block) create visual "wormholes" showing where requests travel. The space between portals pulses with **end rod** particles tracing the request path.

**Fun factor:** Walking through a Nether Portal to "enter" a service is the most Minecraft thing possible. Seeing your gateway frame crack and weep when 500s hit is dramatic. The wormhole effect between services is sci-fi gorgeous.

**Technical feasibility:** **Medium.** Portal construction is RCON `/fill` with obsidian + `/setblock` fire to activate. Material swaps for error states are simple block replacements. Hologram updates use existing DecentHolograms infrastructure. The hard part is getting HTTP metric data (request counts, status codes, latencies) — needs OTLP metric consumption. End Gateway blocks are creative-mode-only items, perfect for our flat world.

---

## Idea 5: Sculk Sensor Error Detection Network

**Name:** Sculk Sensor Network  
**Extension method:** `WithSculkErrorNetwork()`  
**What it visualizes:** Error propagation and cascading failures across services  
**How it looks in Minecraft:**

**Sculk veins and sensors** spread between resource buildings underground (at Y=-61, one block below surface). This creates a Warden-themed detection network:

- When a service throws errors, **sculk catalyst blocks** appear around its building, and **sculk veins** spread along the ground toward dependent services.  
- **Sculk sensors** placed along dependency paths activate (vibration particles) when errors propagate — you can see the error cascade ripple through the network.  
- If errors cascade to 3+ services, **sculk shriekers** activate with their distinctive warning sound and **Darkness** effect applied to players briefly. The *Warden is coming* = your system is about to have a very bad time.  
- Recovery clears the sculk, replaced with **moss blocks** (nature healing).

**Fun factor:** The Deep Dark is Minecraft's scariest biome. Using it to represent cascading failures is thematically perfect — errors spreading like sculk infection through your infrastructure is genuinely unsettling in the best way. The Darkness effect when things go really wrong is *immersive panic*. Recovery moss growing over the sculk is satisfying.

**Technical feasibility:** **Easy-Medium.** Sculk blocks are just block placements via `/setblock`. Sculk sensor activation is harder — they detect vibrations naturally, but we'd need to trigger them artificially (place/break blocks near them, or use `/playsound`). The Darkness effect is `/effect give @a minecraft:darkness 3`. Sculk spread animation would need timed block placement sequences. Main limitation: sculk blocks require 1.19+ (Paper supports this).

---

## Idea 6: Minecart Metric Rails

**Name:** Minecart Metric Rails  
**Extension method:** `WithMetricRails()`  
**What it visualizes:** Time-series metrics — throughput, latency percentiles, queue depths  
**How it looks in Minecraft:**

A **rail network** runs along the village perimeter with **minecarts carrying named items** that represent metric data points. Think of it as a physical strip chart recorder:

- Each metric gets a dedicated rail loop.  
- **Chest minecarts** travel the loop, carrying items that represent values: more items = higher value. The item type indicates the metric (gold ingots = requests/sec, redstone = latency ms, rotten flesh = errors).  
- Rail speed is controlled by **powered rails** — more powered rails = the metric is trending up fast.  
- At each service's building, a **hopper** collects minecarts, showing which services contribute to each metric.  
- A central **sorting station** with labeled item frames shows current values.

Players can ride the minecart to "follow" a metric's journey through the system.

**Fun factor:** A tiny factory-style conveyor belt system carrying your metrics around the village is delightful. The visual of chest carts piling up at a slow service (request queue growing) tells a story without numbers.

**Technical feasibility:** **Hard.** Minecart physics are client-side and can't be reliably controlled via RCON. Spawning them is easy (`/summon`), but managing their lifecycle (despawning old ones, preventing pileups) is complex. Rail construction is straightforward `/setblock` but needs careful powered-rail spacing. Hopper mechanics are server-side but hard to orchestrate via RCON. This idea is visually amazing but technically the hardest on the list.

---

## Idea 7: Villager Trading Hall (Dependency Marketplace)

**Name:** Villager Trading Hall  
**Extension method:** `WithDependencyTraders()`  
**What it visualizes:** Service-to-service API call patterns and data exchange rates  
**How it looks in Minecraft:**

A covered marketplace structure at the village center with **Villager NPCs** representing each service. Each villager has a profession matching its service type:

- **Projects:** Librarian (books = code)  
- **Containers:** Toolsmith (tools = infrastructure)  
- **Databases:** Cartographer (maps = data)  
- **Caches:** Cleric (potions = speed boosts)  

Villagers *walk between each other's stalls* to represent API calls. High-traffic pairs have villagers running back and forth faster. When a service goes down, its villager turns into a **Zombie Villager** (with the cure animation playing when it recovers).

Above the hall, **item frames** on the wall show what data is being exchanged — named items representing API endpoints.

**Fun factor:** Watching villagers hustle between stalls when traffic is high, then seeing one turn into a zombie when Redis goes down, is storytelling through game mechanics. The cure animation on recovery is a natural "healing" metaphor.

**Technical feasibility:** **Medium.** Villager spawning with professions is supported (`/summon villager ~ ~ ~ {VillagerData:{profession:"librarian"}}`). Movement between positions needs repeated `/tp` commands (villagers don't pathfind to coordinates on command). Zombie conversion via `/data merge` or kill+respawn. The RCON budget for moving villagers frequently could be tight — limit to 1 update per cycle, not real-time movement.

---

## Idea 8: Redstone Clock Dashboard

**Name:** Redstone Clock Dashboard  
**Extension method:** `WithRedstoneDashboard()`  
**What it visualizes:** Real-time system metrics as a large mechanical display  
**How it looks in Minecraft:**

A giant wall-mounted display (20×10 blocks) made of **redstone lamps** arranged in a grid — essentially a low-resolution LED scoreboard. Each row represents a resource, each column represents a time bucket (last 10 intervals):

- **Lamp ON (bright):** Healthy during that interval  
- **Lamp OFF (dark):** Unhealthy during that interval  
- **Redstone torch behind lamp:** Currently degraded (flickering effect via rapid on/off)

The display uses **comparators** and **repeaters** to create a shift-register effect — new data enters from the right, old data scrolls left. This is a physical, working Minecraft circuit (not just placed blocks).

Below the display, **concrete blocks** in different colors create a bar chart of response times per service (height = latency).

**Fun factor:** A working redstone scoreboard that updates in real-time is peak Minecraft engineering porn. The shift-register scroll effect makes it feel alive. Conference audiences who understand redstone will absolutely geek out. The concrete bar chart below gives a "mission control" feel.

**Technical feasibility:** **Easy-Medium.** Redstone lamp state via `/setblock` with or without redstone power. The "shift register" effect can be simulated by shifting block states left and placing new state at the right edge — no actual redstone circuit needed (RCON does the work). Concrete bar charts are simple `/fill` commands. This is one of the most RCON-friendly ideas on the list.

---

## Idea 9: Ender Chest Trace Explorer

**Name:** Ender Chest Trace Explorer  
**Extension method:** `WithTraceExplorer()`  
**What it visualizes:** Trace detail — span trees, timing breakdowns, attributes  
**How it looks in Minecraft:**

Each resource building contains an **Ender Chest** that, when the worker detects interaction (via server log monitoring), spawns a **trace exhibit** in a dedicated underground gallery (Y=-64 to -61):

- Each trace becomes a **hallway** branching off the main gallery. Length = total trace duration (1 block per 100ms).  
- **Span segments** are built as colored glass tunnel sections: green glass = fast spans, yellow = slow, red = error spans.  
- **Item frames** on walls display span names, durations, and attributes.  
- **Armor stands** at span boundaries hold named items showing the span operation name.  
- A **soul torch trail** guides players through the critical path (longest span chain).

Walking through a trace hallway literally lets you *walk the timeline* of a request — the distance you travel corresponds to the time the request took.

**Fun factor:** An underground museum of your traces where you physically walk through time is incredible for learning distributed tracing. Seeing a 2-second span as a 20-block-long red glass tunnel makes latency tangible. The critical path soul torch trail teaches you what to optimize.

**Technical feasibility:** **Hard.** Requires trace ingestion (same challenge as Trace River). The hallway construction is many RCON commands per trace (glass segments, item frames, armor stands). Space management is complex — need to clear old traces, manage gallery growth. Ender Chest interaction detection requires server log parsing. Best as a triggered/on-demand feature, not continuous.

---

## Idea 10: Dragon Health Egg

**Name:** Dragon Health Egg  
**Extension method:** `WithDragonEgg()`  
**What it visualizes:** Overall system uptime and SLO compliance  
**How it looks in Minecraft:**

A **Dragon Egg** sits atop a custom obsidian pedestal at the village center — the most precious block in Minecraft representing your system's overall health and uptime. Around the pedestal:

- **End Crystals** (one per monitored resource) float on obsidian pillars in a circle around the egg, beaming light upward when their resource is healthy. This mirrors the End dimension's crystal-and-dragon mechanic.  
- When a resource goes down, its End Crystal "explodes" (particle effect + sound, not actual explosion) and the beam disappears.  
- The Dragon Egg itself emits **portal particles** when uptime SLO is met (e.g., >99.9%). If SLO drops below threshold, the egg teleports to a random nearby position (the actual Dragon Egg mechanic — it runs away from you when clicked).  
- A ring of **End Stone** tiles around the base slowly fills with **Purpur blocks** as uptime accumulates — a physical progress bar toward your SLO target.

**Fun factor:** The Dragon Egg is the rarest block in Minecraft — one per world. Making it represent your system's SLO turns uptime into a treasure to protect. End Crystals exploding when services die is the most dramatic visualization on this list. The egg *running away* when SLO drops is hilarious and terrifying.

**Technical feasibility:** **Easy-Medium.** Dragon Egg placement via `/setblock`. End Crystal spawning via `/summon ender_crystal`. Crystal "explosion" via `/particle` + `/playsound` + `/kill` (the entity, not an actual explosion). Egg teleportation via `/setblock air` + `/setblock dragon_egg` at new coords. Purpur progress ring is simple `/fill`. The SLO calculation is just uptime tracking in the worker. One of the more RCON-efficient ideas.

---

## Summary Matrix

| # | Name | Data Source | RCON Cost | Feasibility | Fun Factor | Sprint Candidate |
|---|------|------------|-----------|-------------|------------|-----------------|
| 1 | Trace River | OTLP Traces | Medium | Medium | ⭐⭐⭐⭐⭐ | S5-S6 |
| 2 | Enchanting Tower | OTLP Metrics | High | Medium-Hard | ⭐⭐⭐⭐ | S5-S6 |
| 3 | Log Campfires | OTLP Logs | Low-Medium | Medium | ⭐⭐⭐⭐ | S5 |
| 4 | Nether Portal Gateway | OTLP Metrics (HTTP) | Low | Medium | ⭐⭐⭐⭐ | S5 |
| 5 | Sculk Error Network | Health + Traces | Low | Easy-Medium | ⭐⭐⭐⭐⭐ | S4-S5 |
| 6 | Minecart Metric Rails | OTLP Metrics | High | Hard | ⭐⭐⭐ | Backlog |
| 7 | Villager Trading Hall | OTLP Traces | Medium | Medium | ⭐⭐⭐⭐ | S5-S6 |
| 8 | Redstone Clock Dashboard | Health history | Low | Easy-Medium | ⭐⭐⭐⭐ | S4 |
| 9 | Ender Chest Trace Explorer | OTLP Traces | High | Hard | ⭐⭐⭐⭐⭐ | S6+ |
| 10 | Dragon Health Egg | Health + SLO | Low | Easy-Medium | ⭐⭐⭐⭐⭐ | S4 |

---

## Rhodey's Recommendation

**Sprint 4 candidates (use existing health data, no new data source needed):**
1. **Dragon Health Egg** (#10) — Low RCON cost, easy feasibility, maximum fun. Ship it.
2. **Redstone Clock Dashboard** (#8) — Health history is already tracked. Just needs time-series storage + lamp grid.
3. **Sculk Error Network** (#5) — Mostly uses existing health data with some error cascade logic.

**Sprint 5 candidates (requires OTLP data ingestion — the big architectural investment):**
1. **Trace River** (#1) — Jeff specifically asked about traces. This is the headline feature.
2. **Log Campfires** (#3) — Low RCON cost, high visual impact.
3. **Nether Portal Gateway** (#4) — Natural extension of the village metaphor.

**Backlog:**
- Enchanting Tower, Minecart Rails, Villager Hall, Trace Explorer — all great but need the OTLP infrastructure first and have higher RCON budgets.

### Critical Architectural Decision Needed

All ideas numbered 1-4, 6-7, and 9 require **consuming OTLP data** (traces, metrics, logs) in the worker service. Today the worker only polls health endpoints. Adding OTLP ingestion is a **cross-cutting architectural change** that should be designed once and implemented as shared infrastructure before any individual feature. This is likely a Sprint 5 epic in itself.

Options:
- **A) Run a secondary OTLP receiver in the worker** — most control, but adds complexity.
- **B) Poll the Aspire dashboard API** — simpler, but the dashboard API isn't designed for this.
- **C) Share the OTLP collector and query stored data** — cleanest but depends on Aspire internals.

This decision should be made before committing to any OTLP-dependent feature.
