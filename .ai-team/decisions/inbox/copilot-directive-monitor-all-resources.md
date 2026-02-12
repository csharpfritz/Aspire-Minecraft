### 2026-02-12: User directive — MonitorAllResources convenience API
**By:** Jeffrey T. Fritz (via Copilot)
**What:** Add a `.MonitorAllResources()` extension method on the Minecraft server resource that automatically discovers and creates buildings for all non-Minecraft resources in the Aspire distributed application. Should exclude the Minecraft server itself and its related resources (worker, BlueMap, etc.) from monitoring.
**Why:** User request — reduces boilerplate in AppHost Program.cs. Instead of manually calling `.WithMonitoredResource()` for each resource, developers can call one method to monitor everything. Planned for next sprint alongside Famous Buildings feature.
