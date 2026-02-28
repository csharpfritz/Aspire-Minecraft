# Session: Atomic Boat Spawn Fix

**Date:** 2026-02-28  
**Requested by:** Jeffrey T. Fritz

## Summary

Jeff tested error boats and discovered three critical issues:

1. **No creeper in boat** — Multi-command RCON timing causes summon+ride to fail (summon executes before ride completes)
2. **Boat doesn't move** — Data merge attempts to set Motion on boats, but water physics override the NBT value
3. **API stuck offline** — trigger-error sets `isHealthy=false` permanently, API never recovers

## Fix Plan

- Revert to single atomic summon command with Passengers and Motion embedded in NBT
- Remove `isHealthy=false` from trigger-error handler
- Combine all spawn logic into one command to eliminate race conditions

## Status

Rocket spawned to implement all three fixes.
