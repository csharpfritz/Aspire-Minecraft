using Xunit;

namespace Aspire.Hosting.Minecraft.Rcon.Tests;

public class RconResponseParserTests
{
    #region StripColorCodes

    [Fact]
    public void StripColorCodes_RemovesMinecraftFormattingCodes()
    {
        var input = "§aHello §bWorld";
        var result = RconResponseParser.StripColorCodes(input);
        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void StripColorCodes_ReturnsUnchangedWhenNoCodes()
    {
        Assert.Equal("Hello World", RconResponseParser.StripColorCodes("Hello World"));
    }

    [Fact]
    public void StripColorCodes_HandlesEmptyString()
    {
        Assert.Empty(RconResponseParser.StripColorCodes(""));
    }

    [Fact]
    public void StripColorCodes_HandlesMultipleConsecutiveCodes()
    {
        Assert.Equal("Text", RconResponseParser.StripColorCodes("§a§b§cText"));
    }

    [Fact]
    public void StripColorCodes_RemovesBoldItalicResetCodes()
    {
        // §l = bold, §o = italic, §r = reset
        var input = "§lBold§r Normal §oItalic";
        Assert.Equal("Bold Normal Italic", RconResponseParser.StripColorCodes(input));
    }

    #endregion

    #region ParseTps

    [Fact]
    public void ParseTps_ValidResponse_ReturnsThreeValues()
    {
        var response = "19.98, 19.95, 20.0";
        var result = RconResponseParser.ParseTps(response);
        Assert.Equal(19.98, result.OneMinute);
        Assert.Equal(19.95, result.FiveMinute);
        Assert.Equal(20.0, result.FifteenMinute);
    }

    [Fact]
    public void ParseTps_IntegerValues_ParsesCorrectly()
    {
        var response = "20, 19, 18";
        var result = RconResponseParser.ParseTps(response);
        Assert.Equal(20.0, result.OneMinute);
        Assert.Equal(19.0, result.FiveMinute);
        Assert.Equal(18.0, result.FifteenMinute);
    }

    [Fact]
    public void ParseTps_FewerThanThreeDecimals_ReturnsDefault()
    {
        var response = "Only one: 19.5";
        var result = RconResponseParser.ParseTps(response);
        Assert.Equal(20.0, result.OneMinute);
        Assert.Equal(20.0, result.FiveMinute);
        Assert.Equal(20.0, result.FifteenMinute);
    }

    [Fact]
    public void ParseTps_EmptyString_ReturnsDefault()
    {
        var result = RconResponseParser.ParseTps("");
        Assert.Equal(20.0, result.OneMinute);
        Assert.Equal(20.0, result.FiveMinute);
        Assert.Equal(20.0, result.FifteenMinute);
    }

    [Fact]
    public void ParseTps_NoNumbers_ReturnsDefault()
    {
        var result = RconResponseParser.ParseTps("No numbers here!");
        Assert.Equal(20.0, result.OneMinute);
        Assert.Equal(20.0, result.FiveMinute);
        Assert.Equal(20.0, result.FifteenMinute);
    }

    [Fact]
    public void ParseTps_WithColorCodes_StripsBeforeParsing()
    {
        // Use a response where color codes surround the values but no extra digits
        var response = "§a19.5§f, §e18.2§f, §a20.0";
        var result = RconResponseParser.ParseTps(response);
        Assert.Equal(19.5, result.OneMinute);
        Assert.Equal(18.2, result.FiveMinute);
        Assert.Equal(20.0, result.FifteenMinute);
    }

    [Fact]
    public void ParseTps_LowTpsValues_ParsesCorrectly()
    {
        var response = "5.2, 3.8, 1.1";
        var result = RconResponseParser.ParseTps(response);
        Assert.Equal(5.2, result.OneMinute);
        Assert.Equal(3.8, result.FiveMinute);
        Assert.Equal(1.1, result.FifteenMinute);
    }

    #endregion

    #region ParseMspt

    [Fact]
    public void ParseMspt_ValidResponse_ReturnsThreeValues()
    {
        var response = "12.5, 14.3, 11.8";
        var result = RconResponseParser.ParseMspt(response);
        Assert.Equal(12.5, result.FiveSecond);
        Assert.Equal(14.3, result.TenSecond);
        Assert.Equal(11.8, result.SixtySecond);
    }

    [Fact]
    public void ParseMspt_WithColorCodes_StripsBeforeParsing()
    {
        var response = "§a5.2§f, §e10.3§f, §c45.1";
        var result = RconResponseParser.ParseMspt(response);
        Assert.Equal(5.2, result.FiveSecond);
        Assert.Equal(10.3, result.TenSecond);
        Assert.Equal(45.1, result.SixtySecond);
    }

    [Fact]
    public void ParseMspt_FewerThanThreeDecimals_ReturnsDefaultZero()
    {
        var result = RconResponseParser.ParseMspt("Just 5.0");
        Assert.Equal(0.0, result.FiveSecond);
        Assert.Equal(0.0, result.TenSecond);
        Assert.Equal(0.0, result.SixtySecond);
    }

    [Fact]
    public void ParseMspt_EmptyString_ReturnsDefaultZero()
    {
        var result = RconResponseParser.ParseMspt("");
        Assert.Equal(0.0, result.FiveSecond);
        Assert.Equal(0.0, result.TenSecond);
        Assert.Equal(0.0, result.SixtySecond);
    }

    [Fact]
    public void ParseMspt_HighValues_ParsesCorrectly()
    {
        var response = "50.0, 48.5, 55.2";
        var result = RconResponseParser.ParseMspt(response);
        Assert.Equal(50.0, result.FiveSecond);
        Assert.Equal(48.5, result.TenSecond);
        Assert.Equal(55.2, result.SixtySecond);
    }

    #endregion

    #region ParsePlayerList

    [Fact]
    public void ParsePlayerList_PlayersOnline_ReturnsCorrectData()
    {
        var response = "There are 3 of a max of 20 players online: Steve, Alex, Notch";
        var result = RconResponseParser.ParsePlayerList(response);
        Assert.Equal(3, result.Online);
        Assert.Equal(20, result.Max);
        Assert.Equivalent(new[] { "Steve", "Alex", "Notch" }, result.Players);
    }

    [Fact]
    public void ParsePlayerList_SinglePlayer_ReturnsSingleElement()
    {
        var response = "There are 1 of a max of 20 players online: Steve";
        var result = RconResponseParser.ParsePlayerList(response);
        Assert.Equal(1, result.Online);
        Assert.Equal(20, result.Max);
        Assert.Equivalent(new[] { "Steve" }, result.Players);
    }

    [Fact]
    public void ParsePlayerList_NoPlayersOnline_ReturnsEmptyList()
    {
        var response = "There are 0 of a max of 20 players online: ";
        var result = RconResponseParser.ParsePlayerList(response);
        Assert.Equal(0, result.Online);
        Assert.Equal(20, result.Max);
        Assert.Empty(result.Players);
    }

    [Fact]
    public void ParsePlayerList_MalformedResponse_ReturnsDefault()
    {
        var result = RconResponseParser.ParsePlayerList("Something unexpected");
        Assert.Equal(0, result.Online);
        Assert.Equal(20, result.Max);
        Assert.Empty(result.Players);
    }

    [Fact]
    public void ParsePlayerList_EmptyString_ReturnsDefault()
    {
        var result = RconResponseParser.ParsePlayerList("");
        Assert.Equal(0, result.Online);
        Assert.Equal(20, result.Max);
        Assert.Empty(result.Players);
    }

    [Fact]
    public void ParsePlayerList_WithColorCodes_ParsesCorrectly()
    {
        var response = "§6There are §a3§6 of a max of §a20§6 players online: §aSteve§f, §aAlex";
        var result = RconResponseParser.ParsePlayerList(response);
        Assert.Equal(3, result.Online);
        Assert.Equal(20, result.Max);
        Assert.Equal(2, result.Players.Length);
    }

    [Fact]
    public void ParsePlayerList_LargePlayerCount_ParsesCorrectly()
    {
        var response = "There are 100 of a max of 500 players online: A, B, C";
        var result = RconResponseParser.ParsePlayerList(response);
        Assert.Equal(100, result.Online);
        Assert.Equal(500, result.Max);
        Assert.Equal(3, result.Players.Length);
    }

    #endregion

    #region ParseWorldList

    [Fact]
    public void ParseWorldList_MultipleWorlds_ReturnsAll()
    {
        var response = "- world\n- world_nether\n- world_the_end";
        var result = RconResponseParser.ParseWorldList(response);
        Assert.Equivalent(new[] { "world", "world_nether", "world_the_end" }, result.Worlds);
    }

    [Fact]
    public void ParseWorldList_SingleWorld_ReturnsSingle()
    {
        var response = "- world";
        var result = RconResponseParser.ParseWorldList(response);
        Assert.Equivalent(new[] { "world" }, result.Worlds);
    }

    [Fact]
    public void ParseWorldList_EmptyResponse_ReturnsEmpty()
    {
        var result = RconResponseParser.ParseWorldList("");
        Assert.Empty(result.Worlds);
    }

    [Fact]
    public void ParseWorldList_WithColorCodes_StripsBeforeParsing()
    {
        var response = "§a- world§r\n§a- world_nether§r";
        var result = RconResponseParser.ParseWorldList(response);
        Assert.Contains("world", result.Worlds);
        Assert.Contains("world_nether", result.Worlds);
    }

    #endregion

    #region Record struct equality

    [Fact]
    public void TpsResult_RecordEquality_WorksCorrectly()
    {
        var a = new TpsResult(20.0, 19.5, 18.0);
        var b = new TpsResult(20.0, 19.5, 18.0);
        Assert.Equal(b, a);
    }

    [Fact]
    public void MsptResult_RecordEquality_WorksCorrectly()
    {
        var a = new MsptResult(5.0, 10.0, 15.0);
        var b = new MsptResult(5.0, 10.0, 15.0);
        Assert.Equal(a, b);
    }

    [Fact]
    public void PlayerListResult_HasExpectedProperties()
    {
        var result = new PlayerListResult(5, 20, ["A", "B", "C", "D", "E"]);
        Assert.Equal(5, result.Online);
        Assert.Equal(20, result.Max);
        Assert.Equal(5, result.Players.Length);
    }

    [Fact]
    public void WorldListResult_HasExpectedProperties()
    {
        var result = new WorldListResult(["overworld", "nether"]);
        Assert.Equal(2, result.Worlds.Length);
    }

    #endregion
}
