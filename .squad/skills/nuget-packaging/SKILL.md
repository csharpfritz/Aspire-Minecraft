---
name: "nuget-packaging"
description: "NuGet packaging patterns for the Aspire.Hosting.Minecraft package"
domain: "dotnet-nuget"
confidence: "high"
source: "manual"
---

## Context
These patterns apply to the single NuGet package produced by this repo: `Fritz.Aspire.Hosting.Minecraft` (PackageId). The RCON library (`Aspire.Hosting.Minecraft.Rcon`) is embedded into this package. The Worker project is not packaged — it runs as a standalone service. They ensure packages are NuGet.org-ready with reproducible builds.

## Patterns

### Single Package Architecture
Only `Aspire.Hosting.Minecraft` is packable (`IsPackable=true`). The Rcon project is embedded via `PrivateAssets="All"` on the ProjectReference plus `TargetsForTfmSpecificBuildOutput` to include the DLL in the lib folder. The Worker project is `IsPackable=false` — it's a standalone service consumers reference directly.

### Never Use Floating Versions
All `PackageReference` entries must have exact pinned versions (e.g., `Version="13.1.0"`). Never use `Version="*"` or version ranges. NuGet.org rejects packages with floating version references and they break reproducible builds.

### Directory.Build.props Owns Shared Config
Shared metadata (Authors, License, RepositoryUrl) and build hardening properties live in `Directory.Build.props` at the repo root. Individual csproj files only add project-specific properties (PackageId, Version, Description, PackageTags, IsPackable).

### Required Hardening Properties
Every packable project inherits these from `Directory.Build.props`:
- `GenerateDocumentationFile` — ships XML docs in the nupkg
- `EnablePackageValidation` — catches breaking API changes between versions
- `Deterministic` — ensures byte-identical builds from same source
- `ContinuousIntegrationBuild` (CI-only) — embeds correct source paths for SourceLink
- `EmbedUntrackedSources` — ensures all source files are available in SourceLink
- `Microsoft.SourceLink.GitHub` — enables debugger source mapping to GitHub

### Package README
The single packable project (`Aspire.Hosting.Minecraft`) has its own `README.md` in the project directory, included in the nupkg via:
```xml
<None Include="README.md" Pack="true" PackagePath="\" />
```
The `PackageReadmeFile` property in `Directory.Build.props` is set to `README.md`. Non-packable projects do not pack READMEs.

### Pack and Verify Workflow
```bash
dotnet restore
dotnet build -c Release
dotnet pack -c Release -o nupkgs
```
Check for: zero warnings on src projects, correct package sizes, no floating versions in generated nuspec.

## Anti-Patterns
- **Floating `Version="*"`** — will be rejected by NuGet.org and breaks reproducibility
- **Multiple packages for a single integration** — consolidate into one package for simpler consumer experience
- **Missing `EnablePackageValidation`** — won't catch breaking API changes between releases
- **Missing SourceLink** — consumers can't step into source during debugging
- **Embedding Worker in the hosting package** — Worker uses `Microsoft.NET.Sdk.Worker` and runs as a separate process; it can't be a DLL inside the hosting package
