# ExecutableResource Health Detection Fix

**Date:** 2026-02-16  
**Author:** Shuri  
**Status:** Implemented  
**Issue:** Python and Node.js apps not showing as healthy in Minecraft village despite Aspire dashboard showing them green

## Root Cause

ExecutableResource types (PythonApp, NodeApp, JavaScriptApp) have endpoints that resolve to DCP-proxied URLs. These proxy addresses are not reachable from the Minecraft worker container's network context:

- **ProjectResources** work because they're Docker containers with proper service networking
- **ExecutableResources** fail because their HTTP endpoints resolve to DCP proxy addresses (e.g., `http://localhost:5300`) that exist in the host context, not the container network
- **Aspire dashboard** shows them as healthy because it queries DCP's resource state API, not the actual HTTP endpoints

The worker's HTTP health check at `AspireResourceMonitor.CheckHttpHealthAsync()` was trying to reach these unreachable proxy URLs, causing them to always appear unhealthy.

## Solution

Skip endpoint resolution for ExecutableResource types in `MinecraftServerBuilderExtensions.WithMonitoredResource()`. This prevents URL/HOST/PORT environment variables from being set for these resource types.

When a resource has no endpoint configuration, `AspireResourceMonitor.PollHealthAsync()` follows the "no endpoint" path (lines 84-87) and assumes `ResourceStatus.Healthy`. This matches the Aspire dashboard behavior.

### Detection Logic

```csharp
var isExecutable = resourceType.Contains("PythonApp", StringComparison.OrdinalIgnoreCase)
    || resourceType.Contains("NodeApp", StringComparison.OrdinalIgnoreCase)
    || resourceType.Contains("JavaScriptApp", StringComparison.OrdinalIgnoreCase)
    || resourceType.Contains("Executable", StringComparison.OrdinalIgnoreCase);
```

The `resourceType` string comes from `GetType().Name.Replace("Resource", "")`, so PythonAppResource → "PythonApp", etc.

## Files Changed

- `src/Aspire.Hosting.Minecraft/MinecraftServerBuilderExtensions.cs` (lines 289-335)

## Tradeoffs

**Pros:**
- Python/Node apps now show healthy when Aspire dashboard shows them healthy (consistency)
- No false negatives from unreachable proxy URLs
- Minimal code change (guard clause before endpoint resolution)
- Matches Aspire dashboard's health determination strategy

**Cons:**
- ExecutableResources no longer have HTTP health checks — assumed healthy if process is running
- If an ExecutableResource crashes or returns 500 errors, the village won't detect it
- Not a "true" health check, but reflects what Aspire dashboard shows

## Alternative Considered

**TCP health check instead of HTTP:** Check if the port is listening rather than HTTP 200 response. Rejected because:
1. Still requires resolving the endpoint, which has the same DCP proxy issue
2. Doesn't provide better signal than "process running" (which is what DCP reports)

## Recommendation for Future

If Aspire exposes a resource state API or health endpoint that the worker can query (similar to what the dashboard uses), we should switch to that for all resource types. This would provide true health status for ExecutableResources.

## Verification

1. Build: `dotnet build Aspire-Minecraft.slnx -c Release` — **PASSED**
2. Manual test: Run GrandVillageDemo sample and verify Python/Node workshops show green lanterns
3. Aspire dashboard: Verify resources show same health state in dashboard and Minecraft village
