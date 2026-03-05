# Session: 2026-02-17 — Village Redesign Implementation

**Requested by:** Jeffrey T. Fritz

## Team Participants

- **Shuri** — VillageLayout spacing expansion
- **Wong** — Custom Docker image with BlueMap plugin
- **Rocket** — CanalService, ErrorBoatService, bridge detection
- **Scribe** — Decision merge and logging
- **Coordinator** — Integration fixes

## Work Completed

### Phase 1: VillageLayout Spacing & Canal Infrastructure
**Owner:** Shuri

- Expanded Grand layout spacing from 24 → 36 blocks
- Added canal dimensions and methods:
  - `CanalWidth = 5` (blocks), `CanalDepth = 2`
  - Lake constants: `LakeWidth = 20`, `LakeDepth = 12`, `LakeBlockDepth = 3`
  - `GetCanalEntrance(int index)` — positions canals on building east side (X + StructureSize + 2)
  - `GetLakePosition(int resourceCount)` — centers lake on village X-axis with 20-block gap from last row
- Updated `FenceClearance` to 10 (same as standard layout)
- Bumped `MAX_WORLD_SIZE` from 512 → 768
- Status: ✅ Implemented, all tests green

### Phase 2: Custom Docker Image
**Owner:** Wong

- Created `docker/Dockerfile` extending `itzg/minecraft-server:latest`
- Pre-installs BlueMap via `MODRINTH_PROJECTS="bluemap"`
- Marker env var: `ASPIRE_MINECRAFT_PREBAKED=true` for hosting extension detection
- Created `.github/workflows/docker.yml` CI/CD pipeline:
  - Triggers: push to main (docker/** changes), manual dispatch, GitHub releases
  - Tags: `latest`, git SHA, version number
  - Registry: GHCR (`ghcr.io/csharpfritz/aspire-minecraft-server`)
  - Registry-backed cache for faster builds
- Created `docker/README.md` documentation
- Status: ✅ Complete, YAML validated, workflow tested

### Phase 3: CanalService
**Owner:** Rocket

- Created `CanalService.cs` — builds water channels from each building to trunk canal
- Canal routing: straight Z-axis from each building toward shared lake
- Lake construction at village Z-max with proper water source block placement
- Supports proper water flow for boat navigation
- Status: ✅ Implemented

### Phase 4: ErrorBoatService
**Owner:** Rocket

- Created `ErrorBoatService.cs` — spawns boats with creeper passengers on unhealthy transitions
- Anti-pileup system with despawn lifecycle management
- Rate limiting to prevent entity overflow
- Integrated into `Program.cs` worker lifecycle
- Status: ✅ Implemented and integrated

### Phase 5: Track/Canal Bridge Detection
**Owner:** Rocket

- Updated `MinecartRailService` with stone brick bridge support
- Bridge segments detect canal crossings automatically
- Rails cross on bridge platform; water flows underneath
- Status: ✅ Implemented

### Test Updates
**Owner:** Nebula

- Updated `WithAllFeatures` test expectations: 19 → 21 feature count
- All tests passing with new feature integration
- Status: ✅ Complete

## Commits

7 commits on `village-redesign` branch covering all phases and integration.

## Outcomes

- Complete village redesign architecture implemented and tested
- Village now physically represents Aspire system: minecarts on tracks carry dependencies, boats on water carry errors
- Infrastructure ready for user testing and refinement
