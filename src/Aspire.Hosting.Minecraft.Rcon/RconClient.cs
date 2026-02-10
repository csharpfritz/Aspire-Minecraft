using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;

namespace Aspire.Hosting.Minecraft.Rcon;

/// <summary>
/// Low-level Minecraft RCON protocol client.
/// Implements the Source RCON protocol used by Minecraft servers.
/// </summary>
public sealed class RconClient : IAsyncDisposable
{
    private const int PacketTypeLogin = 3;
    private const int PacketTypeCommand = 2;
    private const int PacketTypeResponse = 0;
    private const int MaxPayloadSize = 4096;

    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private int _nextRequestId = 1;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private bool _authenticated;

    /// <summary>
    /// Gets a value indicating whether the client is connected and authenticated.
    /// </summary>
    public bool IsConnected => _tcp?.Connected == true && _authenticated;

    /// <summary>
    /// Connects to a Minecraft RCON server.
    /// </summary>
    /// <param name="host">The hostname or IP address of the RCON server.</param>
    /// <param name="port">The RCON port number (typically 25575).</param>
    /// <param name="cancellationToken">A token to cancel the connection attempt.</param>
    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        _tcp = new TcpClient();
        await _tcp.ConnectAsync(host, port, cancellationToken);
        _stream = _tcp.GetStream();
    }

    /// <summary>
    /// Authenticates with the RCON server using the given password.
    /// </summary>
    /// <param name="password">The RCON password configured on the Minecraft server.</param>
    /// <param name="cancellationToken">A token to cancel the authentication attempt.</param>
    /// <returns><c>true</c> if authentication succeeded; <c>false</c> otherwise.</returns>
    public async Task<bool> AuthenticateAsync(string password, CancellationToken cancellationToken = default)
    {
        if (_stream is null) throw new InvalidOperationException("Not connected.");

        var requestId = Interlocked.Increment(ref _nextRequestId);
        await SendPacketAsync(requestId, PacketTypeLogin, password, cancellationToken);
        var response = await ReadPacketAsync(cancellationToken);

        _authenticated = response.RequestId == requestId;
        return _authenticated;
    }

    /// <summary>
    /// Sends a command to the Minecraft server and returns the response.
    /// </summary>
    /// <param name="command">The RCON command to send (e.g., "list", "tps", "weather clear").</param>
    /// <param name="cancellationToken">A token to cancel the command.</param>
    /// <returns>The server's text response to the command.</returns>
    /// <exception cref="InvalidOperationException">Thrown when not connected or not authenticated.</exception>
    public async Task<string> SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        if (_stream is null || !_authenticated)
            throw new InvalidOperationException("Not connected or not authenticated.");

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            var requestId = Interlocked.Increment(ref _nextRequestId);
            await SendPacketAsync(requestId, PacketTypeCommand, command, cancellationToken);
            var response = await ReadPacketAsync(cancellationToken);
            return response.Payload;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task SendPacketAsync(int requestId, int type, string payload, CancellationToken ct)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        // Packet: Length(4) + RequestId(4) + Type(4) + Payload + Pad(2)
        var packetLength = 4 + 4 + payloadBytes.Length + 2;
        var buffer = new byte[4 + packetLength];

        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0), packetLength);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4), requestId);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(8), type);
        payloadBytes.CopyTo(buffer, 12);
        // Two null terminators already zero from array init

        await _stream!.WriteAsync(buffer, ct);
    }

    private async Task<RconPacket> ReadPacketAsync(CancellationToken ct)
    {
        var header = new byte[4];
        await ReadExactlyAsync(header, ct);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);

        if (length > MaxPayloadSize + 10)
            throw new InvalidOperationException($"RCON packet too large: {length}");

        var body = new byte[length];
        await ReadExactlyAsync(body, ct);

        var requestId = BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(0));
        var type = BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(4));
        var payload = Encoding.UTF8.GetString(body, 8, length - 10); // minus requestId(4) + type(4) + pad(2)

        return new RconPacket(requestId, type, payload);
    }

    private async Task ReadExactlyAsync(byte[] buffer, CancellationToken ct)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await _stream!.ReadAsync(buffer.AsMemory(offset), ct);
            if (read == 0) throw new IOException("RCON connection closed.");
            offset += read;
        }
    }

    /// <summary>
    /// Disposes the RCON client, closing the TCP connection.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _authenticated = false;
        if (_stream is not null) await _stream.DisposeAsync();
        _tcp?.Dispose();
        _sendLock.Dispose();
    }
}

internal readonly record struct RconPacket(int RequestId, int Type, string Payload);
