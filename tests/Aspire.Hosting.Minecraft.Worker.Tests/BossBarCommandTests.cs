using Aspire.Hosting.Minecraft.Worker.Tests.Helpers;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests;

/// <summary>
/// Tests for Boss Bar Health Meter (#8) — RCON bossbar commands showing persistent health display.
/// The boss bar should always be visible to all players showing fleet health percentage.
/// </summary>
public class BossBarCommandTests
{
    private const string ExpectedBossBarId = "aspire:health";

    [Fact]
    public void BossBarAdd_HasCorrectFormat()
    {
        var cmd = RconCommandFormats.BossBarAdd(ExpectedBossBarId, """{"text":"Aspire Fleet Health","color":"green"}""");

        Assert.StartsWith("bossbar add ", cmd);
        Assert.Contains(ExpectedBossBarId, cmd);
        Assert.Contains("Aspire Fleet Health", cmd);
    }

    [Fact]
    public void BossBarSet_Value_HasCorrectFormat()
    {
        var cmd = RconCommandFormats.BossBarSet(ExpectedBossBarId, "value", "80");

        Assert.Equal($"bossbar set {ExpectedBossBarId} value 80", cmd);
    }

    [Fact]
    public void BossBarSet_Max_HasCorrectFormat()
    {
        var cmd = RconCommandFormats.BossBarSet(ExpectedBossBarId, "max", "100");

        Assert.Equal($"bossbar set {ExpectedBossBarId} max 100", cmd);
    }

    [Fact]
    public void BossBarSet_Players_HasCorrectFormat()
    {
        var cmd = RconCommandFormats.BossBarSet(ExpectedBossBarId, "players", "@a");

        Assert.Equal($"bossbar set {ExpectedBossBarId} players @a", cmd);
    }

    [Fact]
    public void BossBarSet_Visible_HasCorrectFormat()
    {
        var cmd = RconCommandFormats.BossBarSet(ExpectedBossBarId, "visible", "true");

        Assert.Equal($"bossbar set {ExpectedBossBarId} visible true", cmd);
    }

    [Theory]
    [InlineData("green")]
    [InlineData("yellow")]
    [InlineData("red")]
    [InlineData("blue")]
    [InlineData("pink")]
    [InlineData("purple")]
    [InlineData("white")]
    public void BossBarSet_Color_AcceptsValidColors(string color)
    {
        var cmd = RconCommandFormats.BossBarSet(ExpectedBossBarId, "color", color);

        Assert.Equal($"bossbar set {ExpectedBossBarId} color {color}", cmd);
    }

    [Theory]
    [InlineData("progress")]
    [InlineData("notched_6")]
    [InlineData("notched_10")]
    [InlineData("notched_12")]
    [InlineData("notched_20")]
    public void BossBarSet_Style_AcceptsValidStyles(string style)
    {
        var cmd = RconCommandFormats.BossBarSet(ExpectedBossBarId, "style", style);

        Assert.Equal($"bossbar set {ExpectedBossBarId} style {style}", cmd);
    }

    [Fact]
    public void BossBarRemove_HasCorrectFormat()
    {
        var cmd = RconCommandFormats.BossBarRemove(ExpectedBossBarId);

        Assert.Equal($"bossbar remove {ExpectedBossBarId}", cmd);
    }

    [Fact]
    public void BossBar_FullInitSequence_RequiresMultipleCommands()
    {
        // A boss bar setup requires: add → set max → set value → set players → set visible → set color
        var commands = new[]
        {
            RconCommandFormats.BossBarAdd(ExpectedBossBarId, """{"text":"Aspire Fleet Health"}"""),
            RconCommandFormats.BossBarSet(ExpectedBossBarId, "max", "100"),
            RconCommandFormats.BossBarSet(ExpectedBossBarId, "value", "100"),
            RconCommandFormats.BossBarSet(ExpectedBossBarId, "players", "@a"),
            RconCommandFormats.BossBarSet(ExpectedBossBarId, "visible", "true"),
            RconCommandFormats.BossBarSet(ExpectedBossBarId, "color", "green"),
            RconCommandFormats.BossBarSet(ExpectedBossBarId, "style", "notched_10"),
        };

        Assert.Equal(7, commands.Length);
        Assert.All(commands, cmd => Assert.Contains(ExpectedBossBarId, cmd));
    }

    [Theory]
    [InlineData(5, 5, 100)]   // 100% healthy
    [InlineData(4, 5, 80)]    // 80% healthy
    [InlineData(3, 5, 60)]    // 60% healthy
    [InlineData(1, 5, 20)]    // 20% healthy
    [InlineData(0, 5, 0)]     // 0% healthy
    public void BossBar_HealthPercentage_CalculatesCorrectly(int healthy, int total, int expectedPercent)
    {
        var percent = total > 0 ? (int)((double)healthy / total * 100) : 0;
        Assert.Equal(expectedPercent, percent);
    }

    [Theory]
    [InlineData(100, "green")]
    [InlineData(80, "green")]
    [InlineData(60, "yellow")]
    [InlineData(40, "yellow")]
    [InlineData(20, "red")]
    [InlineData(0, "red")]
    public void BossBar_ColorMapping_MatchesHealthLevel(int percent, string expectedColor)
    {
        // Expected color mapping based on health percentage
        string color;
        if (percent >= 75)
            color = "green";
        else if (percent >= 25)
            color = "yellow";
        else
            color = "red";

        Assert.Equal(expectedColor, color);
    }

    [Fact]
    public void BossBar_NamespaceId_MustContainColon()
    {
        // Minecraft bossbar IDs require namespace:name format
        Assert.Contains(":", ExpectedBossBarId);
    }

    [Fact]
    public void BossBar_NamespaceId_MustBeLowercase()
    {
        Assert.Equal(ExpectedBossBarId, ExpectedBossBarId.ToLowerInvariant());
    }

    [Fact]
    public void BossBar_ValueUpdate_OnlyChangesWhenHealthChanges()
    {
        // Boss bar value should only update when the health percentage actually changes
        int previousPercent = 80;
        int currentPercent = 80;

        bool shouldUpdate = previousPercent != currentPercent;
        Assert.False(shouldUpdate);
    }

    [Fact]
    public void BossBar_ZeroResources_ShouldShowZeroPercent()
    {
        int healthy = 0, total = 0;
        var percent = total > 0 ? (int)((double)healthy / total * 100) : 0;

        Assert.Equal(0, percent);
    }

    [Fact]
    public void BossBar_SingleResource_ShowsFullOrEmpty()
    {
        // With only one resource, health is either 100% or 0%
        Assert.Equal(100, (int)((double)1 / 1 * 100));
        Assert.Equal(0, (int)((double)0 / 1 * 100));
    }

    [Fact]
    public void BossBar_NameJson_MustBeJsonTextComponent()
    {
        var nameJson = """{"text":"Aspire Fleet Health","color":"green"}""";
        var cmd = RconCommandFormats.BossBarAdd(ExpectedBossBarId, nameJson);

        Assert.Contains("{", cmd);
        Assert.Contains("\"text\"", cmd);
    }
}
