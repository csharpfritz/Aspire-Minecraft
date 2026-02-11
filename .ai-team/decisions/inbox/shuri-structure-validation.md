### 2026-02-10: Structure Build Validation with Graceful Degradation

**By:** Shuri  
**What:** Added post-build validation to StructureBuilder that verifies door and window blocks were placed successfully after each structure builds.  
**Why:** RCON commands can fail silently or be rate-limited, leaving incomplete structures. Validation helps detect these failures and logs warnings for observability. Uses graceful degradation (log warnings, don't throw exceptions) to avoid blocking the entire village build process if individual blocks fail validation.

**Implementation:**
- `VerifyBlockAsync()` helper uses `testforblock` RCON command to check block type at coordinates
- Each structure type has a corresponding `Validate*Async()` method called after building
- Validates door air blocks and window blocks (glass_pane, stained_glass variants) at expected coordinates
- Returns false on any exception to handle RCON failures gracefully
