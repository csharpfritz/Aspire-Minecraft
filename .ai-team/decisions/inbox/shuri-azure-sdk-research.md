# Azure SDK Research — Separate Package Recommendation

**By:** Shuri  
**Date:** 2026-02-10  
**Type:** Architecture recommendation

## What

Research completed on Azure SDK integration for visualizing Azure Resource Group resources in Minecraft. Key finding: **Azure monitoring should ship as a separate NuGet package** (`Fritz.Aspire.Hosting.Minecraft.Azure`), not bundled with the core `Fritz.Aspire.Hosting.Minecraft` package.

## Why

1. **Dependency weight:** Azure SDK adds ~5 MB of dependencies (`Azure.ResourceManager`, `Azure.Identity` + MSAL, `Azure.ResourceManager.ResourceHealth`, `Azure.Monitor.Query.Metrics`) that most users don't need.
2. **Authentication complexity:** Azure.Identity brings MSAL, which adds OAuth2/OIDC machinery and potential version conflicts with other Azure-using Aspire components.
3. **Opt-in feature:** Not all Aspire users have Azure resources to monitor. The core Minecraft visualization works fine without Azure.
4. **Industry pattern:** Microsoft's own Aspire components use separate packages per cloud integration (`Aspire.Hosting.Azure.*`).

## For v1

- **Polling approach** (not Event Grid) — matches existing `AspireResourceMonitor` timer pattern, no extra Azure infrastructure needed.
- **Layered health:** Provisioning state (free) + Resource Health API (for resources that support it).
- **DefaultAzureCredential** for auth — works locally with `az login`, in production with managed identity.

## Impact

- Rhodey: Architecture decision on package split before implementation starts
- Rocket: Azure resource type → Minecraft visualization mapping (new beacon colors, structures)
- Nebula: Test strategy for Azure integration (may need mock ARM client)
- Wong: CI/CD for second NuGet package

## Reference

Full research document: `docs/epics/azure-sdk-research.md`
