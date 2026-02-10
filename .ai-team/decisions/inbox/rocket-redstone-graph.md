# Decision: Redstone Dependency Graph Implementation

**By:** Rocket
**Issue:** #36

**What:** Implemented `RedstoneDependencyService` as a `BackgroundService` that visualizes Aspire resource dependencies as redstone wire circuits. Uses L-shaped routing (X then Z), repeaters every 15 blocks, redstone lamps at entrances, and health-reactive circuit breaking/restoring.

**Key decisions:**
1. **BackgroundService pattern** — same as HeartbeatService. Needs its own timing (5s health check loop) independent of the main 10s worker loop. Waits 15s after resource discovery to let structures finish building before laying wire.
2. **L-shaped routing** — simple X-first-then-Z pathing avoids complex A* pathfinding. Works well with the 2×N grid layout since structures don't overlap paths.
3. **Circuit breaking strategy** — on unhealthy: remove redstone block (kills power) + break wire every 5th position (visual gaps). More dramatic than just removing power, and uses fewer commands than removing all wire.
4. **CommandPriority.Low for building** — bulk wire placement uses Low priority to avoid starving higher-priority health/display commands during initial build.
5. **Wire positions at BaseY, Z-1** — paths run in front of structures (Z offset -1 from structure origin) to stay visible and avoid overlap with structure footprints.

**Status:** ✅ Implemented. Build passes (0 errors, 0 warnings), 303 tests pass.
