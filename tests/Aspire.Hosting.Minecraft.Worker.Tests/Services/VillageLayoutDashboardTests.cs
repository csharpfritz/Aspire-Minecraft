using Aspire.Hosting.Minecraft.Worker.Services;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests.Services;

public class VillageLayoutDashboardTests
{
    [Fact]
    public void DashboardX_IsWestOfVillage()
    {
        Assert.True(VillageLayout.DashboardX < VillageLayout.BaseX);
    }

    [Fact]
    public void DashboardColumns_HasDefaultValue()
    {
        Assert.Equal(10, VillageLayout.DashboardColumns);
    }
}
