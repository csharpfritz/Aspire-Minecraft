# SKILL: Aspire Dashboard Custom Commands

## When to Use
When adding interactive buttons to the Aspire dashboard that call HTTP endpoints on resources.

## Pattern: WithHttpCommand (Aspire 13.1+)

Use `WithHttpCommand` for commands that send HTTP requests to a resource's own endpoint. This is the idiomatic way — it handles endpoint resolution, HTTP client, and result reporting automatically.

### AppHost Side
```csharp
var api = builder.AddProject<Projects.MyService>("api")
    .WithHttpHealthCheck("/health")  // Enable Aspire health polling
    .WithHttpCommand("/my-endpoint", "Button Label",
        commandName: "my-command",
        commandOptions: new HttpCommandOptions
        {
            IconName = "ErrorCircle",           // FluentUI icon name
            IconVariant = IconVariant.Filled,    // Regular or Filled
            Description = "Tooltip text",
            ConfirmationMessage = "Are you sure?",
            IsHighlighted = true,               // Makes button prominent
            // Method defaults to POST — set explicitly for GET:
            // Method = HttpMethod.Get,
        });
```

### Service Side
```csharp
app.MapPost("/my-endpoint", () =>
{
    // Do the thing
    return Results.Ok(new { message = "Done" });
});
```

## Key Facts

- `WithHttpCommand` defaults to POST method.
- `WithHttpHealthCheck` is needed for Aspire to actively monitor `/health` — without it, health endpoint changes are invisible to the orchestrator.
- The `IconName` must be a valid [FluentUI System Icon](https://aka.ms/fluentui-system-icons).
- Return 200 from the endpoint for the command to show as "succeeded" in the dashboard. Non-2xx = failure (override with `GetCommandResult` callback).
- Use `PrepareRequest` callback in `HttpCommandOptions` to add headers or modify the request before it's sent.

## Anti-Pattern: WithCommand + Manual HttpClient

Avoid this for same-resource HTTP calls:
```csharp
// DON'T — WithHttpCommand handles endpoint resolution for you
.WithCommand("my-cmd", "Button", async context =>
{
    var client = new HttpClient();
    // Manually resolve endpoint URL... error-prone
    await client.PostAsync(url, null);
    return CommandResults.Success();
});
```

## References

- [WithHttpCommand API](https://learn.microsoft.com/dotnet/api/aspire.hosting.resourcebuilderextensions.withhttpcommand)
- [HttpCommandOptions](https://learn.microsoft.com/dotnet/api/aspire.hosting.applicationmodel.httpcommandoptions)
- [WithCommand API](https://learn.microsoft.com/dotnet/api/aspire.hosting.resourcebuilderextensions.withcommand) (for non-HTTP commands)
