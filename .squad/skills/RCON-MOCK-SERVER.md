# SKILL: RCON Protocol Mock Server Pattern

## Context
Testing the Minecraft RCON binary protocol client (RconClient) requires a mock TCP server since the client connects directly via TcpClient with no abstraction seam.

## Pattern
1. Create a `TcpListener` on `IPAddress.Loopback` port `0` (OS-assigned random port)
2. Start the listener and read the assigned port from `((IPEndPoint)_listener.LocalEndpoint).Port`
3. Use parallel tasks: `AcceptTcpClientAsync` on server side, `ConnectAsync` on client side
4. Read/write RCON packets using the binary protocol format:
   - 4 bytes: packet length (LE int32) = 4 + 4 + payload.Length + 2
   - 4 bytes: request ID (LE int32)
   - 4 bytes: type (LE int32) — 3=login, 2=command, 0=response
   - N bytes: payload (UTF-8)
   - 2 bytes: null terminators

## Example
```csharp
var listener = new TcpListener(IPAddress.Loopback, 0);
listener.Start();
var port = ((IPEndPoint)listener.LocalEndpoint).Port;

await using var client = new RconClient();
var acceptTask = listener.AcceptTcpClientAsync(ct);
await client.ConnectAsync("127.0.0.1", port, ct);
var serverClient = await acceptTask;
var stream = serverClient.GetStream();

// Read auth packet from client, respond with matching requestId
// Read command packet from client, respond with payload
```

## When to Use
- Testing any code that uses `RconClient` directly
- Verifying RCON packet binary format correctness
- Testing authentication success/failure flows
- Testing command send/receive cycles

## Gotchas
- Always use `CancellationTokenSource` with timeout to prevent test hangs
- `RconPacket` is internal — requires InternalsVisibleTo from the RCON project
- The client increments request IDs with `Interlocked.Increment`, so IDs start at 2 (1 is pre-incremented)
