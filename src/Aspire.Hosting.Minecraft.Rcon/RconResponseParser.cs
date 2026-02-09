using System.Text.RegularExpressions;

namespace Aspire.Hosting.Minecraft.Rcon;

/// <summary>
/// Parses Minecraft server RCON responses into structured data.
/// </summary>
public static partial class RconResponseParser
{
    /// <summary>
    /// Strips Minecraft color/formatting codes (§ followed by a character).
    /// </summary>
    public static string StripColorCodes(string input) =>
        ColorCodeRegex().Replace(input, "");

    /// <summary>
    /// Parses TPS from the "tps" command response.
    /// Returns TPS values for 1m, 5m, 15m intervals.
    /// </summary>
    public static TpsResult ParseTps(string response)
    {
        var clean = StripColorCodes(response);
        var matches = DecimalRegex().Matches(clean);

        return matches.Count >= 3
            ? new TpsResult(
                double.Parse(matches[0].Value),
                double.Parse(matches[1].Value),
                double.Parse(matches[2].Value))
            : new TpsResult(20.0, 20.0, 20.0);
    }

    /// <summary>
    /// Parses MSPT from the "mspt" command response.
    /// Returns MSPT values for 5s, 10s, 60s intervals.
    /// </summary>
    public static MsptResult ParseMspt(string response)
    {
        var clean = StripColorCodes(response);
        var matches = DecimalRegex().Matches(clean);

        return matches.Count >= 3
            ? new MsptResult(
                double.Parse(matches[0].Value),
                double.Parse(matches[1].Value),
                double.Parse(matches[2].Value))
            : new MsptResult(0, 0, 0);
    }

    /// <summary>
    /// Parses player list from the "list" command response.
    /// Example: "There are 3 of a max of 20 players online: Steve, Alex, Notch"
    /// </summary>
    public static PlayerListResult ParsePlayerList(string response)
    {
        var clean = StripColorCodes(response);
        var match = PlayerListRegex().Match(clean);

        if (!match.Success)
            return new PlayerListResult(0, 20, []);

        var online = int.Parse(match.Groups[1].Value);
        var max = int.Parse(match.Groups[2].Value);
        var playersPart = match.Groups[3].Value.Trim();
        var players = string.IsNullOrEmpty(playersPart)
            ? []
            : playersPart.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        return new PlayerListResult(online, max, players);
    }

    /// <summary>
    /// Parses world list from the "worlds" command response (Paper servers).
    /// </summary>
    public static WorldListResult ParseWorldList(string response)
    {
        var clean = StripColorCodes(response);
        var lines = clean.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var worlds = new List<string>();
        foreach (var line in lines)
        {
            // Lines like "- world (DIM0): Loaded" or just world names
            var match = WorldLineRegex().Match(line);
            if (match.Success)
                worlds.Add(match.Groups[1].Value);
        }

        return new WorldListResult(worlds.ToArray());
    }

    [GeneratedRegex(@"§.")]
    private static partial Regex ColorCodeRegex();

    [GeneratedRegex(@"\d+\.?\d*")]
    private static partial Regex DecimalRegex();

    [GeneratedRegex(@"There are (\d+) of a max of (\d+) players online:\s*(.*)")]
    private static partial Regex PlayerListRegex();

    [GeneratedRegex(@"[-\s]*(\w[\w_]*)")]
    private static partial Regex WorldLineRegex();
}

public readonly record struct TpsResult(double OneMinute, double FiveMinute, double FifteenMinute);
public readonly record struct MsptResult(double FiveSecond, double TenSecond, double SixtySecond);
public readonly record struct PlayerListResult(int Online, int Max, string[] Players);
public readonly record struct WorldListResult(string[] Worlds);
