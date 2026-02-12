using Aspire.Hosting.Minecraft.Worker.Services;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests.Services;

public class StructureBuilderTypeTests
{
    [Fact]
    public void IsDatabaseResource_Postgres_ReturnsTrue()
    {
        Assert.True(StructureBuilder.IsDatabaseResource("postgres"));
    }

    [Fact]
    public void IsDatabaseResource_Redis_ReturnsTrue()
    {
        Assert.True(StructureBuilder.IsDatabaseResource("redis"));
    }

    [Fact]
    public void IsDatabaseResource_SqlServer_ReturnsTrue()
    {
        Assert.True(StructureBuilder.IsDatabaseResource("sqlserver"));
    }

    [Fact]
    public void IsDatabaseResource_WebApi_ReturnsFalse()
    {
        Assert.False(StructureBuilder.IsDatabaseResource("WebApi"));
    }

    [Fact]
    public void IsAzureResource_AzureServiceBus_ReturnsTrue()
    {
        Assert.True(StructureBuilder.IsAzureResource("azure-servicebus"));
    }

    [Fact]
    public void IsAzureResource_RegularContainer_ReturnsFalse()
    {
        Assert.False(StructureBuilder.IsAzureResource("container"));
    }

    [Fact]
    public void GetStructureType_DatabaseResource_ReturnsCylinder()
    {
        Assert.Equal("Cylinder", StructureBuilder.GetStructureType("postgres"));
    }

    [Fact]
    public void GetStructureType_AzureNonDatabase_ReturnsAzureThemed()
    {
        Assert.Equal("AzureThemed", StructureBuilder.GetStructureType("azure-servicebus"));
    }

    [Fact]
    public void GetStructureType_AzureDatabase_ReturnsCylinder()
    {
        // Database detection wins over Azure detection
        Assert.Equal("Cylinder", StructureBuilder.GetStructureType("cosmosdb"));
    }

    [Fact]
    public void GetStructureType_Project_ReturnsWatchtower()
    {
        Assert.Equal("Watchtower", StructureBuilder.GetStructureType("Project"));
    }

    [Fact]
    public void GetStructureType_Container_ReturnsWarehouse()
    {
        Assert.Equal("Warehouse", StructureBuilder.GetStructureType("Container"));
    }
}
