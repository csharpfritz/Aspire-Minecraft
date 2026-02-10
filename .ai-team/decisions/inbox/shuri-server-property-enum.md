### ServerProperty Enum & File-Based server.properties Loading

**By:** Shuri
**What:** Added `ServerProperty` enum (24 members), `MinecraftGameMode` enum (4 members), `MinecraftDifficulty` enum (4 members), corresponding `WithServerProperty`/`WithGameMode`/`WithDifficulty` overloads, and `WithServerPropertiesFile()` for bulk property loading from disk.
**Why:** Users previously had to look up `server.properties` key names and pass raw strings. The enum gives IntelliSense discovery of all common properties. The typed `MinecraftGameMode` and `MinecraftDifficulty` enums prevent typos. File-based loading lets users maintain a standard `server.properties` file in their AppHost project and apply it in one call.
**Design choices:**
- Chose `enum` over `static class with string constants` — enums provide better IntelliSense grouping and switch exhaustiveness.
- PascalCase enum members convert to UPPER_SNAKE_CASE internally (e.g., `MaxPlayers` → `MAX_PLAYERS`). Users never see the env var name.
- `WithServerPropertiesFile` reads at build/configuration time, not runtime. Values become env vars on the container. Last-write-wins — subsequent `WithServerProperty()` calls override file values.
- File parsing handles `#` comments, blank lines, and `=` in values (splits on first `=` only).
**Status:** ✅ Resolved.
