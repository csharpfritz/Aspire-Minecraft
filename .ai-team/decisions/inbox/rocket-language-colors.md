### Language-Based Color Coding for Village Buildings

**By:** Rocket
**Date:** 2026-02-12
**Requested by:** Jeffrey T. Fritz

**What:** Village buildings now use language/technology-specific colors for wool trim and banners instead of a uniform blue. Color mapping: .NET Project → purple, JavaScript/Node → yellow, Python → blue, Go → cyan, Java → orange, Rust → brown, Unknown → white.

**Why:** With multiple projects in a village, all-blue buildings made it impossible to distinguish technology types at a glance. Color coding gives immediate visual feedback about what language/framework each resource uses. Purple for .NET aligns with the .NET brand color. The mapping is based on `resourceType` (all Aspire-hosted projects are .NET → "Project" type → purple) and falls back to name/type substring matching for containers running Node, Python, Go, Java, or Rust workloads.

**Scope:**
- Watchtower (Project): wool trim at y+8 + wall banner on flagpole — both use language color
- Cottage (default/unknown): wool trim at y+4 — uses language color
- Warehouse, Workshop: no wool trim, unchanged
- Cylinder, AzureThemed: own identity materials, unchanged

**Implementation:** `GetLanguageColor(string resourceType, string resourceName)` returns `(wool, banner, wallBanner)` block ID tuple. `BuildWatchtowerAsync` and `BuildCottageAsync` now accept `ResourceInfo` to pass type/name. The method is `internal static` for testability.

**Also fixed:** Watchtower and Azure banner placement — banners were floating standing banners disconnected from the flagpole. Now uses `wall_banner[facing=south]` attached to an extended flagpole.

**Status:** ✅ Implemented. All 382 tests pass.
