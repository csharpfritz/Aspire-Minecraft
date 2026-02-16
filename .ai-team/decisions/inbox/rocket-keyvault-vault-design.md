# Azure Key Vault Vault Interior Design

**Date:** 2025-01-28  
**Author:** Rocket (Integration Dev)  
**Status:** Implemented  

## Context
Azure Key Vault resources in the Grand Village layout needed differentiation from other Azure resources. While all Azure resources use the AzureThemed building exterior (15×15 light blue terracotta pavilion), Key Vault specifically should convey the concept of secure storage with a vault-themed interior.

## Decision
Modified `BuildGrandAzurePavilionAsync()` in `StructureBuilder.cs` to detect Azure Key Vault resources via `info.Type.Contains("keyvault")` and apply a specialized interior:

### Vault Interior Features
- **Dark vault floor:** Polished deepslate with iron trapdoor grating accents
- **Iron vault door:** Replaced standard air door with double iron doors (requiring buttons/levers to open)
- **Vault door frame:** Heavy iron block archway just inside entrance (3-block tall frame)
- **Security cages:** Two iron bars partitions (left and right walls) containing rows of locked chests
- **Sealed storage:** Barrel arrays along back wall for "sealed containers" aesthetic
- **Master key centerpiece:** Ender chest in the center of the room
- **Security floor details:** Heavy weighted pressure plates (gold) flanking the ender chest
- **Moody lighting:** Soul lanterns (dim blue glow) instead of bright lanterns

### Non-Key-Vault Azure Buildings
All other Azure resources (App Config, Service Bus, Storage, etc.) retain the standard cloud services aesthetic:
- Light blue carpet floor
- Brewing stand and cauldron (cloud metaphor)
- Bright lanterns

## RCON Budget
- **Standard Azure Pavilion:** ~34 base commands + 4 interior commands = ~38 total
- **Key Vault variant:** ~34 base commands + 25 vault interior commands = ~59 total
- **Constraint:** Stay under ~100 commands total (within burst budget)
- **Result:** ✅ Well within budget

## Implementation Details
- Exterior unchanged: light blue terracotta walls, quartz pilasters, banners, skylight
- Detection: `isKeyVault = info.Type.Contains("keyvault", StringComparison.OrdinalIgnoreCase)`
- Branching: If/else block after windows, before final floor/furniture
- Iron doors replace air blocks at entrance (double door with proper hinge configuration)

## Rationale
1. **User Experience:** Players immediately recognize Key Vault as "the vault" — visual metaphor matches function
2. **Consistency:** Exterior remains AzureThemed, preserving village cohesion
3. **Scalability:** Other Azure resources can get specialized interiors using same pattern
4. **Performance:** Vault variant stays well under RCON budget (~59 vs ~100 limit)

## Alternatives Considered
- **Separate building type:** Would break visual cohesion with other Azure resources
- **Exterior differentiation:** Would confuse the "all Azure resources look Azure" pattern
- **Lighter vault aesthetic:** Rejected — needs to feel "secure and heavy" to match Key Vault concept

## References
- `src/Aspire.Hosting.Minecraft.Worker/Services/StructureBuilder.cs` lines 1645-1761
- `IsAzureResource()` includes "keyvault" check (line 204)
- Minecraft blocks: `iron_block`, `iron_door`, `iron_bars`, `chest`, `barrel`, `ender_chest`, `soul_lantern`, `heavy_weighted_pressure_plate`, `polished_deepslate`
