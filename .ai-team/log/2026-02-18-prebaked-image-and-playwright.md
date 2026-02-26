# Session: 2026-02-18 — Pre-baked Docker Image and Playwright Feasibility

**Requested by:** Jeffrey T. Fritz

## Summary

Three agents completed work on the pre-baked Docker image and BlueMap/Playwright testing feasibility.

## Decisions Made

1. **Wong** — Pre-baked Docker image (docker/Dockerfile)
   - 868 MB image, 33-second startup
   - All Minecraft server properties baked in (EULA, TYPE, MODE, flat world, RCON, spawn settings)
   - BlueMap plugin and core.conf included
   - RCON_PASSWORD and SEED intentionally excluded (security, project-specificity)
   - Users can `docker run` with only `-e RCON_PASSWORD=...` for turnkey experience
   - All ports verified working

2. **Shuri** — Hosting extension integration
   - `WithPrebakedImage()` extension added to hosting builder
   - `PrebakedImageAnnotation` attached to resource for detection
   - `WithBlueMap()` checks annotation (not env var) to decide whether to skip core.conf bind-mount
   - Annotation-based detection is synchronous and idiomatic (matches ModrinthPluginAnnotation pattern)

3. **Shuri** — World save binding
   - `MinecraftAppFixture.WorldSaveDirectory` only resolves bind mounts targeting `/data`
   - Named Docker volumes left unresolved (platform-specific host paths, fragile translation)
   - AnvilTestHelper gracefully skips when null — no test failures

4. **Rhodey** — BlueMap + Playwright testing feasibility assessment
   - RCON block assertions are the primary validation method (exact, deterministic, immediate)
   - BlueMap HTTP API provides secondary validation (tiles exist, JSON served)
   - Playwright **can** load BlueMap but visual regression is non-deterministic and fragile
   - **Recommendation:** Ship RCON/HTTP tests now (stable), defer Playwright screenshots to Sprint 6+
   - Visual WebGL rendering adds marginal confidence vs. high flakiness cost

## Outcomes

- Pre-baked Docker image ready for deployment
- Hosting extension fully integrated
- Test fixture properly handles bind mounts vs. named volumes
- Clear validation strategy for village structure: RCON (primary), HTTP (secondary), Playwright (optional polish, future)
