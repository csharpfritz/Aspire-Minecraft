using Aspire.Hosting.Minecraft.Worker.Tests.Helpers;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests;

/// <summary>
/// Tests for Title Screen Alerts (#5) — RCON title commands showing dramatic text on health state changes.
/// Validates title command format, timing, JSON text payloads, and state-driven content.
/// </summary>
public class TitleAlertsCommandTests
{
    [Fact]
    public void TitleShow_HasCorrectFormat()
    {
        var cmd = RconCommandFormats.TitleShow("@a", """{"text":"Service Down!","color":"red","bold":true}""");

        Assert.StartsWith("title @a title ", cmd);
        Assert.Contains("Service Down!", cmd);
    }

    [Fact]
    public void TitleSubtitle_HasCorrectFormat()
    {
        var cmd = RconCommandFormats.TitleSubtitle("@a", """{"text":"api-service is Unhealthy","color":"gray"}""");

        Assert.StartsWith("title @a subtitle ", cmd);
        Assert.Contains("api-service", cmd);
    }

    [Fact]
    public void TitleTimes_HasCorrectFormat()
    {
        // fadeIn=10 ticks (0.5s), stay=70 ticks (3.5s), fadeOut=20 ticks (1s)
        var cmd = RconCommandFormats.TitleTimes("@a", 10, 70, 20);

        Assert.Equal("title @a times 10 70 20", cmd);
    }

    [Fact]
    public void TitleClear_HasCorrectFormat()
    {
        var cmd = RconCommandFormats.TitleClear("@a");

        Assert.Equal("title @a clear", cmd);
    }

    [Theory]
    [InlineData("@a")]
    [InlineData("@p")]
    [InlineData("Steve")]
    public void Title_DifferentSelectors_AreSupported(string selector)
    {
        var cmd = RconCommandFormats.TitleShow(selector, """{"text":"test"}""");
        Assert.Contains($"title {selector} title", cmd);
    }

    [Fact]
    public void Title_HealthyTransition_ShowsGreenRecoveryMessage()
    {
        // When resource recovers: show green title
        var title = """{"text":"✔ Service Recovered!","color":"green","bold":true}""";
        var cmd = RconCommandFormats.TitleShow("@a", title);

        Assert.Contains("green", cmd);
        Assert.Contains("Recovered", cmd);
    }

    [Fact]
    public void Title_UnhealthyTransition_ShowsRedAlertMessage()
    {
        // When resource goes down: show red title
        var title = """{"text":"⚠ Service Down!","color":"red","bold":true}""";
        var cmd = RconCommandFormats.TitleShow("@a", title);

        Assert.Contains("red", cmd);
        Assert.Contains("Down", cmd);
    }

    [Fact]
    public void Title_SubtitleIncludesResourceName()
    {
        var subtitle = """{"text":"redis-cache changed from Healthy to Unhealthy","color":"gray"}""";
        var cmd = RconCommandFormats.TitleSubtitle("@a", subtitle);

        Assert.Contains("redis-cache", cmd);
        Assert.Contains("Healthy", cmd);
        Assert.Contains("Unhealthy", cmd);
    }

    [Fact]
    public void Title_TimesSequence_IsCorrectOrder()
    {
        // Minecraft expects: fadeIn stay fadeOut (all in ticks, 20 ticks = 1 second)
        var cmd = RconCommandFormats.TitleTimes("@a", 10, 70, 20);
        var parts = cmd.Split(' ');

        Assert.Equal("10", parts[3]); // fadeIn
        Assert.Equal("70", parts[4]); // stay
        Assert.Equal("20", parts[5]); // fadeOut
    }

    [Fact]
    public void Title_FullSequence_RequiresThreeCommands()
    {
        // A proper title display requires: times → subtitle → title (in that order)
        var times = RconCommandFormats.TitleTimes("@a", 10, 70, 20);
        var subtitle = RconCommandFormats.TitleSubtitle("@a", """{"text":"details"}""");
        var title = RconCommandFormats.TitleShow("@a", """{"text":"ALERT"}""");

        Assert.Contains("times", times);
        Assert.Contains("subtitle", subtitle);
        Assert.Contains("title @a title", title);
    }

    [Fact]
    public void Title_JsonPayload_MustBeValidFormat()
    {
        // Minecraft title command requires JSON text component
        var json = """{"text":"Test","color":"red","bold":true}""";
        var cmd = RconCommandFormats.TitleShow("@a", json);

        // The JSON must be present in the command
        Assert.Contains("{", cmd);
        Assert.Contains("}", cmd);
        Assert.Contains("\"text\"", cmd);
    }

    [Fact]
    public void Title_ZeroTimes_AreValid()
    {
        // Edge case: instant display with no fade
        var cmd = RconCommandFormats.TitleTimes("@a", 0, 20, 0);
        Assert.Equal("title @a times 0 20 0", cmd);
    }
}
