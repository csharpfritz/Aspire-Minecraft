# 2026-02-27: OTel Boat Positioning Issues

**Requested by:** Jeffrey T. Fritz

## Summary

Jeff triggered an error via the Aspire dashboard and the creeper boat spawned but was underwater and facing the wrong direction. Two bugs identified:

### Issues Found

1. **GetCanalEntrance returns wrong Y coordinate**: Returns `CanalY` (inside water, y=62) — needs `CanalY + 1` (on water surface, y=63) so boats spawn at surface level, not underwater.

2. **Summon command lacks Rotation NBT**: The boat spawn command was missing `Rotation:[270f,0f]` to face west. Without rotation, boats spawn facing default direction (east) instead of west toward the trunk canal.

## Prior Work This Session

- **Commit 5321057**: Added `ASPIRE_RESOURCE_{NAME}_HEALTH_PATH` environment variable support in Rocket's health check path implementation
- **Commit 3b683cb**: OTel error boat webhook pipeline — allows error logs to trigger boat spawns from Aspire dashboard

## Rocket Action Items

Spawned a rocket to fix both boat positioning issues:
1. Fix `GetCanalEntrance()` to return surface-level coordinates (`CanalY + 1`)
2. Add `Rotation:[270f,0f]` to the boat summon command

## Context

Related decisions:
- **2026-02-27: Aspire dashboard "Trigger Error" command** — OTel error logs now trigger error boats
- **2026-02-27: Health Check Path Support** — Framework-level health path support ensures reliable error detection
