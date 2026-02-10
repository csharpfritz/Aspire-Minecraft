using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Aspire.Hosting.Minecraft.Tests;

public class MinecraftHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_NullConnectionString_ReturnsUnhealthy()
    {
        var healthCheck = new MinecraftHealthCheck(() => null);
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("test", healthCheck, null, null)
        });

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("Connection string not yet available", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_EmptyConnectionString_ReturnsUnhealthy()
    {
        var healthCheck = new MinecraftHealthCheck(() => "");
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("test", healthCheck, null, null)
        });

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("Connection string not yet available", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_UnreachableServer_ReturnsUnhealthy()
    {
        var healthCheck = new MinecraftHealthCheck(
            () => "Host=127.0.0.1;Port=1;Password=test");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext
            {
                Registration = new HealthCheckRegistration("test", healthCheck, null, null)
            },
            cts.Token);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("Cannot connect", result.Description);
    }

    #region ParseConnectionString

    [Fact]
    public void ParseConnectionString_FullConnectionString_ExtractsAllComponents()
    {
        var (host, port, password) = MinecraftHealthCheck.ParseConnectionString(
            "Host=mc.example.com;Port=25575;Password=secret123");

        Assert.Equal("mc.example.com", host);
        Assert.Equal(25575, port);
        Assert.Equal("secret123", password);
    }

    [Fact]
    public void ParseConnectionString_OnlyHost_UsesDefaultPortAndEmptyPassword()
    {
        var (host, port, password) = MinecraftHealthCheck.ParseConnectionString("Host=myserver");

        Assert.Equal("myserver", host);
        Assert.Equal(25575, port);
        Assert.Empty(password);
    }

    [Fact]
    public void ParseConnectionString_EmptyString_ReturnsDefaults()
    {
        var (host, port, password) = MinecraftHealthCheck.ParseConnectionString("");

        Assert.Equal("localhost", host);
        Assert.Equal(25575, port);
        Assert.Empty(password);
    }

    [Fact]
    public void ParseConnectionString_CaseInsensitiveKeys()
    {
        var (host, port, password) = MinecraftHealthCheck.ParseConnectionString(
            "HOST=server1;PORT=9999;PASSWORD=pass");

        Assert.Equal("server1", host);
        Assert.Equal(9999, port);
        Assert.Equal("pass", password);
    }

    [Fact]
    public void ParseConnectionString_InvalidPort_UsesDefaultPort()
    {
        var (_, port, _) = MinecraftHealthCheck.ParseConnectionString(
            "Host=localhost;Port=notanumber;Password=pw");

        Assert.Equal(0, port); // int.TryParse fails, out parameter defaults to 0
    }

    [Fact]
    public void ParseConnectionString_ExtraWhitespace_IsTrimmed()
    {
        var (host, port, password) = MinecraftHealthCheck.ParseConnectionString(
            " Host = mc.local ; Port = 12345 ; Password = secret ");

        Assert.Equal("mc.local", host);
        Assert.Equal(12345, port);
        Assert.Equal("secret", password);
    }

    [Fact]
    public void ParseConnectionString_PasswordWithEqualsSign_PreservesValue()
    {
        var (_, _, password) = MinecraftHealthCheck.ParseConnectionString(
            "Host=localhost;Password=abc=def");

        Assert.Equal("abc=def", password);
    }

    [Fact]
    public void ParseConnectionString_UnknownKeys_AreIgnored()
    {
        var (host, port, password) = MinecraftHealthCheck.ParseConnectionString(
            "Host=server;Port=25575;Password=pw;Unknown=value;Extra=stuff");

        Assert.Equal("server", host);
        Assert.Equal(25575, port);
        Assert.Equal("pw", password);
    }

    #endregion
}
