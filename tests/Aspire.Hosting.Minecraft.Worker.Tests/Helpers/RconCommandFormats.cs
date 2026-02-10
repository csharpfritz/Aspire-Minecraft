namespace Aspire.Hosting.Minecraft.Worker.Tests.Helpers;

/// <summary>
/// Expected RCON command formats for Sprint 1 features.
/// Validates command strings match Minecraft command syntax.
/// These are the command patterns Rocket's implementations should produce.
/// </summary>
public static class RconCommandFormats
{
    // Particle Effects (#3)
    public static string Particle(string particleName, int x, int y, int z, double dx, double dy, double dz, double speed, int count)
        => $"particle {particleName} {x} {y} {z} {dx} {dy} {dz} {speed} {count}";

    // Title Screen Alerts (#5)
    public static string TitleShow(string selector, string jsonText)
        => $"title {selector} title {jsonText}";

    public static string TitleSubtitle(string selector, string jsonText)
        => $"title {selector} subtitle {jsonText}";

    public static string TitleTimes(string selector, int fadeIn, int stay, int fadeOut)
        => $"title {selector} times {fadeIn} {stay} {fadeOut}";

    public static string TitleClear(string selector)
        => $"title {selector} clear";

    // Weather = System Health (#7)
    public static string Weather(string weatherType)
        => $"weather {weatherType}";

    public static string WeatherClear() => "weather clear";
    public static string WeatherRain() => "weather rain";
    public static string WeatherThunder() => "weather thunder";

    // Boss Bar Health Meter (#8)
    public static string BossBarAdd(string id, string nameJson)
        => $"bossbar add {id} {nameJson}";

    public static string BossBarSet(string id, string property, string value)
        => $"bossbar set {id} {property} {value}";

    public static string BossBarRemove(string id)
        => $"bossbar remove {id}";

    // Sound Effects (#10)
    public static string PlaySound(string sound, string source, string selector, int x, int y, int z, float volume, float pitch)
        => $"playsound {sound} {source} {selector} {x} {y} {z} {volume} {pitch}";

    public static string PlaySound(string sound, string source, string selector)
        => $"playsound {sound} {source} {selector}";
}
