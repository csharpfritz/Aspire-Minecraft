# Design Review — 2026-02-27
**Facilitator:** Rhodey
**Participants:** Rocket, Shuri, Nebula
**Context:** Planning minecart tracks between dependent resources and boat/minecart movement

## Decisions

1. **No RCON-based entity movement.** Use Minecraft native physics — powered rails push minecarts, water flow + blue_ice propel boats. RCON `tp` commands at scale would consume the entire burst budget (40 cmd/s) and produce jittery movement. All three participants agreed.

2. **Fix existing bugs before adding movement features.** Four critical bugs must be resolved first:
   - Minecart spawn position is on `detector_rail` — must be on `powered_rail` (z+1) to receive initial push
   - `DisableRailsAsync` replaces powered rails with `air`, breaking the rail path — must use `minecraft:rail` instead
   - `GetCanalEntrance` returns incorrect coordinates (west of building, mid-Z) when canals are actually at `oz + StructureSize + 4` — ErrorBoatService spawns boats on dry land
   - Boats spawn on still water with no propulsion — need `{Motion}` NBT on summon or water flow via block placement

3. **Add powered rails before every bridge ramp.** Current "every 8 blocks" pattern doesn't account for 1-block elevation changes that kill momentum. Insert powered rails immediately before each ramp-up position.

4. **Entity lifecycle management required.** Neither MinecartRailService nor ErrorBoatService can detect if their spawned entities still exist. Add periodic entity existence checks via `/execute if entity` and respawn as needed. Budget: ~1 RCON query per connection per cycle.

5. **Rail path dedup for fan-in dependencies.** Multiple resources depending on the same parent (e.g., 5 services → postgres) create overlapping L-shaped rail paths. Add spatial reservation or offset strategy to prevent `setblock` overwriting other connections' rails.

6. **Accept movement as best-effort for testing.** Minecraft RCON provides no entity position queries. Movement verification is limited to: track placement correctness (testable), entity spawn (testable), entity existence (testable via `/execute if entity`), arrival at destination (not testable). This is acceptable for v1.

7. **ErrorBoatService must take CanalService dependency.** Currently spawns boats with no guarantee canals exist. Add `CanalService?` injection and gate spawning on `_canalsBuilt` readiness.

8. **Verify forceload coverage for canal transit routes.** Boats traversing from building canals through the N-S trunk to the lake cross multiple chunks. If those chunks aren't force-loaded, entities despawn mid-transit. Must audit forceload boundaries before movement work.

## Action Items
| Owner | Action |
|-------|--------|
| Rocket | Fix minecart spawn position to powered_rail at z+1 |
| Rocket | Fix `DisableRailsAsync` to replace powered rails with `minecraft:rail` (not air) |
| Rocket | Fix `GetCanalEntrance` to return actual canal coordinates (`oz + StructureSize + 4`) |
| Rocket | Add `{Motion}` NBT or water flow for boat propulsion in canals |
| Rocket | Add powered rails immediately before bridge ramp-up positions |
| Rocket | Implement rail path spatial reservation to prevent overlap on fan-in deps |
| Shuri | Add entity lifecycle tracking — periodic `/execute if entity` + respawn for minecarts and boats |
| Shuri | Add `CanalService?` dependency to `ErrorBoatService` with readiness gating |
| Shuri | Audit forceload chunk boundaries to confirm canal transit routes are covered |
| Nebula | Write unit tests for rail placement correctness (L-shaped routing, bridge ramps) |
| Nebula | Write tests for circular dependency rail dedup |
| Nebula | Write entity spawn verification tests using RCON mock |
| Nebula | Write integration test for fan-in rail overlap detection |

## Notes

### Risks
- **Entity accumulation across server restarts.** No entity tracking means orphaned minecarts/boats persist. `CleanupBoatsAsync` only targets near-lake entities. At 20+ resources, entity count grows unbounded. Entity lifecycle service should include startup cleanup sweep.
- **90° boat turns at canal junctions.** Still water won't redirect boats from E-W canals into the N-S trunk. Without flowing water or soul_sand bubble columns, boats pile up at junction walls. Rocket needs to prototype junction flow mechanics.
- **No `IRconService` interface.** `RconService` is `internal sealed` with no interface. Nebula flagged this as a test blocker — can't mock RCON for unit tests. This is pre-existing tech debt, not new to this feature, but becomes more painful with movement features. Defer to separate cleanup sprint.
- **Nullable injection pattern fragility.** `CanalService? canals = null` in MinecartRailService constructor works via DI ordering today but has no compile-time safety. Adding more cross-service nullable deps (ErrorBoatService → CanalService) compounds the risk. Consider introducing a `VillageInfrastructureReady` event or flag.

### Disagreements
- None. All participants aligned on using Minecraft native physics over RCON-driven movement. Agreed on fix-first-then-enhance sequencing.
