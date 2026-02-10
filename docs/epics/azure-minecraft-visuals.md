# Azure Resource Visualization in Minecraft — Design Document

> **Author:** Rocket (Integration Dev)
> **Status:** Draft — Design Only (no implementation)
> **Date:** 2026-02-10
> **Epic:** Azure Resource Group ↔ Minecraft World

---

## 1. Design Philosophy

### Two Universes, One World

The Aspire village is a **local dev workshop** — cozy, intimate, wood-and-stone buildings that feel handmade. The Azure district should feel like stepping into a **different civilization entirely**.

| | Aspire Village | Azure District |
|---|---|---|
| **Theme** | Medieval village / workshop | Ancient-modern hybrid — Endstone & Prismarine citadel |
| **Materials** | Wood, stone, cobblestone, wool | Prismarine, end stone, quartz, blackstone, crying obsidian |
| **Scale** | Small buildings (5–10 blocks tall) | Grand structures (8–16 blocks tall) |
| **Palette** | Warm tones (oak, campfire, torches) | Cool tones (cyan, teal, purple, white) |
| **Lighting** | Torches, glowstone, redstone lamps | Sea lanterns, end rods, soul lanterns, amethyst |
| **Vibe** | "My workshop" | "The cloud kingdom" |

### Why Different?

- A player should know immediately which district they're in.
- Azure is *infrastructure* — it should feel monumental, engineered, slightly alien. Prismarine (ocean monument blocks) and end stone (End dimension) evoke "something built by a higher power."
- The contrast makes both more memorable. At a conference demo, the camera pans from the cozy village to the gleaming citadel — instant "wow."

### The Azure Color Language

Azure's brand color is `#0078D4` (blue). In Minecraft terms:
- **Primary:** `prismarine_bricks`, `dark_prismarine`, `cyan_terracotta`
- **Accent:** `quartz_block`, `smooth_quartz`, `end_stone_bricks`
- **Dramatic:** `crying_obsidian`, `blackstone`, `gilded_blackstone`
- **Glow:** `sea_lantern`, `end_rod`, `amethyst_cluster`

---

## 2. Azure Resource Type → Minecraft Structure Mapping

### Summary Table

| Azure Resource | Minecraft Structure | Key Materials | Footprint | Height |
|---|---|---|---|---|
| App Service | **Cathedral** | Quartz, prismarine, stained glass | 9×9 | 14 |
| Azure SQL Database | **Vault Library** | Deepslate bricks, amethyst, bookshelves | 9×7 | 10 |
| Storage Account | **Crystal Silo** | Blue ice, packed ice, glass | 7×7 | 12 |
| Key Vault | **Obsidian Stronghold** | Obsidian, crying obsidian, iron bars | 5×5 | 8 |
| Container Apps | **Modular Dock** | Iron blocks, dark prismarine, chains | 9×7 | 8 |
| AKS (Kubernetes) | **Honeycomb Tower** | Honeycomb blocks, copper, oxidized copper | 9×9 | 16 |
| Virtual Network | **Gateway Arch** | Purpur blocks, end stone bricks, end rods | 7×5 | 12 |
| Function App | **Lightning Spire** | Copper, lightning rod, redstone blocks | 5×5 | 14 |
| Cosmos DB | **Star Observatory** | Lapis lazuli, gold, glass dome | 9×9 | 12 |
| Redis Cache | **Redstone Engine** | Redstone blocks, pistons, observers | 7×5 | 7 |
| Service Bus | **Rail Station** | Polished blackstone, powered rails, hoppers | 11×7 | 9 |
| Event Hub | **Signal Tower** | Dark prismarine, nether bricks, soul fire | 5×5 | 16 |
| Application Insights | **Spyglass Tower** | Tinted glass, amethyst, copper | 5×5 | 14 |
| API Management | **Grand Gate** | Quartz pillars, iron doors, banners | 11×5 | 10 |
| Load Balancer | **Roundabout Fountain** | Smooth stone, water, prismarine | 9×9 | 6 |

### Detailed Structure Designs

#### App Service — "The Cathedral"
**Why:** App Service is the workhorse of Azure — it hosts your web apps. A cathedral is the central, most visible building in any medieval city. Grand, welcoming, always open.

- **Block palette:** `quartz_block` walls, `quartz_pillar` columns, `prismarine_bricks` floor, `cyan_stained_glass_pane` windows, `dark_prismarine` roof trim
- **Dimensions:** 9×9 footprint, 14 blocks tall (tallest common structure)
- **Distinctive feature:** Rose window — a 3×3 `cyan_stained_glass` circle on the front face, framed by `quartz_stairs`. Two quartz pillar spires flanking the entrance.
- **Health indicator:** Sea lantern embedded in the rose window center. Beacon beam through the roof peak.
- **Build commands:** ~30 RCON commands (fill for walls, setblock for details)

#### Azure SQL Database — "The Vault Library"
**Why:** Databases store structured knowledge. A vault library with bookshelves and amethyst geode accents says "precious, organized data."

- **Block palette:** `deepslate_brick` walls, `bookshelf` interior accents, `amethyst_block` floor highlights, `smooth_basalt` trim, `lantern` lighting
- **Dimensions:** 9×7 footprint, 10 blocks tall
- **Distinctive feature:** Visible bookshelf rows through `iron_bars` window grating — like peering into a vault of knowledge. Amethyst cluster "crystals" on corners.
- **Health indicator:** `amethyst_cluster` on roof peak (grows → large when healthy, small when degraded). Redstone lamp behind iron bars.
- **Build commands:** ~25 RCON commands

#### Storage Account — "The Crystal Silo"
**Why:** Storage is about volume and containment. A tall ice/glass silo looks like a frozen data container — cold storage made literal.

- **Block palette:** `blue_ice` base ring, `packed_ice` walls, `glass` upper section (visible interior), `prismarine_slab` cap
- **Dimensions:** 7×7 footprint, 12 blocks tall (cylindrical feel using octagonal layout)
- **Distinctive feature:** Transparent glass midsection — you can see `blue_ice` blocks stacked inside like data blocks. The silo "glows" from within via sea lanterns behind the ice.
- **Health indicator:** Sea lantern at the top, visible through glass crown. Beacon column through center.
- **Build commands:** ~20 RCON commands

#### Key Vault — "The Obsidian Stronghold"
**Why:** Key Vault is about secrets and security. Obsidian is the hardest common block in Minecraft — unbreakable by most means. Small, impenetrable, mysterious.

- **Block palette:** `obsidian` walls, `crying_obsidian` accents (purple particle drip), `iron_bars` over windows, `gilded_blackstone` door frame, `soul_lantern` lighting
- **Dimensions:** 5×5 footprint, 8 blocks tall (deliberately small — vaults are compact)
- **Distinctive feature:** Crying obsidian tears (built-in purple particles) running down the walls. No visible entrance — sealed. Iron bars on all faces.
- **Health indicator:** Soul lantern color can't change, so use `redstone_lamp` inside behind iron bars (lit=healthy). `crying_obsidian` particles provide constant ambient effect.
- **Build commands:** ~15 RCON commands (small structure = fewer commands)

#### Container Apps — "The Modular Dock"
**Why:** Container Apps are containers that scale. A shipping dock with modular bays = containers ready to deploy.

- **Block palette:** `iron_block` frame, `dark_prismarine` floor, `chain` hanging details, `barrel` cargo, `dark_oak_trapdoor` bay doors
- **Dimensions:** 9×7 footprint, 8 blocks tall
- **Distinctive feature:** Open-front bay doors (trapdoors in open position) with barrels and chests visible inside — a cargo dock with containers ready to ship. Chains hanging from the ceiling.
- **Health indicator:** Redstone lamp above each bay door. Beacon at the back wall.
- **Build commands:** ~25 RCON commands

#### AKS (Kubernetes) — "The Honeycomb Tower"
**Why:** Kubernetes orchestrates pods. Honeycomb blocks = cells/pods. A multi-tiered tower of honeycomb cells = a cluster of pods managed by a central controller.

- **Block palette:** `honeycomb_block` cell walls, `copper_block` frame (oxidizes over time = age of cluster!), `oxidized_copper` trim, `waxed_copper_block` accents, `bee_nest` decorative
- **Dimensions:** 9×9 footprint, 16 blocks tall (tallest structure — Kubernetes is complex)
- **Distinctive feature:** Honeycomb pattern visible on exterior — alternating honeycomb blocks and copper creates a cell-grid texture. Three tiers (base=infrastructure, mid=pods, top=control plane).
- **Health indicator:** Beacon through the top. Sea lantern at each tier boundary. The copper oxidation is a neat real-time metaphor but not controllable via RCON — purely decorative.
- **Build commands:** ~35 RCON commands

#### Virtual Network — "The Gateway Arch"
**Why:** VNets connect things. A gateway arch with end rods = a portal/pathway that resources pass through.

- **Block palette:** `purpur_block` arch, `purpur_pillar` columns, `end_stone_bricks` base, `end_rod` lighting along the arch
- **Dimensions:** 7×5 footprint, 12 blocks tall (vertical arch)
- **Distinctive feature:** Open archway you can walk through — symbolizing network transit. End rods line the arch like fiber optic cables. End stone bricks evoke "otherworldly infrastructure."
- **Health indicator:** End rods along the arch serve as always-on lighting. Redstone lamp at keystone (top center of arch).
- **Build commands:** ~20 RCON commands

#### Function App — "The Lightning Spire"
**Why:** Functions are event-driven, fast, ephemeral — like lightning strikes. A narrow copper spire with a lightning rod on top.

- **Block palette:** `copper_block` shaft, `cut_copper` details, `lightning_rod` crown, `redstone_block` core, `tinted_glass` windows
- **Dimensions:** 5×5 footprint, 14 blocks tall (tall and narrow = fast and focused)
- **Distinctive feature:** Lightning rod at the apex — during thunderstorms (triggered by weather effects when resources are unhealthy), actual lightning could strike the function spire. `redstone_block` core visible through tinted glass = "charged and ready."
- **Health indicator:** Redstone lamp midway up the spire. Redstone torch at the base (always on = always ready).
- **Build commands:** ~18 RCON commands

#### Cosmos DB — "The Star Observatory"
**Why:** Cosmos = universe. A domed observatory with a glass ceiling for stargazing. The gold and lapis palette evokes celestial maps.

- **Block palette:** `lapis_block` base walls, `gold_block` trim, `glass` dome ceiling, `glowstone` star map floor, `dark_prismarine` foundation
- **Dimensions:** 9×9 footprint, 12 blocks tall (wide dome)
- **Distinctive feature:** Glass dome roof — visible from above and when inside. Glowstone blocks in the floor form a constellation pattern. Gold band around the dome's equator.
- **Health indicator:** Beacon beam through the glass dome (visible as a star beam from a distance). Sea lantern in dome apex.
- **Build commands:** ~28 RCON commands

#### Redis Cache — "The Redstone Engine"
**Why:** Redis = fast, in-memory, key-value. Redstone is Minecraft's electricity — fast signal propagation. An engine block made of redstone machinery.

- **Block palette:** `redstone_block` core, `piston` faces, `observer` monitoring faces, `hopper` intake/output, `iron_block` frame
- **Dimensions:** 7×5 footprint, 7 blocks tall (compact and dense)
- **Distinctive feature:** Pistons on the front face (suggesting mechanical motion). Observers watching outward. Hoppers at the base as data intake. The whole thing looks like a working engine.
- **Health indicator:** Redstone lamp on top (powered by the redstone blocks = always on when healthy). Redstone torch at entry.
- **Build commands:** ~20 RCON commands

#### Service Bus — "The Rail Station"
**Why:** Service Bus is a message broker — messages get queued and routed. A rail station with powered rails and hoppers = messages arriving, being processed, departing.

- **Block palette:** `polished_blackstone_bricks` walls, `powered_rail` tracks, `hopper` ticket counters, `dark_oak_planks` platform, `chain` barriers
- **Dimensions:** 11×7 footprint, 9 blocks tall (wide — it's a station)
- **Distinctive feature:** Powered rail tracks running through the structure (even if non-functional, they're visually unmistakable). Open sides for "arrivals" and "departures." Hoppers as message intake points.
- **Health indicator:** Redstone lamp above each rail track. Sea lantern at the station clock position (front center, high).
- **Build commands:** ~28 RCON commands

#### Event Hub — "The Signal Tower"
**Why:** Event Hub ingests millions of events. A tall, narrow tower with soul fire = a signal beacon broadcasting events.

- **Block palette:** `dark_prismarine` shaft, `nether_brick` base, `soul_lantern` lighting, `soul_campfire` crown, `cyan_stained_glass_pane` windows
- **Dimensions:** 5×5 footprint, 16 blocks tall (second tallest — reaching for the sky to broadcast signals)
- **Distinctive feature:** Soul campfire at the top — eerie blue flame visible from far away. The tower's height makes it a landmark. Soul lanterns spiral up the exterior.
- **Health indicator:** Soul lanterns can't change, so use redstone lamp at mid-height behind glass. Beacon beam for healthy.
- **Build commands:** ~22 RCON commands

#### Application Insights — "The Spyglass Tower"
**Why:** App Insights watches everything. A spyglass/telescope tower — tinted glass walls let it observe while remaining mysterious.

- **Block palette:** `tinted_glass` walls (semi-transparent), `amethyst_block` frame, `copper_block` base, `spyglass`-inspired shape, `end_rod` antenna
- **Dimensions:** 5×5 footprint, 14 blocks tall
- **Distinctive feature:** Tinted glass on all sides — you can barely see in, but the tower can "see" out. End rod antenna at the top. Amethyst frame gives a crystalline, high-tech feel.
- **Health indicator:** Sea lantern inside (glows through tinted glass softly). Beacon beam through tinted glass roof.
- **Build commands:** ~18 RCON commands

#### API Management — "The Grand Gate"
**Why:** APIM is a gateway — it manages, secures, and routes API traffic. A grand gate with banners and iron doors = official controlled entry.

- **Block palette:** `quartz_pillar` columns, `smooth_quartz` walls, `iron_door` entrance, `cyan_banner` decoration, `dark_prismarine` base
- **Dimensions:** 11×5 footprint, 10 blocks tall (wide and imposing)
- **Distinctive feature:** Two tall quartz pillar columns flanking an iron door. Cyan banners hanging from each column. Wide enough to walk through — this is the official entrance to your API surface.
- **Health indicator:** Redstone lamp above the iron door (lit = gate is open and healthy). Beacon behind the gate.
- **Build commands:** ~22 RCON commands

#### Load Balancer — "The Roundabout Fountain"
**Why:** Load balancer distributes traffic evenly. A circular fountain distributes water in all directions. The roundabout concept = traffic being routed.

- **Block palette:** `smooth_stone` base ring, `prismarine` inner ring, `water` center, `sea_lantern` lights, `prismarine_brick_slab` edges
- **Dimensions:** 9×9 footprint, 6 blocks tall (low and wide — it's infrastructure, not a building)
- **Distinctive feature:** Working water fountain in the center. Prismarine ring channels. Low profile — it's literally the infrastructure everything else sits on. Four `sea_lantern` lights at cardinal directions.
- **Health indicator:** Sea lanterns in the ring (always on). Beacon in the fountain center.
- **Build commands:** ~18 RCON commands

---

## 3. Layout Design

### District Separation

```
                NORTH
                  |
   ┌──────────────┼──────────────┐
   │              │              │
   │   ASPIRE     │    AZURE     │
   │   VILLAGE    │   CITADEL    │
   │              │              │
   │  (X: 10–40)  │  (X: 60–?)  │
   │              │              │
   │  BaseX=10    │  BaseX=60   │
   │              │              │
   └──────────────┼──────────────┘
                  |
                SOUTH

   ← 20-block gap with a grand prismarine road →
```

**Key dimensions:**
- Aspire village occupies X=10 to ~40 (2-column grid, Spacing=10)
- **20-block buffer** between districts — a wide `prismarine_brick` boulevard with `end_rod` lampposts
- Azure citadel starts at **X=60**, same Y=-60 base level
- The boulevard is the "conference camera path" — walk from village to citadel

### Azure Citadel Layout

Rather than a simple 2×N grid, the Azure district uses a **concentric ring layout** inspired by Azure's own region/resource group hierarchy:

```
                    ┌─────────────┐
                    │  VNet Arch  │   ← Gateway at district entrance
                    └──────┬──────┘
                           │
              ┌────────────┼────────────┐
              │            │            │
        ┌─────┴─────┐  ┌──┴───┐  ┌────┴─────┐
        │ App Svc   │  │ APIM │  │ Load Bal  │     ← FRONT ROW: public-facing
        │ Cathedral │  │ Gate │  │ Fountain  │
        └─────┬─────┘  └──┬───┘  └────┬─────┘
              │            │           │
        ┌─────┴─────┐  ┌──┴───┐  ┌────┴─────┐
        │ Func App  │  │ Cont │  │   AKS    │     ← COMPUTE ROW: processing
        │ Spire     │  │ Apps │  │ Honeycomb │
        └─────┬─────┘  └──┬───┘  └────┬─────┘
              │            │           │
        ┌─────┴─────┐  ┌──┴───┐  ┌────┴─────┐
        │ SQL DB    │  │Cosmos│  │  Redis   │     ← DATA ROW: storage
        │ Vault Lib │  │ Obs  │  │ Engine   │
        └─────┬─────┘  └──┬───┘  └────┴─────┘
              │            │           │
        ┌─────┴─────┐  ┌──┴───┐  ┌────┴─────┐
        │ Key Vault │  │ Svc  │  │ Storage  │     ← INFRA ROW: foundational
        │ Stronghld │  │ Bus  │  │ Silo     │
        └───────────┘  └──────┘  └──────────┘
              │            │
        ┌─────┴─────┐  ┌──┴───┐
        │ Event Hub │  │ App  │                    ← MONITORING ROW: observability
        │ Sig Tower │  │Insght│
        └───────────┘  └──────┘
```

### Layout Algorithm

```
Azure Citadel Grid:
  BaseX = 60        (20-block gap from Aspire village edge)
  BaseY = -60       (same flat world level)
  BaseZ = 0         (same Z origin)
  Columns = 3       (wider than Aspire's 2 — Azure has more resources)
  Spacing = 14      (larger structures need more room — 11×9 is the max footprint)
  RowSpacing = 14   (front-to-back gap)
```

### Grouping Strategy

Resources are grouped by **functional tier**, not alphabetically:

| Row | Tier | Resource Types | Why |
|---|---|---|---|
| 0 | **Gateway** | VNet, APIM, Load Balancer | Public-facing entry points |
| 1 | **Compute** | App Service, Container Apps, AKS, Function App | Processing tier |
| 2 | **Data** | SQL DB, Cosmos DB, Redis, Storage Account | Persistence tier |
| 3 | **Security & Messaging** | Key Vault, Service Bus, Event Hub | Cross-cutting infrastructure |
| 4 | **Observability** | Application Insights | Monitoring (watches everything) |

This tier ordering mirrors how Azure architects think about their systems — and produces a natural "walk-through" from the public edge to the inner sanctum.

### Handling 50+ Resources

Real Azure resource groups can have 50+ resources. Design for scale:

1. **Multiple instances of same type:** Stack vertically or extend rows. If there are 5 App Services, the compute row gets 5 cathedrals in a line.
2. **Pagination by chunk distance:** With `VIEW_DISTANCE=6` (6 chunks = 96 blocks), the entire citadel must fit within ~90 blocks. At Spacing=14, that's 6 columns or ~18 structures per screen. Beyond that, use a **second citadel plane** offset on the Z axis.
3. **Resource count → layout mode:**
   - 1–15 resources: 3×5 grid (single screen)
   - 16–30 resources: 3×10 grid (scroll by walking north)
   - 31–50 resources: Two 3×N planes, Z offset = 50
   - 50+: Three planes — consider culling non-essential resources (auto-created managed identities, etc.)

### Paths and Connections

- **Main boulevard:** `prismarine_brick` road from Aspire village to Azure citadel gate. 5 blocks wide, `end_rod` lampposts every 10 blocks.
- **Tier rows:** `dark_prismarine_slab` paths connecting structures within a tier.
- **Cross-tier connections:** `prismarine_slab` paths between rows (north-south).
- **VNet → Subnet visualization (future):** Redstone wire or carpet runners showing network topology. Different colored carpets per subnet.
- **Dependency lines (future):** Builds on the Redstone Dependency Graph decision — redstone wires from App Service to SQL DB, etc.

---

## 4. Health Visualization

### Reused Patterns (from Aspire Village)

These patterns transfer directly — same RCON commands, same services:

| Feature | Azure Behavior | Notes |
|---|---|---|
| **Beacon beams** | Color by resource type (see below) | Same `BeaconTowerService` pattern, new color map |
| **Guardian mobs** | Iron golem (healthy) / zombie (unhealthy) near structures | Same `GuardianMobService`, new coordinates |
| **Particles** | Above structure roofs | Same `ParticleEffectService`, new coordinates |
| **Boss bar** | Aggregate health % across all Azure resources | Separate boss bar? Or combined with Aspire? |
| **Fireworks** | All-green recovery celebration | Same `FireworksService` |
| **Weather** | Shared with Aspire — one world, one weather | Cannot be per-district |

### Azure Beacon Color Map

Azure resources don't map to Aspire's Project/Container/Executable types. New color palette:

| Azure Category | Healthy Beacon Glass | Rationale |
|---|---|---|
| Compute (App Svc, Func, AKS, ContainerApps) | `cyan_stained_glass` | Azure compute = cyan |
| Data (SQL, Cosmos, Redis, Storage) | `blue_stained_glass` | Data = blue (depth) |
| Networking (VNet, LB, APIM) | `purple_stained_glass` | Network = purple (infrastructure) |
| Security (Key Vault) | `black_stained_glass` | Security = dark, opaque |
| Messaging (Service Bus, Event Hub) | `orange_stained_glass` | Events = orange (energy) |
| Observability (App Insights) | `magenta_stained_glass` | Monitoring = magenta (distinct) |

**Unhealthy:** Always `red_stained_glass` (consistent with Aspire village).
**Starting/Provisioning:** Always `yellow_stained_glass` (consistent with Aspire village).

### Azure-Specific Health States

Azure has more nuanced lifecycle states than Aspire's simple Healthy/Unhealthy/Unknown:

#### Runtime States

| Azure State | Minecraft Representation | Details |
|---|---|---|
| **Running** | ✅ Healthy (type-colored beacon, glowstone lamp, iron golem) | Standard healthy display |
| **Starting** | 🟡 Starting (yellow beacon, sea lantern, no mob) | Same as Aspire "Unknown" |
| **Stopping** | 🟠 Transition (orange beacon, redstone lamp blinking*, particles) | Pulse redstone lamp via repeating command |
| **Stopped** | ⚫ Dormant (no beacon, redstone lamp off, cobweb overlay) | `cobweb` blocks placed on structure corners = "dusty, dormant" |
| **Failed** | 🔴 Unhealthy (red beacon, zombie mob, thunder particles) | Same as Aspire unhealthy |
| **Deallocated** | ⬛ Decommissioned (no beacon, structure darkened, `soul_sand` perimeter) | Soul sand around the base = "ghost of a resource." Remove mob. |

*Blinking effect: Rapidly toggling redstone lamps is not practical via RCON (250ms throttle). Instead, use `particle minecraft:dust 1.0 0.5 0.0 1` (orange dust) above the structure to indicate transition.

#### Provisioning States

| Provisioning State | Minecraft Representation |
|---|---|
| **Creating** | Structure builds block-by-block in real time (the normal build sequence, but exposed as a status indicator) |
| **Updating** | `particle minecraft:enchant` effect (sparkles) over the structure |
| **Deleting** | Structure deconstructs block-by-block (reverse build — dramatic!) |
| **Succeeded** | Standard healthy display |
| **Failed** | Red beacon + `particle minecraft:angry_villager` above structure |

### Deallocated vs. Stopped vs. Failed — Visual Distinction

This is the trickiest design challenge. All three are "not running," but they mean very different things:

```
STOPPED (intentional pause):
  - Structure intact but dark (all lamps off)
  - Cobwebs on corners (dusty from disuse)
  - No beacon beam
  - No guardian mob
  - Sign reads: "⏸ Stopped"

DEALLOCATED (not consuming resources):
  - Structure intact but surrounded by soul_sand ring
  - Structure partially "faded" — some blocks replaced with gray concrete
  - No beacon beam, no mob
  - Sign reads: "💤 Deallocated"

FAILED (broken):
  - Structure intact but red particles raining down
  - Red beacon beam
  - Zombie mob nearby
  - Netherrack + fire on roof (the building is "on fire")
  - Sign reads: "🔥 Failed"
```

---

## 5. Interactive Features (Future)

### Proximity Information (Medium effort)

**Concept:** Walk near a structure → get detailed resource info in the action bar or chat.

**Implementation path:**
- RCON `execute as @a[x=...,y=...,z=...,distance=..5]` to detect players near structures
- Trigger `tellraw @a[near structure]` with resource details
- Poll every 5 seconds (not every tick — too expensive)

**Info displayed:**
```
☁️ my-app-service | Standard S1 | East US 2
Status: Running | Uptime: 14d 3h | Cost: $73.20/mo
```

### Resource Detail Signs (Low effort)

Place `oak_sign` or `dark_oak_sign` on each structure's front face with key details:

```
Line 1: ☁️ Resource Name
Line 2: SKU / Tier
Line 3: Region
Line 4: Status
```

Signs support 4 lines of ~15 characters. Use `data merge block` to update sign text via RCON. This is achievable today with existing RCON patterns.

### Advancement Achievements (Medium effort)

Minecraft advancements (achievements) that fire when conditions are met:

| Achievement | Trigger | Icon |
|---|---|---|
| "Cloud Architect" | All Azure resources healthy simultaneously | `diamond` |
| "First Deploy" | First Azure resource reaches Running state | `iron_pickaxe` |
| "Zero Downtime" | No unhealthy events for 10 minutes | `golden_apple` |
| "Cost Watcher" | View cost info on a sign | `gold_ingot` |
| "Full Stack" | Both Aspire village and Azure citadel fully healthy | `nether_star` |

Implementation via `advancement grant @a only custom:{id}` RCON command.

### Day/Night Cycle Tied to Deployment Windows (Low effort, high delight)

**Concept:** Minecraft time reflects deployment safety.

- **Deployment window open:** `time set day` — bright, clear, safe to deploy
- **Deployment window closed:** `time set night` — dark, hostile mobs could spawn (if we enable them), danger
- **Maintenance window:** `time set 13000` (sunset) — twilight, things are changing

This maps real operational concepts to visceral Minecraft feelings. Nobody deploys on a dark and stormy night.

### Nether Portal Drill-Down (High effort, conference showstopper)

**Concept:** Place a `nether_portal` frame next to a structure. Walking through teleports the player to a "metrics dimension" — a separate area showing detailed graphs rendered as block art.

**Implementation sketch:**
- Build nether portal frame (obsidian rectangle) near each structure
- The Nether dimension would contain per-resource detail rooms
- Each room: block-art graphs (colored wool columns for metrics over time), signs with detailed stats, hologram dashboards
- RCON `tp @a` to move players (simpler than actual nether portal mechanics)

**Reality check:** Building block-art graphs in real time is expensive (many setblock commands). Better as a pre-built area that updates key values via signs/holograms rather than re-rendering graph shapes each cycle.

---

## 6. Technical Constraints & Mitigations

### RCON Command Budget

| Concern | Number | Mitigation |
|---|---|---|
| Commands per structure build | 15–35 | Comparable to Aspire structures (15–25). Acceptable. |
| Total build for 15 Azure resources | ~375 commands | At 250ms throttle = ~94 seconds. Long but one-time. |
| Total build for 50 resources | ~1,250 commands | ~5.2 minutes. Need a loading screen (boss bar progress). |
| Per-cycle health update (15 resources) | ~30 commands | Beacon + lamp + mob + sign = ~2 per resource. Fine. |
| Per-cycle health update (50 resources) | ~100 commands | At 250ms = 25 seconds per cycle. May need to batch or update only changed resources. |

### Build Time Optimization

For large resource groups (30+ resources), the initial structure build will take minutes. Mitigations:

1. **Progress boss bar:** `bossbar set azure:build value {pct}` — shows "Building Azure Citadel: 47%"
2. **Incremental building:** Build structures as resources are discovered, not all at once
3. **Priority building:** Build the VNet gate and first compute resource immediately; queue the rest
4. **Delta updates:** Only rebuild structures that changed since last poll (track resource list hash)
5. **Batch fills:** Use `fill` (one command for many blocks) over `setblock` (one command per block) wherever possible. The current `fill ... hollow` pattern is already optimal for walls.

### Chunk Loading

With `VIEW_DISTANCE=6` (96-block render radius) and `MAX_WORLD_SIZE=256`:

- Aspire village (X=10–40): ~30 blocks wide — fits in 2 chunks
- Azure citadel at X=60: 20 blocks from village edge — player at the boulevard midpoint sees both
- Citadel width (3 columns × 14 spacing): ~42 blocks — fits in 3 chunks
- Citadel depth (5 rows × 14 spacing): ~70 blocks — 4–5 chunks
- **Total world usage:** ~120 blocks X, ~70 blocks Z — well within 256-block world border

### Terrain

Current config uses `LEVEL_TYPE=flat` which generates a superflat world (grass → dirt → bedrock). This is fine:

- **No terrain generation needed** — flat is actually better for a dashboard world
- All structures built at Y=-60 (underground level) or above — consistent with Aspire village
- If we wanted dramatic terrain for the Azure district (a raised platform?), we'd need to fill a platform first: `fill 55 -60 -5 140 -58 75 minecraft:prismarine_bricks` — one command, but adds 3-block elevation

**Recommendation:** Keep flat. The structures themselves provide all the visual interest. A raised prismarine platform (2-block height) for the Azure district is a nice touch — one `fill` command — but not required.

### Structure Build Ordering

Build in this order for maximum visual impact:

1. **Boulevard** (prismarine road connecting districts) — immediate spatial context
2. **VNet Gateway Arch** — "entrance" to the Azure district
3. **Compute row** (App Service cathedral first) — most visually impressive
4. **Data row**
5. **Infrastructure row**
6. **Monitoring row**
7. **Beacon towers** (after structures, so beams are visible through completed buildings)

---

## 7. Conference Demo "Wow" Moments

These are the shots that make people pull out their phones:

1. **The Pan:** Camera slowly moving from the cozy Aspire workshop village, down the lamplit prismarine boulevard, into the gleaming Azure citadel. Two civilizations, one world.

2. **The Break:** Stop an Azure resource (App Service). The cathedral's rose window goes dark. Red beacon beam shoots up. Zombie spawns. Thunder rolls. The boss bar drops. The audience gasps.

3. **The Recovery:** Restart the resource. Lightning strikes the cathedral (deployment fanfare). The rose window lights up cyan. Green beacon. Iron golem replaces zombie. Fireworks burst. Boss bar returns to green. The audience cheers.

4. **The Drill-Down (future):** Walk up to the SQL Database vault. Signs show table count, DTU usage, region. Step through the nether portal. Materialize in a metrics room with block-art graphs of query performance. The audience loses their minds.

5. **The Scale:** Zoom out (increase render distance temporarily) to show 40+ Azure resources rendered as a full citadel. Each with its own beacon beam creating a forest of colored light. The audience takes photos.

---

## Appendix: Block Palette Quick Reference

### Azure Primary Palette
| Block | Used For | Obtainable in Creative? |
|---|---|---|
| `prismarine_bricks` | Roads, floors, accent walls | ✅ |
| `dark_prismarine` | Foundations, dramatic walls | ✅ |
| `quartz_block` | App Service, APIM walls | ✅ |
| `quartz_pillar` | Columns, pillars | ✅ |
| `end_stone_bricks` | VNet, accent walls | ✅ |
| `deepslate_bricks` | SQL DB walls | ✅ |
| `obsidian` | Key Vault walls | ✅ |
| `crying_obsidian` | Key Vault accents | ✅ |
| `honeycomb_block` | AKS cell walls | ✅ |
| `copper_block` | Function App, AKS frames | ✅ |

### Azure Accent Palette
| Block | Used For |
|---|---|
| `sea_lantern` | Primary healthy light |
| `end_rod` | Lampposts, antennas |
| `soul_lantern` | Key Vault, Event Hub lighting |
| `amethyst_block` | SQL DB, App Insights frames |
| `amethyst_cluster` | SQL DB health indicator |
| `redstone_block` | Function App, Redis core |
| `tinted_glass` | App Insights walls |
| `soul_campfire` | Event Hub crown |
| `crying_obsidian` | Key Vault ambient particles |

### Health Indicator Blocks
| State | Block |
|---|---|
| Healthy | `glowstone` (warm), `sea_lantern` (cool) |
| Unhealthy | `redstone_lamp` (off) + red beacon |
| Starting | `sea_lantern` + yellow beacon |
| Stopped | All lamps off + `cobweb` corners |
| Deallocated | `soul_sand` perimeter + gray concrete patches |
| Failed | `netherrack` + `fire` on roof + red beacon |
