### 2026-02-10: User feature idea — Redstone Dependency Graph + Service Switches
**By:** Jeffrey T. Fritz (via Copilot)

**What:** Design a feature that:
1. **Redstone Wires = Resource Dependencies** — Visualize the connections between Aspire resources (databases, APIs, workers, etc.) using redstone wire circuits in the Minecraft world. Each resource has a structure/building, and redstone lines connect them to show the dependency graph.
2. **Lever Switches = Service Control** — Place Minecraft levers/switches on each service's structure so the player can physically toggle services on/off from within Minecraft. Flipping a lever would start or stop the corresponding Aspire resource.

**Why:** This turns the Minecraft world into an interactive operations dashboard. Instead of just visualizing health, the player can actually *control* the distributed system from inside the game. It's the ultimate "infrastructure as a game" experience.

**Technical considerations:**
- Redstone wires have a max range of 15 blocks — may need repeaters for distant services
- Lever state changes can be detected via RCON world interaction or plugin events
- Need to model the DAG (directed acyclic graph) of Aspire resource dependencies
- Starting/stopping services maps to Aspire's resource lifecycle (IResourceWithConnectionString, etc.)
- Should respect dependency ordering — stopping a database should warn about dependent services
- Could use redstone signal strength to indicate health/load

**Sprint target:** Sprint 3 (Showstopper) — this is a flagship feature
