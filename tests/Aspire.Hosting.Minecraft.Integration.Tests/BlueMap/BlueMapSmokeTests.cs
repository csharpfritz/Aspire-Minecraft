using Aspire.Hosting.Minecraft.Integration.Tests.Fixtures;
using Xunit;

namespace Aspire.Hosting.Minecraft.Integration.Tests.BlueMap;

/// <summary>
/// Smoke tests verifying the BlueMap web UI is running and returns 200 OK.
/// These do not verify rendering correctness — they confirm BlueMap is reachable.
/// </summary>
[Collection("Minecraft")]
[Trait("Category", "Integration")]
public class BlueMapSmokeTests(MinecraftAppFixture fixture)
{
    [Fact]
    public async Task BlueMap_RootPage_Returns200()
    {
        if (string.IsNullOrEmpty(fixture.BlueMapUrl))
        {
            Assert.Fail("BlueMap URL not available — was WithBlueMap() configured in the AppHost?");
            return;
        }

        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(fixture.BlueMapUrl);

        Assert.True(
            response.IsSuccessStatusCode,
            $"BlueMap root page returned {(int)response.StatusCode} {response.StatusCode}");
    }

    [Fact]
    public async Task BlueMap_SettingsJson_ContainsMaps()
    {
        if (string.IsNullOrEmpty(fixture.BlueMapUrl))
        {
            Assert.Fail("BlueMap URL not available — was WithBlueMap() configured in the AppHost?");
            return;
        }

        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync($"{fixture.BlueMapUrl}/settings.json");

        Assert.True(response.IsSuccessStatusCode,
            $"BlueMap settings.json returned {(int)response.StatusCode}");

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("maps", content);
    }
}
