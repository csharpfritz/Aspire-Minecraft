# Contributing to Aspire.Hosting.Minecraft

Thank you for your interest in contributing! This guide will help you get started.

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or later
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for running samples)
- A code editor (Visual Studio, VS Code, or Rider recommended)

## Building

```bash
dotnet build
```

For release builds:

```bash
dotnet build -c Release
```

## Testing

```bash
dotnet test
```

Tests are in the `tests/` directory:

| Project | Scope |
|---------|-------|
| `Aspire.Hosting.Minecraft.Tests` | Hosting integration tests |
| `Aspire.Hosting.Minecraft.Rcon.Tests` | RCON protocol client tests |
| `Aspire.Hosting.Minecraft.Worker.Tests` | Worker service tests |

## Packing

To produce the NuGet package locally:

```bash
dotnet pack -o nupkgs
```

This produces a single `Fritz.Aspire.Hosting.Minecraft.{version}.nupkg` in the `nupkgs/` folder.

## Project Structure

```
src/
  Aspire.Hosting.Minecraft/        # Hosting library (the NuGet package)
  Aspire.Hosting.Minecraft.Rcon/   # RCON protocol client (embedded in hosting package)
  Aspire.Hosting.Minecraft.Worker/ # In-world display worker (standalone, not packaged)
tests/
  Aspire.Hosting.Minecraft.Tests/
  Aspire.Hosting.Minecraft.Rcon.Tests/
  Aspire.Hosting.Minecraft.Worker.Tests/
samples/
  GrandVillageDemo/                # Demo application
```

### Single-Package Architecture

Only `Aspire.Hosting.Minecraft` is published as a NuGet package. The RCON client library is embedded into it via `PrivateAssets="All"` on the `ProjectReference`, so consumers get a single package. The Worker project is a standalone service that runs as a separate process — consumers reference it via `WithAspireWorldDisplay<TWorkerProject>()` using a project reference.

## Code Style

- Follow existing patterns in the codebase
- Use XML documentation comments on all public APIs
- Use `internal` for implementation details — only types intended for consumer use should be `public`
- Source projects use `InternalsVisibleTo` to allow test access to internal types
- Use primary constructors where appropriate
- Nullable reference types are enabled globally

## Pull Request Process

1. Fork the repository and create a feature branch
2. Make your changes following the code style guidelines
3. Ensure `dotnet build -c Release` compiles cleanly
4. Ensure `dotnet test` passes
5. Ensure `dotnet pack -o nupkgs` produces packages without warnings on `src/` projects
6. Open a pull request using the [PR template](.github/PULL_REQUEST_TEMPLATE.md)

## Issue Labels

| Label | Meaning |
|-------|---------|
| `sprint-1`, `sprint-2`, `sprint-3` | Sprint assignment |
| `shuri`, `rocket`, `nebula`, `wong`, `rhodey`, `mantis` | Team member assignment |

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE).
