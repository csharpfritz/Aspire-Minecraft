# Session: 2026-03-05 — Milestone: TNT Minecarts, Bridge Collision, Squad NPCs

**Date:** 2026-03-05  
**Agents:** Rocket, Shuri, Nebula (3 spawns total + final Rocket)  
**Status:** ✅ COMPLETE  

---

## Summary

Four parallel squad member spawns completed a major milestone:

1. **Rocket** — Replaced error minecart creepers with TNT minecarts (Fuse:-1), restored simple peaceful mode, added collision-avoidance helpers
2. **Shuri** — Integrated `.squad/team.md` detection into Aspire hosting via `WithSquadVillagers()` extension
3. **Nebula** — Wrote 50 proactive milestone tests, updated 7 tests for TNT minecart behavior
4. **Rocket** — Implemented `SquadVillagerService` to spawn named agent villagers in-world

---

## Key Outcomes

### Error Visualization Refactored
- **Problem:** Peaceful mode despawned summoned creepers in error minecarts, breaking error visualization
- **Solution:** Switched to TNT minecarts (single entity, non-exploding via Fuse:-1)
- **Impact:** Error boats now persist in true peaceful mode; simplified startup sequence by 1 command

### Bridge Collision Avoidance Added
- **Helper Method:** `VillageLayout.GetFruitStandBounds()` shared across BridgeService and SquadVillagerService
- **Strategy:** Prevents fruit stand area (6-block shift applied) from being overwritten
- **Test Coverage:** Collision tests now paired with actual implementation

### Squad NPC Villagers Feature Complete
- **Hosting Side:** Shuri's `WithSquadVillagers()` parses team roster, injects `ASPIRE_SQUAD_AGENTS`
- **Worker Side:** Rocket's `SquadVillagerService` spawns named villagers distributed around village
- **Safety:** Invulnerable, tagged for cleanup, gracefully no-op when env var absent
- **Integration:** Included in `WithAllFeatures()` chain for seamless adoption

### Test Coverage Milestone
- **Tests Written:** 50 new proactive test cases covering all milestone features
- **Tests Updated:** 7 existing tests rewired for TNT minecart assertions
- **Total Passing:** 640 tests ✅

---

## Decisions Made

| Decision | Owner | Rationale |
|----------|-------|-----------|
| TNT minecarts over creepers | Rocket | Single entity, no despawning in peaceful, clearer error signal |
| Peaceful mode restored | Rocket | No longer need `difficulty easy` workaround; TNT entities persist |
| Column-index parsing (no regex) | Shuri | Robust to `.squad/team.md` column reordering |
| Conditional SquadVillagerService registration | Rocket | No allocation overhead if squad env var absent |
| Proactive test contracts + rewiring | Nebula | Executable specs enable test-first; swap to real code when ready |
| Invulnerable squad villagers | Rocket | Prevents accidental player-caused despawn; enforces living lore |

---

## Files Changed

### New Files
- `.squad/orchestration-log/2026-03-05T{1,2,3,4}-*.md` (4 spawn logs)
- `src/Aspire.Hosting.Minecraft.Worker/Services/SquadVillagerService.cs`
- `src/Aspire.Hosting.Minecraft/Extensions/WithSquadVillagers.cs`

### Modified Files
- `src/Aspire.Hosting.Minecraft.Worker/Services/ErrorBoatService.cs` — TNT minecart spawn
- `src/Aspire.Hosting.Minecraft.Worker/Services/VillageLayout.cs` — GetFruitStandBounds()
- `src/Aspire.Hosting.Minecraft.Worker/Program.cs` — peaceful mode simplified, SquadVillagerService registration
- `tests/Aspire.Hosting.Minecraft.Tests/Services/ErrorBoatServiceTests.cs` — 7 tests updated

---

## Cross-Agent Impact

- **Rocket → Nebula:** TNT minecart change triggered 7 test rewires
- **Shuri → Rocket:** ASPIRE_SQUAD_AGENTS env var consumed by SquadVillagerService
- **Rocket → Rocket:** VillageLayout bounds shared across two services
- **Nebula → Team:** Proactive test contracts establish expectations for future parallel work

---

## Next Steps (Post-Milestone)

- [ ] Merge `.squad/decisions/inbox/` into `decisions.md`
- [ ] Verify all 640 tests passing in CI
- [ ] Consider additional squad agent role/profession assignments (e.g., librarian for Shuri)
- [ ] Document squad NPC feature in README for end users

---

**Test Status:** ✅ 640 passing  
**Build Status:** ✅ Clean  
**Feature Parity:** ✅ Squad NPCs complete (test-verified)
