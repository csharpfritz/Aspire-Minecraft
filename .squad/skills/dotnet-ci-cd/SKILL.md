# SKILL: .NET NuGet CI/CD Pipeline

> Reusable pattern for .NET projects that produce NuGet packages.

## CI Workflow Pattern (build.yml)

**Triggers:** `push` to main + `pull_request` to main.

**Key steps (in order):**
1. `dotnet restore <solution>`
2. `dotnet build <solution> -c Release --no-restore`
3. `dotnet test --no-build -c Release --verbosity normal`
4. `dotnet pack -c Release --no-build -o nupkgs`
5. Upload `nupkgs/*.nupkg` as artifact

**Best practices:**
- Use concurrency groups (`build-${{ github.ref }}` + `cancel-in-progress: true`) to kill stale runs.
- Matrix on `ubuntu-latest` + `windows-latest` for cross-platform verification.
- Only upload artifacts from one OS leg to avoid name collisions.
- Always pass `--no-restore` to build and `--no-build` to test/pack for speed.

## Release Workflow Pattern (release.yml)

**Trigger:** Push of a `v*` tag.

**Key steps:**
1. Full build + pack (cannot reuse CI artifacts across workflows).
2. `dotnet nuget push "nupkgs/*.nupkg" --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json --skip-duplicate`
3. Create GitHub Release with `softprops/action-gh-release@v2` attaching nupkg files.

**Requirements:**
- `NUGET_API_KEY` repo secret.
- `permissions: contents: write` for release creation.
- `--skip-duplicate` prevents failures on re-runs.

## Release Flow

```
developer pushes to main → build.yml runs CI
developer tags v0.1.0     → release.yml builds, packs, publishes to NuGet, creates GitHub Release
```

## Anti-patterns to Avoid

- Don't trigger NuGet publish on every push to main (accidental publishes).
- Don't use `dotnet pack` without `--no-build` after a build step (double compilation).
- Don't upload artifacts from every matrix leg (duplicate artifact names fail).
