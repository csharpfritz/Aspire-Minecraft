# Session: Boat Teleport & Stair Fix

**Date:** 2026-03-01  
**Requested by:** Jeffrey T. Fritz

## Summary

Jeff tested again — boats appear correctly on water but don't move. Motion NBT is overridden by Minecraft water physics every tick, making Motion-based movement a no-op. Creeper presence uncertain. Tower staircase also has stairwell hole issues — gaps too large or missing.

## Fix Approach

1. **Boat Motion Fix:** Replace Motion NBT with periodic teleportation via execute/tp RCON commands
2. **Tower Stairwell:** Reduce stairwell holes to top-3-steps only

## Outcomes

- Rocket spawned for boat teleportation fix
- Rocket spawned for tower stairwell hole reduction
- Both fixes targeted for implementation
