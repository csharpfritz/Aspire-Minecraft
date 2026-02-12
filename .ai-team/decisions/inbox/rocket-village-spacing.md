### Village Spacing increased from 10 to 12

**By:** Rocket
**What:** Changed `VillageLayout.Spacing` from 10 to 12, giving a 5-block walking gap between 7×7 structures (was 3 blocks).
**Why:** Buildings were too close together — a 3-block gap between structures made the village feel cramped and hard to navigate. 5 blocks is comfortable for player walking and allows room for doors, switches, and decorative elements without collision. DashboardX and fence perimeter calculations are unaffected (both derive positions dynamically).
**Status:** ✅ Resolved. All 382 tests updated and passing.
