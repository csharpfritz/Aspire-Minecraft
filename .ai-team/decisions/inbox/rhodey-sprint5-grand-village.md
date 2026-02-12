# Sprint 5 "Grand Village" — Architectural Decisions

> **By:** Rhodey (Lead)
> **Date:** 2026-02-12
> **Scope:** Sprint 5 architecture, layout changes, rail network, performance

---

### Decision 1: VillageLayout constants become mutable properties

**What:** `Spacing`, `StructureSize`, and `FenceClearance` change from `const int` to `static int { get; private set; }` with default values matching Sprint 4. A `ConfigureGrandLayout()` method sets them to Grand Village values.

**Why:** Preserves backward compatibility. Without `WithGrandVillage()`, the village is identical to Sprint 4. Avoids a hard fork of `VillageLayout` into two classes. All existing services use `VillageLayout.Spacing` etc. — they don't need code changes, just recompilation.

**Risk:** Mutable statics are a code smell. Mitigated by: `private set`, called once at startup, no thread contention (single-threaded init in `Program.cs`).

---

### Decision 2: Structure size is 15×15, not 11×11 or 21×21

**What:** All buildings expand to 15×15 footprint (13×13 usable interior).

**Why:** 11×11 (9×9 interior) is too small for meaningful multi-floor buildings with staircases — the spiral staircase alone needs 3×3, leaving only 6×6 per floor. 21×21 would be impressive but the RCON cost balloons (>200 commands for a watchtower), spacing goes to 32+ blocks, and the village exceeds world border with just 4 resources. 15×15 is the sweet spot — room for 3 floors with furniture, staircases fit, RCON stays under ~100 commands per building.

---

### Decision 3: Spacing is 24 blocks (15 + 9 gap)

**What:** `Spacing` increases from 12 to 24.

**Why:** Building is 15 blocks wide. Need 9 blocks between buildings for: 3-block walking path + 3-block rail corridor + 3-block walking path. This gives room for rails to run between buildings without clipping walls, and players can walk alongside rails.

**Trade-off:** Village Z-extent doubles per row. 8 resources = Z ~110 blocks. Requires `MAX_WORLD_SIZE` bump to 512.

---

### Decision 4: MAX_WORLD_SIZE bumps from 256 to 512

**What:** Default world border diameter doubles.

**Why:** At 24-block spacing, 8 resources need Z ~110 blocks. With fence clearance and margin, 256 blocks is too tight. 512 gives comfortable room for 20 resources. Memory impact is minimal (~10 MB additional for chunk data in a superflat world).

---

### Decision 5: Minecart rails coexist with redstone wires, not replace them

**What:** `WithMinecartRails()` is a separate feature from `WithRedstoneDependencyGraph()`. Both can be active simultaneously. Rails are offset by 1 block in X from redstone wires.

**Why:** Redstone wires have health-reactive behavior (break on unhealthy, restore on recovery) that's visually distinct and valuable. Rails add a second visual language — physical connection you can ride. Replacing redstone with rails loses the health-reactive visual. Coexistence gives users the choice.

---

### Decision 6: RCON burst mode for initial construction

**What:** `RconService` gets an `EnterBurstMode()` method that temporarily increases `MaxCommandsPerSecond` from 10 to 40 during initial village build.

**Why:** A 6-resource Grand Village with rails sends ~600 commands. At 10 cmd/sec = 60 seconds. At 40 cmd/sec = 15 seconds. The Minecraft server can handle 40 RCON commands/sec for short bursts — the tick budget is 50ms per tick, and simple `/setblock` + `/fill` commands typically complete in <1ms each. Steady-state (health updates) stays at 10 cmd/sec.

---

### Decision 7: Grand Village is opt-in via `WithGrandVillage()`

**What:** New feature is behind a feature flag, not a default behavior change.

**Why:** Breaking the default experience is unacceptable for existing users. The standard 7×7 village is fast to build, works within 256-block world border, and is conference-demo-proven. Grand Village is for users who want the immersive experience and are willing to accept longer build times and larger world requirements.

---
