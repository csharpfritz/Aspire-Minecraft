### 2026-02-18: WorldSaveDirectory only supports bind mounts, not named volumes
**By:** Shuri
**What:** The `MinecraftAppFixture.WorldSaveDirectory` property only resolves bind mounts targeting `/data`. Named Docker volumes (from `WithPersistentWorld()`) are intentionally left unresolved — the property stays null.
**Why:** Named Docker volume host paths are platform-specific (WSL2 on Windows, `/var/lib/docker` on Linux) and require Docker CLI inspection with fragile path translation. Bind mounts give a clean, cross-platform host path. If MCA file testing is needed, configure a bind mount to `/data` in the AppHost. The AnvilTestHelper gracefully skips when WorldSaveDirectory is null, so no test failures occur.
