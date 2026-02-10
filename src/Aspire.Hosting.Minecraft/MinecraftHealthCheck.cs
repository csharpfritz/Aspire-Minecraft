using Aspire.Hosting.Minecraft.Rcon;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aspire.Hosting.Minecraft;

/// <summary>
/// Health check that verifies the Minecraft server is responsive via RCON.
/// Uses a connection string supplier that is populated once the resource's
/// connection string becomes available (via Aspire eventing).
/// </summary>
public class MinecraftHealthCheck(Func<string?> connectionStringSupplier) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var connectionString = connectionStringSupplier();
        if (string.IsNullOrEmpty(connectionString))
            return HealthCheckResult.Unhealthy("Connection string not yet available.");

        var parts = ParseConnectionString(connectionString);

        try
        {
            await using var client = new RconClient();
            await client.ConnectAsync(parts.Host, parts.Port, cancellationToken);
            var authed = await client.AuthenticateAsync(parts.Password, cancellationToken);

            if (!authed)
                return HealthCheckResult.Unhealthy("RCON authentication failed.");

            var response = await client.SendCommandAsync("list", cancellationToken);
            var result = RconResponseParser.ParsePlayerList(response);

            return HealthCheckResult.Healthy(
                $"Server online. {result.Online}/{result.Max} players.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Cannot connect to Minecraft server.", ex);
        }
    }

    internal static (string Host, int Port, string Password) ParseConnectionString(string cs)
    {
        var host = "localhost";
        var port = 25575;
        var password = "";

        foreach (var part in cs.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            switch (kv[0].Trim().ToLowerInvariant())
            {
                case "host": host = kv[1].Trim(); break;
                case "port": int.TryParse(kv[1].Trim(), out port); break;
                case "password": password = kv[1].Trim(); break;
            }
        }

        return (host, port, password);
    }
}
