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

📌 Sprint 1 test infrastructure (2026-02-10): Created two xUnit test projects under tests/ — Aspire.Hosting.Minecraft.Rcon.Tests and Aspire.Hosting.Minecraft.Tests. 62 tests total, all passing.
📌 InternalsVisibleTo added to both source projects for test access to internal types (RconPacket, endpoint name constants, WorkerBuilder, ParseConnectionString).
📌 Changed MinecraftHealthCheck.ParseConnectionString from private to internal to enable direct unit testing of connection string parsing logic.
📌 RconResponseParser is fully testable — all methods are public static, pure functions. Best coverage target in the codebase.
📌 RconClient requires a mock TCP server for protocol-level testing. Created a pattern using TcpListener on loopback port 0 with manual RCON packet read/write.
📌 RconConnection is hard to unit test in isolation — it creates RconClient internally with no DI seam. Integration tests with a real server needed for reconnection/backoff logic.
📌 FluentAssertions v8 (resolved as 8.8.0) has commercial licensing (Xceed). Team should evaluate switching to a free alternative for open-source compatibility.
📌 MinecraftServerBuilderExtensions.AddMinecraftServer needs full Aspire DI (DistributedApplication.CreateBuilder) for integration-level testing — deferred to Sprint 2.

📌 Team update (2026-02-10): NuGet hardening completed — source projects now have pinned deps and SourceLink — decided by Shuri
📌 Team update (2026-02-10): CI/CD pipeline created — tests will run in ubuntu+windows matrix via build.yml — decided by Wong
