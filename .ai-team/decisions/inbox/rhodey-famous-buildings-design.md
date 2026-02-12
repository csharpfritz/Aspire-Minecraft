### 2026-02-12: Famous Buildings API design
**By:** Rhodey
**What:** Designed the `AsMinecraftFamousBuilding(FamousBuilding)` extension method and `FamousBuilding` enum for assigning iconic real-world buildings (Big Ben, Eiffel Tower, Colosseum, Pyramid, etc.) to any Aspire resource. The API lives on `IResourceBuilder<T> where T : IResource` — not on the Minecraft server builder — because the building choice belongs to the resource being visualized. Selection flows via `FamousBuildingAnnotation` → `WithMonitoredResource` deferred env var callback → `ASPIRE_RESOURCE_{NAME}_FAMOUS_BUILDING` env var → worker reads and overrides auto-detected structure type. Enum has 15 buildings spanning 6 continents, all constrained to 15×15 footprint. Building models are pure C# methods (no JSON/NBT), matching the existing `StructureBuilder` pattern. Feature requires `WithGrandVillage()` for full-size rendering. Full design at `docs/designs/famous-buildings-design.md`.
**Why:** Jeff wants conference demos where resources are represented by recognizable landmarks. The annotation-based approach is order-independent, the env var pattern matches all existing resource metadata flow, and the enum keeps the API surface small and intentional. Two-sprint phasing (API+3 buildings, then remaining 12) avoids a single oversized sprint. Famous buildings override auto-detection but don't break it — resources without annotations continue to work exactly as before.

#### Key Decisions

1. **Extension method targets `IResourceBuilder<T> where T : IResource`** — broadest constraint; annotation is inert unless resource is monitored.
2. **Annotation + deferred env var callback** — guarantees order-independence (can call `AsMinecraftFamousBuilding` before or after `WithMonitoredResource`).
3. **Pure C# building models** — no JSON schemas, no NBT files, no runtime file loading. Consistent with existing `StructureBuilder` pattern.
4. **15 buildings in the enum** — geographic diversity, Minecraft buildability, distinctive silhouettes at 15–30 block scale.
5. **Requires `WithGrandVillage()`** — famous buildings at 7×7 would be unrecognizable. Worker logs warning and falls back to auto-detection if grid is too small.
6. **Two-sprint phasing** — Sprint A: API + infrastructure + 3 starter models. Sprint B: remaining 12 models. Avoids monolithic sprint.
7. **200 RCON command hard cap per building** — prevents individual models from becoming performance problems.
