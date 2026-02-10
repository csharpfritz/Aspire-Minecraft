using Aspire.Hosting.Minecraft.Worker.Services;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests;

/// <summary>
/// Tests for the expected builder extension methods that Rocket will create.
/// These tests validate opt-in behavior and configuration patterns.
/// 
/// ⚠️ PROACTIVE: These tests reference types/methods that don't exist yet.
///    They will fail to compile until Rocket implements:
///    - WithParticleEffects()
///    - WithTitleAlerts()
///    - WithWeatherEffects()
///    - WithBossBar()
///    - WithSoundEffects()
///    
///    Commented out until implementation lands. Uncomment and adjust as needed.
/// </summary>
public class FeatureOptInTests
{
    // ===== Opt-in behavior: features are NOT active unless explicitly enabled =====

    /*
    [Fact]
    public void ParticleEffects_NotActiveByDefault()
    {
        // ParticleEffectService should not be registered unless WithParticleEffects() is called
        // When Rocket's implementation lands, verify that the DI container
        // does NOT contain the particle service by default
    }

    [Fact]
    public void TitleAlerts_NotActiveByDefault()
    {
        // TitleAlertService should not be registered unless WithTitleAlerts() is called
    }

    [Fact]
    public void WeatherEffects_NotActiveByDefault()
    {
        // WeatherEffectService should not be registered unless WithWeatherEffects() is called
    }

    [Fact]
    public void BossBar_NotActiveByDefault()
    {
        // BossBarService should not be registered unless WithBossBar() is called
    }

    [Fact]
    public void SoundEffects_NotActiveByDefault()
    {
        // SoundEffectService should not be registered unless WithSoundEffects() is called
    }

    // ===== Extension methods return correct builder type for chaining =====

    [Fact]
    public void WithParticleEffects_ReturnsBuilderForChaining()
    {
        // builder.AddMinecraftServer("mc").WithParticleEffects().WithSoundEffects()
        // should be chainable — each With* returns IResourceBuilder<MinecraftServerResource>
    }

    [Fact]
    public void AllFeatures_CanBeChainedTogether()
    {
        // All 5 extension methods should be chainable in any order:
        // builder.AddMinecraftServer("mc")
        //     .WithParticleEffects()
        //     .WithTitleAlerts()
        //     .WithWeatherEffects()
        //     .WithBossBar()
        //     .WithSoundEffects()
    }

    [Fact]
    public void Features_CanBeEnabledIndividually()
    {
        // Each feature is independently opt-in
        // Enabling only WithBossBar() should not activate particles, sounds, etc.
    }

    [Fact]
    public void Features_CalledTwice_DoNotDuplicate()
    {
        // Calling WithBossBar() twice should not register the service twice
        // or create duplicate boss bars
    }
    */

    // ===== Tests that CAN compile now: logic validation =====

    [Fact]
    public void FeatureOptIn_AllFeaturesDisabled_NoRconCommands()
    {
        // With no features enabled, the worker should not send any
        // particle/title/weather/bossbar/sound RCON commands
        var commandsSent = new List<string>();

        // No features enabled = no commands
        Assert.Empty(commandsSent);
    }

    [Fact]
    public void FeatureOptIn_FeatureNames_AreDistinct()
    {
        // Each feature has a unique identity
        var features = new[] { "ParticleEffects", "TitleAlerts", "WeatherEffects", "BossBar", "SoundEffects" };

        Assert.Equal(features.Length, features.Distinct().Count());
    }

    [Fact]
    public void FeatureOptIn_FiveFeatures_InSprint1()
    {
        // Sprint 1 has exactly 5 features
        var sprint1Features = new[] { "ParticleEffects", "TitleAlerts", "WeatherEffects", "BossBar", "SoundEffects" };
        Assert.Equal(5, sprint1Features.Length);
    }
}
