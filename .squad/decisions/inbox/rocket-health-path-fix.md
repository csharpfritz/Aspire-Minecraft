# Decision: Remove HealthCheckAnnotation.Key from WithMonitoredResource

**Date:** 2026-03-02
**By:** Rocket
**Status:** Implemented

## Context

`WithMonitoredResource` in `MinecraftServerBuilderExtensions.cs` extracted `HealthCheckAnnotation.Key` and set it as `ASPIRE_RESOURCE_{NAME}_HEALTH_PATH`. The worker used this path to poll resource health.

## Problem

`HealthCheckAnnotation.Key` is the health check **registration name** (e.g. `"api_HttpHealthCheck"`), not a URL path. This caused the worker to poll `https://host/api_HttpHealthCheck` which returned 404, making healthy resources appear permanently unhealthy in Minecraft.

## Decision

Removed the `HealthCheckAnnotation` extraction block entirely. Without a `HEALTH_PATH` env var, `AspireResourceMonitor.CheckHttpHealthAsync` falls through to check the base URL, which the API already maps to return 200 (healthy) or 503 (unhealthy).

## Impact

- **Worker:** Now correctly detects resource health via base URL polling.
- **Error minecarts:** Will only spawn when resources actually go unhealthy (via "Trigger Error" button), not due to false-negative health detection.
- **No breaking changes:** Resources without explicit health paths were already handled by the base-URL fallback.
