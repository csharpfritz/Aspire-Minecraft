# Decision: Azure Resource Group Integration — Epic Design

**By:** Rhodey  
**Date:** 2026-02-10  
**Scope:** New epic — Azure Resource Group → Minecraft integration  
**Document:** `docs/epics/azure-resource-group-integration.md`

## Decisions Made

1. **Separate NuGet package:** `Fritz.Aspire.Hosting.Minecraft.Azure` — isolates Azure SDK dependencies from consumers who don't need Azure.

2. **Azure monitor is a new resource discovery source, not a new rendering pipeline.** `AzureResourceMonitor` emits the same `ResourceInfo`/`ResourceStatusChange` records. All existing in-world services work unchanged.

3. **Polling for v1, Event Grid deferred.** 30-second default interval, configurable. Event Grid is Phase 5.

4. **Aspire-only for v1.** Standalone mode (no Aspire) is Phase 5 — uncertain demand doesn't justify the plumbing cost.

5. **`MaxResources = 50` default** with auto-exclude of infrastructure noise (NSGs, route tables, public IPs, etc.).

6. **`DefaultAzureCredential` as the default auth.** Users can override with a specific `TokenCredential`.

## Open Questions for Jeff

- Package naming: `Fritz.Aspire.Hosting.Minecraft.Azure` vs `Fritz.Azure.Minecraft`?
- Should mixed mode (Aspire + Azure resources in same world) be supported in v1?
- Default exclude list for infrastructure resource types — should we ship one?

## Team Impact

- **Shuri:** Owns Phases 1 and 3 (ARM client, auth, options, NuGet package scaffold)
- **Rocket:** Owns Phase 2 (Azure type → Minecraft structure mapping, provisioning state visuals)
- **Nebula:** Owns Phase 4 (mocked ARM client tests, options validation)
- **All:** Review the epic doc and push back on anything that doesn't feel right
