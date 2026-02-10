# Project Context

- **Owner:** Jeffrey T. Fritz (csharpfritz@users.noreply.github.com)
- **Project:** Aspire.Hosting.Minecraft — .NET Aspire integration for Minecraft servers
- **Stack:** C#, .NET 10, Docker, Aspire, OpenTelemetry, Minecraft Paper Server, RCON
- **Created:** 2026-02-10

## Key Facts

- Three NuGet packages, all at version 0.1.0
- No test projects exist yet — test infrastructure needs to be created
- RCON protocol has complex edge cases (reconnection, response parsing, timeouts)
- Worker service polls metrics and manages in-world state
- Health checks use MinecraftHealthCheck.cs

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

📌 Team update (2026-02-10): NuGet readiness audit completed — pack output needs smoke testing — decided by Shuri
📌 Team update (2026-02-10): 18 features proposed — Nebula will test all Rocket features per sprint — decided by Rocket
📌 Team update (2026-02-10): 3-sprint roadmap adopted — Sprint 1 assigns Nebula: test project structure, RCON unit tests, health check tests, pack smoke test — decided by Rhodey
