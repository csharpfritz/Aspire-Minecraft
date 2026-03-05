# Session: 2026-02-17 — Village Redesign Fixes

**Requested by:** Jeffrey T. Fritz

## Issues Reported

- Invisible wall blocking part of town
- Canals not created
- No minecart tracks visible
- Redstone between buildings unwanted

## Fixes Applied (Shuri)

- MAX_WORLD_SIZE: 768 → 29999984
- WorldBorder: 200 → 2000/1000
- VIEW_DISTANCE: 6 → 12
- SIMULATION_DISTANCE: 4 → 8
- Enabled `.WithCanals()` in sample app
- Enabled `.WithErrorBoats()` in sample app
- Removed `.WithRedstoneDependencyGraph()` from sample and `WithAllFeatures()`
- Test count updated: 21 → 20
- Added TestResults/ to .gitignore

## Status

Complete. All fixes implemented.
