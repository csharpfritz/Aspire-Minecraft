# Session: 2026-02-27 — Boat & Creeper Motion Issues

**Requested by:** Jeffrey T. Fritz  
**Date:** 2026-02-27

## Summary

Jeff confirmed boats appear in canals after the positioning fix. Two remaining issues identified:

### Issue (a): Creeper Passenger Not Visible
- **Problem:** Creeper passenger doesn't render when riding in the error boat
- **Root Cause:** Likely Paper server NBT `Passengers` tag issue — Minecraft/Paper may not properly serialize or deserialize passenger data
- **Status:** Awaiting investigation

### Issue (b): Boat Doesn't Float Downstream
- **Problem:** Water friction stops boat's Motion quickly, preventing downstream travel
- **Solution:** Needs higher Motion values to maintain velocity against friction
- **Status:** Rocket spawned to apply `/ride` command for creeper + increase Motion values

## Decision

Rocket to address both issues using:
1. `/ride` command for creeper passenger visibility
2. Higher Motion values for sustained boat propulsion downstream

## Outcomes

- Boats confirmed functional and positioned correctly in canals
- Path forward identified for both remaining blockers
