---
name: "post-build-validation"
description: "Validate side effects after build/deploy operations with graceful degradation"
domain: "error-handling, resilience, observability"
confidence: "low"
source: "earned"
---

## Context

When build or deployment operations rely on external systems (APIs, remote commands, databases), individual operations can fail silently or be rate-limited. Post-build validation helps detect these failures and provides observability without blocking the entire process.

## Patterns

**Validate After Build, Not During:**
- Separate validation logic from build logic
- Call validation methods after primary build operations complete
- Keeps build methods focused on construction, validation methods on verification

**Graceful Degradation:**
- Log warnings instead of throwing exceptions
- Return boolean success/failure instead of throwing
- Catch and handle exceptions in validation code to prevent cascading failures
- Use `LogWarning()` with structured logging (include coordinates, expected values)

**Verification Helper Pattern:**
```csharp
private async Task<bool> VerifyBlockAsync(int x, int y, int z, string expectedBlock, CancellationToken ct)
{
    try
    {
        var response = await rcon.SendCommandAsync($"testforblock {x} {y} {z} {expectedBlock}", ct);
        return !response.Contains("did not match", StringComparison.OrdinalIgnoreCase);
    }
    catch
    {
        return false; // Fail gracefully on exceptions
    }
}
```

**Structure-Specific Validation:**
```csharp
private async Task BuildWatchtowerAsync(int x, int y, int z, CancellationToken ct)
{
    // ... build operations ...
    await ValidateWatchtowerAsync(x, y, z, ct);
}

private async Task ValidateWatchtowerAsync(int x, int y, int z, CancellationToken ct)
{
    if (!await VerifyBlockAsync(x + 3, y + 4, z + 1, "minecraft:glass_pane", ct))
        logger.LogWarning("Validation failed at ({X},{Y},{Z})", x + 3, y + 4, z + 1);
}
```

## Examples

**StructureBuilder.cs:**
- Each `Build*Async()` method calls corresponding `Validate*Async()` method
- Validates critical blocks (doors, windows) that indicate successful placement
- Uses `VerifyBlockAsync()` helper for consistent validation logic
- Logs warnings with coordinates and expected block types

## Anti-Patterns

**Don't throw exceptions from validation:**
- Bad: `throw new ValidationException("Block mismatch")`
- Good: `logger.LogWarning("Block mismatch at {X},{Y},{Z}", x, y, z)`

**Don't validate everything:**
- Focus on critical indicators (doors, windows) not every single block
- Full validation would add significant overhead

**Don't block the build process:**
- Validation failures should not prevent other structures from building
- Use warnings for observability, not errors for blocking
