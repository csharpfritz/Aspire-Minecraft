# Project Context

- **Owner:** Jeffrey T. Fritz (csharpfritz@users.noreply.github.com)
- **Project:** Aspire.Hosting.Minecraft — .NET Aspire integration for Minecraft servers
- **Stack:** C#, .NET 10, Docker, Aspire, OpenTelemetry, Minecraft Paper Server, RCON
- **Created:** 2026-02-10

## Key Facts

- Three NuGet packages: Aspire.Hosting.Minecraft (hosting lib), Aspire.Hosting.Minecraft.Rcon (RCON client), Aspire.Hosting.Minecraft.Worker (in-world display)
- All packages at version 0.1.0, packable via `dotnet pack -o nupkgs`
- Hosting lib depends on Aspire.Hosting and the Rcon project
- Worker depends on Microsoft.Extensions.Hosting, Http, and OpenTelemetry packages
- Rcon depends only on Microsoft.Extensions.Logging.Abstractions
- Content files: bluemap/core.conf and otel/opentelemetry-javaagent.jar bundled with hosting package
- Uses PackageReference with Version="*" for Aspire packages (floating versions)

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### NuGet Readiness Audit (2026-02-10)

- **Build status:** `dotnet build Aspire-Minecraft.slnx` succeeds cleanly (0 errors, 0 warnings on src projects).
- **Pack status:** `dotnet pack -o nupkgs` succeeds. Produces three `.nupkg` files. Two sample projects correctly emit "not packable" warnings.
- **Package sizes:** Hosting=~41 MB (due to 23 MB otel jar), Rcon=~20 KB, Worker=~28 KB.
- **Content files:** `bluemap/core.conf` and `otel/opentelemetry-javaagent.jar` are packed as both `content/` and `contentFiles/` entries — correct for consumer copy-to-output behavior.
- **Floating versions:** All three csproj files use `Version="*"` on PackageReference. Pack resolves these to concrete versions in the nuspec (e.g., Aspire.Hosting → 13.1.0). This is fine for local dev but is fragile for reproducible builds and should be pinned before publishing.
- **Directory.Build.props** (`Directory.Build.props`): Sets shared metadata — Authors, License (MIT), PackageProjectUrl, RepositoryUrl, RepositoryType, PackageReadmeFile. Conditionally includes repo-root README.md in all packages.
- **Missing from all csproj/props:** `GenerateDocumentationFile`, `EnablePackageValidation`, `SourceLink`, `Deterministic`, `ContinuousIntegrationBuild`, `EmbedUntrackedSources`, `PackageIcon`, `PackageReleaseNotes`.
- **README:** All three packages share the repo-level `README.md`. No per-package README.
- **CI/CD:** No `.github/workflows/` files exist. Only `.github/agents/squad.agent.md`.
- **XML docs:** Present on public APIs in all three projects (confirmed by grep). But `GenerateDocumentationFile` is not enabled, so no XML doc file is shipped in the nupkg.
- **No icon:** `img/sample-1.png` exists but no `PackageIcon` property is set. NuGet.org will show a default icon.

📌 Team update (2026-02-10): 18 Minecraft interaction features proposed across 3 tiers — decided by Rocket
📌 Team update (2026-02-10): 3-sprint roadmap adopted — Sprint 1 assigns Shuri: pin deps, NuGet hardening, extract otel jar, verify pack — decided by Rhodey

### Sprint 1 NuGet Hardening (2026-02-10)

- **Pinned all floating `Version="*"` dependencies** to exact resolved versions:
  - `Aspire.Hosting` → `13.1.0` (in Aspire.Hosting.Minecraft.csproj)
  - `Microsoft.Extensions.Logging.Abstractions` → `10.0.2` (in Aspire.Hosting.Minecraft.Rcon.csproj)
  - `Microsoft.Extensions.Hosting` → `10.0.2` (in Aspire.Hosting.Minecraft.Worker.csproj)
  - `Microsoft.Extensions.Http` → `10.0.2` (in Aspire.Hosting.Minecraft.Worker.csproj)
  - `OpenTelemetry.Extensions.Hosting` → `1.15.0` (in Aspire.Hosting.Minecraft.Worker.csproj)
  - `OpenTelemetry.Exporter.OpenTelemetryProtocol` → `1.15.0` (in Aspire.Hosting.Minecraft.Worker.csproj)
- **Added NuGet hardening properties** to `Directory.Build.props`: `GenerateDocumentationFile`, `EnablePackageValidation`, `Deterministic`, `ContinuousIntegrationBuild` (CI-only), `EmbedUntrackedSources`.
- **Added SourceLink** via `Microsoft.SourceLink.GitHub` Version `8.*` with `PrivateAssets="All"`.
- **OpenTelemetry Java agent (23 MB):** Chose Option B — kept embedded, added a TODO comment in the csproj for Sprint 2 runtime download. Rationale: runtime download introduces container networking assumptions and itzg image init-system complexity. Ship the simple thing first.
- **Created per-package README.md files** for all three packages; removed the shared repo-root README from `Directory.Build.props` conditional include. Each csproj now packs its own local README.
- **Pack output verified clean:** 0 errors, 0 warnings on src projects. Only warnings are from sample projects ("not packable") — expected.
  - `Aspire.Hosting.Minecraft.0.1.0.nupkg` — 39.6 MB (otel jar dominates)
  - `Aspire.Hosting.Minecraft.Rcon.0.1.0.nupkg` — 20.8 KB
  - `Aspire.Hosting.Minecraft.Worker.0.1.0.nupkg` — 27.5 KB
- **No floating versions remain** in any nuspec — confirmed by inspecting the packed nuspec inside the nupkg.

📌 Team update (2026-02-10): CI/CD pipeline created — build.yml + release.yml now build/test/publish your packages — decided by Wong
📌 Team update (2026-02-10): Test infrastructure created — InternalsVisibleTo added to both source projects, 62 tests passing — decided by Nebula

### Single Package Consolidation (2026-02-10)

- **Consolidated three NuGet packages into one:** Only `Aspire.Hosting.Minecraft` is now packable. Rcon and Worker projects set to `IsPackable=false`.
- **Rcon embedding approach:** Used `PrivateAssets="All"` on the ProjectReference to prevent Rcon from appearing as a nuspec dependency, combined with `TargetsForTfmSpecificBuildOutput` and `BuildOutputInPackage` MSBuild items to physically include `Aspire.Hosting.Minecraft.Rcon.dll` and its XML docs in the package's `lib/` folder.
- **Rcon transitive dependency:** Added `Microsoft.Extensions.Logging.Abstractions` as a direct `PackageReference` in the Hosting project since Rcon's dependency is no longer surfaced via a separate package.
- **Worker kept separate:** Worker uses `Microsoft.NET.Sdk.Worker` and runs as a standalone process — cannot be a DLL inside the hosting package. Consumers reference it as a `ProjectReference` via the `WithAspireWorldDisplay<TWorkerProject>()` generic type parameter.
- **Test projects unchanged:** Both test projects still reference their source projects directly. All 62 tests (45 Rcon + 17 Hosting) pass.
- **Pack output:** Single `Aspire.Hosting.Minecraft.0.1.0.nupkg` (39.6 MB). Contains both DLLs, XML docs, content files (bluemap, otel jar), and README.
