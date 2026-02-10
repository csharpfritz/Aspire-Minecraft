### Single NuGet Package Consolidation

**By:** Shuri
**What:** Consolidated three NuGet packages (Aspire.Hosting.Minecraft, Aspire.Hosting.Minecraft.Rcon, Aspire.Hosting.Minecraft.Worker) into a single package: `Aspire.Hosting.Minecraft`.

- **Rcon project:** Set `IsPackable=false`. Its assembly is embedded into the Hosting package via `PrivateAssets="All"` on the ProjectReference plus `TargetsForTfmSpecificBuildOutput` / `BuildOutputInPackage` to include the DLL and XML docs in the package's `lib/` folder. Rcon's transitive dependency (`Microsoft.Extensions.Logging.Abstractions`) is surfaced as a direct `PackageReference` in the Hosting project.
- **Worker project:** Set `IsPackable=false`. Kept as a separate project — it uses `Microsoft.NET.Sdk.Worker` and runs as a standalone process, so it cannot be merged into the hosting library.
- **Hosting project:** Remains the sole packable project. Updated README to document the embedded RCON client.
- **Test projects:** No changes needed — both test projects reference their respective source projects via `ProjectReference` and continue to build and pass (62 tests: 45 Rcon + 17 Hosting).

**Why:** Jeff directive — consumers should install one package, not three. The Rcon library is a pure implementation detail of the hosting integration. The Worker is a separate service that consumers reference as a project type parameter, not a library dependency.

**Verified:**
- `dotnet restore` ✅
- `dotnet build -c Release` ✅ (0 errors)
- `dotnet pack -c Release -o nupkgs` ✅ (single package: `Aspire.Hosting.Minecraft.0.1.0.nupkg`, 39.6 MB)
- `dotnet test` ✅ (62 tests pass)
- Package contents: Hosting DLL + Rcon DLL + XML docs + content files (bluemap, otel jar) + README

**Status:** ✅ Resolved
