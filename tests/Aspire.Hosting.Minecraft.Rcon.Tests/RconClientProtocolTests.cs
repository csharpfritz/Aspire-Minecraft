using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Xunit;

namespace Aspire.Hosting.Minecraft.Rcon.Tests;

public class RconClientProtocolTests : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private TcpClient? _serverSideClient;
    private NetworkStream? _serverStream;

    public int Port { get; }

    public RconClientProtocolTests()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
    }

    public async ValueTask DisposeAsync()
    {
        if (_serverStream is not null) await _serverStream.DisposeAsync();
        _serverSideClient?.Dispose();
        _listener.Stop();
    }

    private async Task AcceptClientAsync(CancellationToken ct = default)
    {
        _serverSideClient = await _listener.AcceptTcpClientAsync(ct);
        _serverStream = _serverSideClient.GetStream();
    }

    private async Task<RconPacket> ReadPacketFromClientAsync(CancellationToken ct = default)
    {
        var header = new byte[4];
        await ReadExactlyAsync(_serverStream!, header, ct);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);

        var body = new byte[length];
        await ReadExactlyAsync(_serverStream!, body, ct);

        var requestId = BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(0));
        var type = BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(4));
        var payload = Encoding.UTF8.GetString(body, 8, length - 10);

        return new RconPacket(requestId, type, payload);
    }

    private async Task SendPacketToClientAsync(int requestId, int type, string payload, CancellationToken ct = default)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var packetLength = 4 + 4 + payloadBytes.Length + 2;
        var buffer = new byte[4 + packetLength];

        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0), packetLength);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4), requestId);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(8), type);
        payloadBytes.CopyTo(buffer, 12);

        await _serverStream!.WriteAsync(buffer, ct);
    }

    private static async Task ReadExactlyAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), ct);
            if (read == 0) throw new IOException("Connection closed.");
            offset += read;
        }
    }

    [Fact]
    public async Task AuthenticateAsync_SuccessfulLogin_ReturnsTrue()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var client = new RconClient();

        var acceptTask = AcceptClientAsync(cts.Token);
        await client.ConnectAsync("127.0.0.1", Port, cts.Token);
        await acceptTask;

        // Server side: read auth packet and respond with matching request ID
        var authTask = client.AuthenticateAsync("test-password", cts.Token);
        var authPacket = await ReadPacketFromClientAsync(cts.Token);
        Assert.Equal(3, authPacket.Type); // PacketTypeLogin
        Assert.Equal("test-password", authPacket.Payload);

        await SendPacketToClientAsync(authPacket.RequestId, 2, "", cts.Token);
        var result = await authTask;

        Assert.True(result);
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task AuthenticateAsync_FailedLogin_ReturnsFalse()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var client = new RconClient();

        var acceptTask = AcceptClientAsync(cts.Token);
        await client.ConnectAsync("127.0.0.1", Port, cts.Token);
        await acceptTask;

        var authTask = client.AuthenticateAsync("wrong-password", cts.Token);
        var authPacket = await ReadPacketFromClientAsync(cts.Token);

        // Respond with -1 request ID (authentication failure)
        await SendPacketToClientAsync(-1, 2, "", cts.Token);
        var result = await authTask;

        Assert.False(result);
        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task AuthenticateAsync_WithoutConnection_ThrowsInvalidOperation()
    {
        await using var client = new RconClient();
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.AuthenticateAsync("password"));
    }

    [Fact]
    public async Task SendCommandAsync_WithoutAuth_ThrowsInvalidOperation()
    {
        await using var client = new RconClient();
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendCommandAsync("list"));
    }

    [Fact]
    public async Task SendCommandAsync_SendsCorrectPacketAndReceivesResponse()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var client = new RconClient();

        var acceptTask = AcceptClientAsync(cts.Token);
        await client.ConnectAsync("127.0.0.1", Port, cts.Token);
        await acceptTask;

        // Authenticate first
        var authTask = client.AuthenticateAsync("password", cts.Token);
        var authPacket = await ReadPacketFromClientAsync(cts.Token);
        await SendPacketToClientAsync(authPacket.RequestId, 2, "", cts.Token);
        await authTask;

        // Send command
        var commandTask = client.SendCommandAsync("list", cts.Token);
        var cmdPacket = await ReadPacketFromClientAsync(cts.Token);
        Assert.Equal(2, cmdPacket.Type); // PacketTypeCommand
        Assert.Equal("list", cmdPacket.Payload);

        // Respond with player list
        var responsePayload = "There are 2 of a max of 20 players online: Steve, Alex";
        await SendPacketToClientAsync(cmdPacket.RequestId, 0, responsePayload, cts.Token);
        var response = await commandTask;

        Assert.Equal(responsePayload, response);
    }

    [Fact]
    public async Task SendCommandAsync_PacketFormat_HasCorrectBinaryLayout()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var client = new RconClient();

        var acceptTask = AcceptClientAsync(cts.Token);
        await client.ConnectAsync("127.0.0.1", Port, cts.Token);
        await acceptTask;

        // Authenticate
        var authTask = client.AuthenticateAsync("pw", cts.Token);
        var authPacket = await ReadPacketFromClientAsync(cts.Token);
        await SendPacketToClientAsync(authPacket.RequestId, 2, "", cts.Token);
        await authTask;

        // Send a command and verify the raw packet structure
        var commandTask = client.SendCommandAsync("tps", cts.Token);
        var cmdPacket = await ReadPacketFromClientAsync(cts.Token);

        // Verify packet contents
        Assert.True(cmdPacket.RequestId > 0);
        Assert.Equal(2, cmdPacket.Type);
        Assert.Equal("tps", cmdPacket.Payload);

        await SendPacketToClientAsync(cmdPacket.RequestId, 0, "20.0, 20.0, 20.0", cts.Token);
        await commandTask;
    }

    [Fact]
    public async Task IsConnected_BeforeConnect_ReturnsFalse()
    {
        await using var client = new RconClient();
        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task IsConnected_AfterConnectBeforeAuth_ReturnsFalse()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var client = new RconClient();

        var acceptTask = AcceptClientAsync(cts.Token);
        await client.ConnectAsync("127.0.0.1", Port, cts.Token);
        await acceptTask;

        // Connected but not authenticated
        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task DisposeAsync_CleansUpResources()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var client = new RconClient();

        var acceptTask = AcceptClientAsync(cts.Token);
        await client.ConnectAsync("127.0.0.1", Port, cts.Token);
        await acceptTask;

        // Authenticate
        var authTask = client.AuthenticateAsync("pw", cts.Token);
        var authPacket = await ReadPacketFromClientAsync(cts.Token);
        await SendPacketToClientAsync(authPacket.RequestId, 2, "", cts.Token);
        await authTask;

        Assert.True(client.IsConnected);
        await client.DisposeAsync();
        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task SendCommandAsync_MultipleCommands_IncrementRequestIds()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var client = new RconClient();

        var acceptTask = AcceptClientAsync(cts.Token);
        await client.ConnectAsync("127.0.0.1", Port, cts.Token);
        await acceptTask;

        // Authenticate
        var authTask = client.AuthenticateAsync("pw", cts.Token);
        var authPacket = await ReadPacketFromClientAsync(cts.Token);
        await SendPacketToClientAsync(authPacket.RequestId, 2, "", cts.Token);
        await authTask;

        // First command
        var cmd1Task = client.SendCommandAsync("list", cts.Token);
        var pkt1 = await ReadPacketFromClientAsync(cts.Token);
        await SendPacketToClientAsync(pkt1.RequestId, 0, "response1", cts.Token);
        await cmd1Task;

        // Second command
        var cmd2Task = client.SendCommandAsync("tps", cts.Token);
        var pkt2 = await ReadPacketFromClientAsync(cts.Token);
        await SendPacketToClientAsync(pkt2.RequestId, 0, "response2", cts.Token);
        await cmd2Task;

        Assert.True(pkt2.RequestId > pkt1.RequestId);
    }
}
