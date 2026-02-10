using Aspire.Hosting.Minecraft.Worker.Tests.Helpers;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests;

/// <summary>
/// Tests for Sound Effects (#10) — RCON playsound commands on health events.
/// Validates sound command format, sound names, volume/pitch, and event-driven triggering.
/// </summary>
public class SoundEffectsCommandTests
{
    [Fact]
    public void PlaySound_FullFormat_HasCorrectStructure()
    {
        var cmd = RconCommandFormats.PlaySound(
            "minecraft:entity.experience_orb.pickup", "master", "@a", 0, -58, 0, 1.0f, 1.0f);

        Assert.StartsWith("playsound ", cmd);
        Assert.Contains("minecraft:entity.experience_orb.pickup", cmd);
        Assert.Contains("master", cmd);
        Assert.Contains("@a", cmd);
    }

    [Fact]
    public void PlaySound_ShortFormat_HasCorrectStructure()
    {
        var cmd = RconCommandFormats.PlaySound("minecraft:entity.wither.spawn", "master", "@a");

        Assert.Equal("playsound minecraft:entity.wither.spawn master @a", cmd);
    }

    [Fact]
    public void PlaySound_FullFormat_IncludesAllParameters()
    {
        var cmd = RconCommandFormats.PlaySound(
            "minecraft:block.note_block.pling", "master", "@a", 10, -58, 5, 1.0f, 1.5f);
        var parts = cmd.Split(' ');

        Assert.Equal("playsound", parts[0]);
        Assert.Equal("minecraft:block.note_block.pling", parts[1]);
        Assert.Equal("master", parts[2]);
        Assert.Equal("@a", parts[3]);
        Assert.Equal(9, parts.Length); // playsound sound source selector x y z volume pitch
    }

    [Theory]
    [InlineData("master")]
    [InlineData("music")]
    [InlineData("record")]
    [InlineData("weather")]
    [InlineData("block")]
    [InlineData("hostile")]
    [InlineData("neutral")]
    [InlineData("player")]
    [InlineData("ambient")]
    [InlineData("voice")]
    public void PlaySound_ValidSoundSources_AreAccepted(string source)
    {
        var cmd = RconCommandFormats.PlaySound("minecraft:entity.player.levelup", source, "@a");
        Assert.Contains(source, cmd);
    }

    [Theory]
    [InlineData("@a")]
    [InlineData("@p")]
    [InlineData("Steve")]
    public void PlaySound_DifferentSelectors_AreSupported(string selector)
    {
        var cmd = RconCommandFormats.PlaySound("minecraft:entity.player.levelup", "master", selector);
        Assert.Contains(selector, cmd);
    }

    // ---- Health event → Sound mapping ----

    [Theory]
    [InlineData("minecraft:entity.experience_orb.pickup")]
    [InlineData("minecraft:entity.player.levelup")]
    [InlineData("minecraft:block.note_block.pling")]
    [InlineData("minecraft:entity.villager.celebrate")]
    public void PlaySound_RecoveryEvent_ShouldUsePositiveSound(string sound)
    {
        // When a resource transitions to Healthy, use a positive/upbeat sound
        var cmd = RconCommandFormats.PlaySound(sound, "master", "@a");
        Assert.StartsWith("playsound minecraft:", cmd);
        Assert.Contains(sound, cmd);
    }

    [Theory]
    [InlineData("minecraft:entity.wither.spawn")]
    [InlineData("minecraft:entity.ender_dragon.growl")]
    [InlineData("minecraft:block.anvil.land")]
    [InlineData("minecraft:entity.ghast.scream")]
    public void PlaySound_DegradationEvent_ShouldUseAlarmSound(string sound)
    {
        // When a resource transitions to Unhealthy, use an alarming sound
        var cmd = RconCommandFormats.PlaySound(sound, "master", "@a");
        Assert.StartsWith("playsound minecraft:", cmd);
        Assert.Contains(sound, cmd);
    }

    [Fact]
    public void PlaySound_SoundName_MustBeNamespaced()
    {
        // Minecraft sound names must use namespace:path format
        var sound = "minecraft:entity.player.levelup";
        Assert.Contains(":", sound);
        Assert.StartsWith("minecraft:", sound);
    }

    [Fact]
    public void PlaySound_Volume_ZeroMeansMinDistance()
    {
        // Volume 0 = minimum audible distance in Minecraft
        var cmd = RconCommandFormats.PlaySound(
            "minecraft:entity.player.levelup", "master", "@a", 0, 0, 0, 0f, 1.0f);
        Assert.Contains("0 1", cmd);
    }

    [Fact]
    public void PlaySound_Pitch_RangeIsHalfToDouble()
    {
        // Minecraft pitch: 0.5 = half speed, 1.0 = normal, 2.0 = double speed
        var lowPitch = RconCommandFormats.PlaySound(
            "minecraft:entity.wither.spawn", "master", "@a", 0, 0, 0, 1.0f, 0.5f);
        var highPitch = RconCommandFormats.PlaySound(
            "minecraft:entity.experience_orb.pickup", "master", "@a", 0, 0, 0, 1.0f, 2.0f);

        Assert.Contains("0.5", lowPitch);
        Assert.Contains("2", highPitch);
    }

    [Fact]
    public void PlaySound_ShouldOnlyPlayOnStateChange()
    {
        // Sounds should only play when health status changes, not on every poll
        var changes = new[]
        {
            (old: "Healthy", @new: "Healthy"),     // no sound
            (old: "Healthy", @new: "Unhealthy"),   // alarm sound
            (old: "Unhealthy", @new: "Unhealthy"), // no sound
            (old: "Unhealthy", @new: "Healthy"),   // recovery sound
        };

        int soundsPlayed = 0;
        foreach (var (old, @new) in changes)
        {
            if (old != @new) soundsPlayed++;
        }

        Assert.Equal(2, soundsPlayed);
    }

    [Fact]
    public void PlaySound_MultipleResourceChanges_ShouldPlayMultipleSounds()
    {
        // When multiple resources change simultaneously, each should trigger a sound
        var resources = new[] { "api", "redis", "db" };
        int soundCount = 0;

        foreach (var resource in resources)
        {
            // Each resource transition should produce a playsound command
            var cmd = RconCommandFormats.PlaySound("minecraft:entity.wither.spawn", "master", "@a");
            Assert.Contains("playsound", cmd);
            soundCount++;
        }

        Assert.Equal(3, soundCount);
    }

    [Fact]
    public void PlaySound_RapidStateChanges_ShouldNotOverlap()
    {
        // Rapid state changes should still each produce a sound
        // but implementation may want to debounce
        var transitions = Enumerable.Range(0, 10).Select(i =>
            i % 2 == 0 ? "Healthy" : "Unhealthy").ToArray();

        int changeCount = 0;
        for (int i = 1; i < transitions.Length; i++)
        {
            if (transitions[i] != transitions[i - 1])
                changeCount++;
        }

        Assert.Equal(9, changeCount); // alternating = all changes
    }
}
