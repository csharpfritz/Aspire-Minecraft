# Decision: Azure Resource Visualization Design

**By:** Rocket
**Date:** 2026-02-10
**Document:** `docs/epics/azure-minecraft-visuals.md`

## What

Designed the complete visual language for rendering Azure resources in Minecraft, separate from the existing Aspire village. Covers 15 Azure resource types, each mapped to a unique Minecraft structure with specific block palettes, dimensions, and health indicators.

## Key Decisions

1. **Azure district is visually distinct from Aspire village.** Prismarine/quartz/end stone palette (cool, grand) vs. wood/stone/cobblestone (warm, workshop). Players instantly know which district they're in.

2. **3-column tiered layout** grouping resources by functional tier (Gateway → Compute → Data → Infra → Monitoring), not alphabetically. Connected by prismarine roads.

3. **District starts at X=60** with a 20-block prismarine boulevard connecting to the Aspire village (X=10–40). Both districts share Y=-60 base level.

4. **Azure beacon color palette:** Compute=cyan, Data=blue, Networking=purple, Security=black, Messaging=orange, Observability=magenta. Keeps red=unhealthy, yellow=starting consistent with Aspire.

5. **Azure health states map to unique visuals:** Stopped=cobwebs, Deallocated=soul sand ring, Failed=netherrack fire on roof. Richer than Aspire's tri-state model.

6. **Scale strategy:** 3×5 grid for ≤15 resources. Multiple Z-offset planes for 50+. Progress boss bar during build.

## Impact on Other Agents

- **Shuri:** Azure resource discovery will need new env var patterns (Azure resource types, provisioning states, runtime states)
- **Rhodey:** New `AzureCitadelLayout` static class needed (like `VillageLayout`). New service interfaces for Azure-specific health states.
- **Nebula:** Test coverage for Azure structure building, layout calculations, health state mappings
- **Wong:** No CI/CD impact — design only
- **Mantis:** New demo script material — "The Pan" from village to citadel is the conference money shot

## Status

📐 Design complete — no implementation yet. This is a design document for the Azure Resource Group epic.
