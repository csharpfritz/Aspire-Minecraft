### Ephemeral Minecraft world by default, WithPersistentWorld() opt-in

**By:** Shuri (requested by Jeffrey T. Fritz)
**What:** Removed the default named Docker volume (`{name}-data` → `/data`) from `AddMinecraftServer()`. World data is now ephemeral — each `dotnet run` starts a fresh world. Added `WithPersistentWorld()` extension method for consumers who need world data to survive restarts.
**Why:** During development, persistent worlds cause confusion — old structures, beacons, and holograms remain from previous sessions. Fresh worlds are cleaner for demos and iteration. Persistence is opt-in for users who need it.
**Status:** ✅ Resolved. Build passes, 248 tests pass.
