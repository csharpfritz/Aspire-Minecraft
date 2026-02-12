# Sprint 4 Technical Design Decisions

> **By:** Rhodey (Lead)
> **Date:** 2026-02-12
> **Status:** 📐 Design — pending team review

---

### Decision: Redstone Dashboard Wall placement west of village at X=-5

**What:** The Redstone Dashboard Wall is placed at `DashboardX = -5`, facing east toward the village. This is 11 blocks west of the fence perimeter — visible from the village gate but not overshadowing any buildings.

**Why:** Jeff specifically requested it "near the village but far enough away it isn't overshadowing the buildings." West placement uses negative-X space that is otherwise empty (village grows in positive-Z). The east-facing orientation means players see it when they exit the village gate and turn left.

**Trade-offs:** Could have placed it north (behind village) but that competes with village growth direction. Could have placed it further away but then it's outside view distance for players at the village.

---

### Decision: /clone shift-register for dashboard scrolling, not per-lamp updates

**What:** Each update cycle uses one `/clone` command to shift the entire lamp grid left by one column, then writes only the new rightmost column (N commands for N resources). Total: N+1 commands per cycle.

**Why:** Naive approach would update every lamp every cycle: N×columns commands. For 8 resources × 10 columns = 80 commands vs 9 commands with /clone. This is a 9× RCON savings, critical for staying within the 10 cmd/sec budget alongside other services.

**Risk:** `/clone` copies block states including redstone power. Must clone the power layer (x-1) not just the lamp layer (x). Tested in Paper 1.21 — `/clone` handles this correctly.

---

### Decision: Database resources get cylindrical buildings using circular geometry in 7×7 grid

**What:** Resources detected as databases via `IsDatabaseResource()` are built as cylindrical structures using polished deepslate, fitting within the existing 7×7 structure footprint. The circular footprint uses a 3-block radius approximation.

**Why:** Jeff requested "round/cylindrical buildings — like database cylinder icons in architecture diagrams." The 7×7 grid cell perfectly accommodates a radius-3 circle. Deepslate palette is dark and distinct from all other structure types.

**Trade-off:** Cylinder construction requires ~88 RCON commands vs ~15 for a watchtower. Acceptable because it's a one-time build, and database resources are typically <30% of total resources.

---

### Decision: Azure detection via resource type string matching, not SDK dependency

**What:** `IsAzureResource()` uses string matching on the resource type (starts with "azure.", contains "azure", or matches known Azure-only types like "cosmosdb", "servicebus"). No Azure SDK package reference needed.

**Why:** Avoids introducing `Azure.ResourceManager.*` dependencies into the main package, which was already decided against (separate package for Azure SDK integration). String matching works for the visual theming use case — we're just choosing a building color, not making API calls.

**Risk:** False positives are harmless (worst case: a non-Azure resource gets a blue banner). False negatives are unlikely since Aspire resource types are well-defined strings.

---

### Decision: Azure banner on ALL Azure resources regardless of structure type

**What:** The light_blue_banner is placed on the rooftop of any structure when `IsAzureResource()` returns true. This applies even to database cylinders (Azure SQL gets a cylinder + azure banner). The banner is additive — it doesn't change the building shape, just adds the flag.

**Why:** Jeff asked for "Azure-related resources should have a bright azure blue flag/banner on top." Making it additive means a resource's building shape communicates its function (database, project, container) while the banner communicates its origin (Azure). Players can spot Azure resources at a glance across the village.

---

### Decision: Sprint 4 scope is 14 issues — dashboard, buildings, Dragon Egg, DX polish, docs

**What:** Sprint 4 includes: Redstone Dashboard (4 issues), Enhanced Buildings (3 issues), Dragon Egg monument (1 issue), DX polish (3 issues: WithAllFeatures, env var tightening, welcome teleport), and documentation (3 issues: README, user-docs, tests).

**Why:** This balances Jeff's visual enhancement requests (dashboard, buildings, Dragon Egg) with the tech debt items recommended since Sprint 2 (WithAllFeatures, env var checks). Documentation is mandatory per Jeff's directive. Sculk Error Network and OTLP features defer to Sprint 5.

**Cut line:** If sprint runs long, drop welcome teleport first (M, nice-to-have), then Dragon Egg (L, can slip to Sprint 5 without blocking anything).

---

### Decision: HealthHistoryTracker as a separate class, not embedded in AspireResourceMonitor

**What:** Health history tracking (ring buffer per resource) lives in a new `HealthHistoryTracker` class, not added to `AspireResourceMonitor`.

**Why:** `AspireResourceMonitor` has a clear responsibility: discover resources and poll health. Adding time-series storage blurs that. `HealthHistoryTracker` is consumed only by the dashboard service — it's optional and shouldn't burden the core monitoring path. It's also independently testable.
