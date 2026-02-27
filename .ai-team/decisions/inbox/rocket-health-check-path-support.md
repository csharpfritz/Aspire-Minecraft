# Health Check Path Support for Resource Monitoring

## Context
The Minecraft worker's `AspireResourceMonitor` polls resource health by hitting the base URL from `ASPIRE_RESOURCE_{NAME}_URL` environment variables. For resources with health check endpoints at specific paths (like `/health`), this means the worker never actually checks the health endpoint — it just hits the root path.

## Decision
Implemented two-layered health check support:

1. **Quick Fix (API Service)**: Modified the Grand Village Demo API's root endpoint (`GET /`) to also return 503 when the `isHealthy` flag is false, matching the behavior of `/health`. This ensures the worker's current URL-based polling detects errors immediately.

2. **Proper Fix (Framework Level)**: Added `ASPIRE_RESOURCE_{NAME}_HEALTH_PATH` environment variable support:
   - `MinecraftServerBuilderExtensions.cs` now detects `HealthCheckAnnotation` on resources and sets the `_HEALTH_PATH` env var
   - `AspireResourceMonitor.cs` reads this env var during resource discovery and stores it in `ResourceInfo`
   - Health checks append the health path to the base URL when polling

## Implementation Details
- Added `HealthPath` field to `ResourceInfo` record (positioned after `TcpPort`, before `Status`)
- Updated all test fixtures to include empty `HealthPath` parameter (`""`)
- Health check annotation discovery uses `TryGetLastAnnotation<HealthCheckAnnotation>` with the Key property (defaults to "/health")

## Rationale
The two-fix approach ensures:
- **Immediate resolution**: API service errors are detected right now
- **Scalable pattern**: Any future resource with `WithHttpHealthCheck()` automatically benefits
- **Backward compatibility**: Resources without health annotations continue working as before
- **Explicit over implicit**: Health path is explicitly passed via env var rather than making assumptions

## Alternatives Considered
- Modifying the worker to always try `/health` first: Rejected because not all HTTP services use `/health`
- Using HTTP header hints: Rejected as overly complex for this use case
- Dashboard-only health checks: Rejected because the worker needs independent health awareness for in-world visualization

## Consequences
- Positive: Generalizable solution for any resource type
- Positive: Clear separation between base URL and health check path
- Neutral: Requires test updates when `ResourceInfo` signature changes
- Negative: Adds another environment variable per monitored resource (acceptable trade-off)
