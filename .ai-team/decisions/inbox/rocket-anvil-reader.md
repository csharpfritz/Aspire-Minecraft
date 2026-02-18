# Decision: AnvilRegionReader lives in integration test project

**Date:** 2026-02-19
**By:** Rocket
**Issue:** #93

## Context

We needed an MCA/Anvil region file reader to verify Minecraft block placement in integration tests. The prior NBT library evaluation (fNbt) was approved, and this implements the region file parsing layer on top of it.

## Decision

- `AnvilRegionReader` class placed in `tests/Aspire.Hosting.Minecraft.Integration.Tests/Helpers/`
- Uses fNbt 1.0.0 (BSD-3-Clause) for NBT parsing after manual zlib decompression
- Handles full 1.18+ format: negative Y (-64 to 319), palette-based block storage, packed long arrays
- Returns `BlockState` records with block name + properties dictionary

## Rationale

- This is test infrastructure, not production code — it belongs in the test project
- If we ever need MCA reading in production (e.g., world analysis features), we'd extract it to a shared library
- fNbt is NBT-only, so the ~270 lines of MCA binary I/O is ours to maintain

## Impact

- Unblocks #94 (WorldSaveDirectory fixture) and future block verification tests
- Tests can now assert actual block state from saved world files, not just RCON responses
- Nebula can write tests that read world saves to verify StructureBuilder output
