using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Aspire.Hosting.Minecraft.Worker.Tests.Helpers;

/// <summary>
/// A lightweight mock TCP server that speaks the RCON protocol.
/// Accepts connections, handles authentication, and records all commands received.
/// </summary>
internal sealed class MockRconServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<string> _receivedCommands = [];
    private Task? _acceptLoop;

    public int Port { get; }
    public IReadOnlyList<string> ReceivedCommands => _receivedCommands;

    public MockRconServer()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => HandleClientAsync(client, ct), ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        try
        {
            using (client)
            await using (var stream = client.GetStream())
            {
                while (!ct.IsCancellationRequested)
                {
                    var packet = await ReadPacketAsync(stream, ct);
                    if (packet is null) break;

                    if (packet.Value.type == 3) // Login
                    {
                        // Always accept auth
                        await WritePacketAsync(stream, packet.Value.requestId, 2, "", ct);
                    }
                    else if (packet.Value.type == 2) // Command
                    {
                        lock (_receivedCommands)
                        {
                            _receivedCommands.Add(packet.Value.payload);
                        }
                        await WritePacketAsync(stream, packet.Value.requestId, 0, "", ct);
                    }
                }
            }
        }
        catch (IOException) { }
        catch (OperationCanceledException) { }
    }

    private static async Task<(int requestId, int type, string payload)?> ReadPacketAsync(
        NetworkStream stream, CancellationToken ct)
    {
        var header = new byte[4];
        var bytesRead = 0;
        while (bytesRead < 4)
        {
            var read = await stream.ReadAsync(header.AsMemory(bytesRead), ct);
            if (read == 0) return null;
            bytesRead += read;
        }

        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        var body = new byte[length];
        bytesRead = 0;
        while (bytesRead < length)
        {
            var read = await stream.ReadAsync(body.AsMemory(bytesRead), ct);
            if (read == 0) return null;
            bytesRead += read;
        }

        var requestId = BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(0));
        var type = BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(4));
        var payload = Encoding.UTF8.GetString(body, 8, length - 10);

        return (requestId, type, payload);
    }

    private static async Task WritePacketAsync(
        NetworkStream stream, int requestId, int type, string payload, CancellationToken ct)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var packetLength = 4 + 4 + payloadBytes.Length + 2;
        var buffer = new byte[4 + packetLength];

        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0), packetLength);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4), requestId);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(8), type);
        payloadBytes.CopyTo(buffer, 12);

        await stream.WriteAsync(buffer, ct);
    }

    public void ClearCommands()
    {
        lock (_receivedCommands)
        {
            _receivedCommands.Clear();
        }
    }

    public List<string> GetCommands()
    {
        lock (_receivedCommands)
        {
            return [.. _receivedCommands];
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _listener.Stop();
        if (_acceptLoop is not null)
        {
            try { await _acceptLoop; } catch { }
        }
        _cts.Dispose();
    }
}
