### Feature env var checks now require exact `"true"` value

**By:** Shuri
**What:** All `ASPIRE_FEATURE_*` env var checks in `Program.cs` changed from `!string.IsNullOrEmpty(...)` to `== "true"`. This affects 16 feature registrations (15 service DI registrations + 1 peaceful mode check in `ExecuteAsync`). The `With*()` extension methods in `MinecraftServerBuilderExtensions.cs` already set all feature env vars to `"true"`, so no changes were needed on the hosting side.
**Why:** Prevents accidental feature activation from empty strings, whitespace, or junk values in environment variables. Only an explicit `"true"` value activates a feature now.
**Impact:** Any code that sets `ASPIRE_FEATURE_*` env vars must use the exact string `"true"` (lowercase). Other truthy values like `"1"`, `"yes"`, or `"True"` will NOT activate features.
**Status:** ✅ Implemented on sprint-4 branch.
