### 2026-02-19: Fix Java Spring app OTEL agent path in GrandVillageDemo
**By:** Rocket
**What:** Added `OtelAgentPath = "/agents"` to the `JavaAppContainerResourceOptions` for the `java-api` resource in the GrandVillageDemo AppHost. The `aliencube/aspire-spring-maven-sample` image stores its OpenTelemetry Java agent at `/agents/opentelemetry-javaagent.jar`, not at the root path that `CommunityToolkit.Aspire.Hosting.Java` defaults to when `OtelAgentPath` is omitted.
**Why:** Without this setting, the JVM picks up `JAVA_TOOL_OPTIONS=-javaagent:/opentelemetry-javaagent.jar` (injected by the CommunityToolkit package), fails to find the JAR at `/`, and crashes immediately with exit code 1. The container never started. This one-line fix maps the agent path to the actual image layout.
