### 2026-02-27: User directive — error boats trigger on OTel error logs, not HTTP health status
**By:** Jeffrey T. Fritz (via Copilot)
**What:** Error boats should NOT spawn when a service returns HTTP 503. Instead, error boats should spawn when an OpenTelemetry error-level log entry is posted by a monitored resource. The "Trigger Error" dashboard command should cause an OTel error log, not a health check flip.
**Why:** User request — captured for team memory. Jeff wants the in-world visualization to react to application errors (logged via OTel), not infrastructure health status. This is a more meaningful signal — a service can be "healthy" but still have application errors worth visualizing.
