### Decision: Never chain `.WithHttpEndpoint()` after `AddSpringApp()` / `AddJavaApp()`

**By:** Shuri
**Date:** 2026-02-17
**Affects:** Anyone adding Java/Spring container resources to Aspire AppHost demos

**Context:**
`CommunityToolkit.Aspire.Hosting.Java`'s `AddSpringApp()` (and `AddJavaApp()`) internally registers a named HTTP endpoint via `JavaAppContainerResource.HttpEndpointName` using the `Port` and `TargetPort` from `JavaAppContainerResourceOptions`. Chaining an additional `.WithHttpEndpoint()` creates a duplicate endpoint, causing a runtime allocation error.

**Decision:**
Configure the host-side port via `JavaAppContainerResourceOptions.Port` (default 8080) and `TargetPort` (default 8080) — do NOT add a separate `.WithHttpEndpoint()` call. This is different from `AddPythonApp()` and `AddNodeApp()` which do NOT auto-register endpoints and require explicit `.WithHttpEndpoint()`.

**Example (correct):**
```csharp
var javaApi = builder.AddSpringApp("java-api",
    new JavaAppContainerResourceOptions
    {
        ContainerImageName = "aliencube/aspire-spring-maven-sample",
        Port = 5500,    // host-side port
                        // TargetPort defaults to 8080 (Spring Boot default)
    });
```

**Anti-pattern (causes duplicate endpoint error):**
```csharp
var javaApi = builder.AddSpringApp("java-api",
    new JavaAppContainerResourceOptions { ... })
    .WithHttpEndpoint(targetPort: 8080, port: 5500);  // ❌ duplicate!
```
