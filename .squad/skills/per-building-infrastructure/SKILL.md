---
name: "per-building-infrastructure"
description: "Pattern for attaching infrastructure (canals, beacons, paths) to each building in the village"
domain: "minecraft-world-building"
confidence: "medium"
source: "earned"
---

## Context

Each building in the Aspire village can have associated infrastructure: a canal along its back, a beacon on its right side, a path at its entrance. These per-building features must coordinate with the village layout (neighborhoods, spacing) and avoid colliding with each other.

## Patterns

### 1. Per-building canal segments (E-W)

Each building gets a canal segment running along its back (northern edge). The segment runs from the building's western edge to the trunk canal on the west side of town.

```csharp
int canalZ = buildingZ - 2;  // 2 blocks north of building
int canalStartX = buildingX; // building's western edge
int canalEndX = trunkX;      // trunk canal X position
```

### 2. Use building footprint for positioning

All per-building infrastructure is positioned relative to the building's origin (x, z) and `StructureSize`:
- **Back (north):** z - 2
- **Front (south/entrance):** z + StructureSize
- **Right (east):** x + StructureSize
- **Left (west):** x - 1

### 3. Trunk canal collects branches

A single N-S trunk canal on the west side of town collects all E-W branch canals. The trunk runs from the northernmost building row to the lake at the southern edge.

### 4. Respect building protection

Per-building infrastructure must use the BuildingProtectionService to clip fills around buildings. Even if Y ranges don't currently overlap, this is a safety net for future changes.

### 5. Initialization order

Infrastructure systems must run AFTER buildings are placed so protection zones are registered:
1. Buildings (StructureBuilder) — registers protection
2. Canals (CanalService) — uses protected fills
3. Rails (MinecartRailService) — reads CanalPositions for bridge detection

## Anti-Patterns

- **Placing infrastructure before buildings** — protection registry will be empty
- **Hardcoding positions** — always derive from building origin + StructureSize + layout constants
- **Ignoring neighborhood gaps** — ZoneGap=20 creates space between quadrants where infrastructure must bridge or terminate