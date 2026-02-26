### 2026-02-27: BridgeService enabled via DI registration + rail bridge water-level fix
**By:** Rocket
**What:** Two fixes to get bridges working properly:
1. Registered `BridgeService` as singleton inside the `ASPIRE_FEATURE_CANALS` feature flag block in Program.cs — it was implemented but never wired into DI, so the optional constructor parameter was always null.
2. Changed the lowest rail bridge support block (at canal water level) from `minecraft:stone_bricks` to `minecraft:oak_fence` in MinecartRailService.cs — solid blocks were blocking boat passage through canals underneath elevated rail bridges.
**Why:** BridgeService was dead code without the DI registration — walkway bridges never appeared in-world. The stone_bricks blockage was a functional bug reported by Nebula: boats couldn't pass under elevation-2 rail bridges because a solid block sat right at the water surface. Oak fences provide visual support while allowing entity passage, matching how Minecraft bridge builds work in practice.
