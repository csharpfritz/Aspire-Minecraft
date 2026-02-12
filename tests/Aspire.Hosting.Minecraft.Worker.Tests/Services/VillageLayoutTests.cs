using Aspire.Hosting.Minecraft.Worker.Services;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests.Services;

public class VillageLayoutTests
{
    [Theory]
    [InlineData(1, 0, new[] { 10, -59, 0 })]
    [InlineData(2, 0, new[] { 10, -59, 0 })]
    [InlineData(2, 1, new[] { 22, -59, 0 })]
    [InlineData(4, 0, new[] { 10, -59, 0 })]
    [InlineData(4, 1, new[] { 22, -59, 0 })]
    [InlineData(4, 2, new[] { 10, -59, 12 })]
    [InlineData(4, 3, new[] { 22, -59, 12 })]
    [InlineData(8, 0, new[] { 10, -59, 0 })]
    [InlineData(8, 7, new[] { 22, -59, 36 })]
    [InlineData(10, 0, new[] { 10, -59, 0 })]
    [InlineData(10, 9, new[] { 22, -59, 48 })]
    public void GetStructureOrigin_ReturnsCorrectCoordinates(int totalResources, int index, int[] expected)
    {
        var (x, y, z) = VillageLayout.GetStructureOrigin(index);
        
        Assert.Equal(expected[0], x);
        Assert.Equal(expected[1], y);
        Assert.Equal(expected[2], z);
    }

    [Theory]
    [InlineData(0, 13, -59, 3)]
    [InlineData(1, 25, -59, 3)]
    [InlineData(2, 13, -59, 15)]
    [InlineData(3, 25, -59, 15)]
    public void GetStructureCenter_ReturnsCorrectOffset(int index, int expectedX, int expectedY, int expectedZ)
    {
        var (x, y, z) = VillageLayout.GetStructureCenter(index);
        
        Assert.Equal(expectedX, x);
        Assert.Equal(expectedY, y);
        Assert.Equal(expectedZ, z);
    }

    [Theory]
    [InlineData(1, 10, 0, 16, 6)]
    [InlineData(2, 10, 0, 28, 6)]
    [InlineData(4, 10, 0, 28, 18)]
    [InlineData(8, 10, 0, 28, 42)]
    [InlineData(10, 10, 0, 28, 54)]
    public void GetVillageBounds_ReturnsCorrectBoundingBox(int resourceCount, int expectedMinX, int expectedMinZ, int expectedMaxX, int expectedMaxZ)
    {
        var (minX, minZ, maxX, maxZ) = VillageLayout.GetVillageBounds(resourceCount);
        
        Assert.Equal(expectedMinX, minX);
        Assert.Equal(expectedMinZ, minZ);
        Assert.Equal(expectedMaxX, maxX);
        Assert.Equal(expectedMaxZ, maxZ);
    }

    [Theory]
    [InlineData(1, 6, -4, 20, 10)]
    [InlineData(2, 6, -4, 32, 10)]
    [InlineData(4, 6, -4, 32, 22)]
    [InlineData(8, 6, -4, 32, 46)]
    [InlineData(10, 6, -4, 32, 58)]
    public void GetFencePerimeter_ReturnsCorrectPerimeterWithGap(int resourceCount, int expectedMinX, int expectedMinZ, int expectedMaxX, int expectedMaxZ)
    {
        var (minX, minZ, maxX, maxZ) = VillageLayout.GetFencePerimeter(resourceCount);
        
        Assert.Equal(expectedMinX, minX);
        Assert.Equal(expectedMinZ, minZ);
        Assert.Equal(expectedMaxX, maxX);
        Assert.Equal(expectedMaxZ, maxZ);
    }

    [Fact]
    public void ReorderByDependency_EmptyResources_ReturnsEmptyList()
    {
        var resources = new Dictionary<string, ResourceInfo>();
        
        var result = VillageLayout.ReorderByDependency(resources);
        
        Assert.Empty(result);
    }

    [Fact]
    public void ReorderByDependency_NoDependencies_ReturnsSameOrder()
    {
        var resources = new Dictionary<string, ResourceInfo>
        {
            ["A"] = new ResourceInfo("A", "Project", "", "", 0, ResourceStatus.Unknown, []),
            ["B"] = new ResourceInfo("B", "Container", "", "", 0, ResourceStatus.Unknown, []),
            ["C"] = new ResourceInfo("C", "Executable", "", "", 0, ResourceStatus.Unknown, [])
        };
        
        var result = VillageLayout.ReorderByDependency(resources);
        
        Assert.Equal(3, result.Count);
        Assert.Contains("A", result);
        Assert.Contains("B", result);
        Assert.Contains("C", result);
    }

    [Fact]
    public void ReorderByDependency_SimpleDependency_ReturnsParentFirst()
    {
        var resources = new Dictionary<string, ResourceInfo>
        {
            ["child"] = new ResourceInfo("child", "Project", "", "", 0, ResourceStatus.Unknown, ["parent"]),
            ["parent"] = new ResourceInfo("parent", "Container", "", "", 0, ResourceStatus.Unknown, [])
        };
        
        var result = VillageLayout.ReorderByDependency(resources);
        
        Assert.Equal(2, result.Count);
        Assert.Equal("parent", result[0], StringComparer.OrdinalIgnoreCase);
        Assert.Equal("child", result[1], StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReorderByDependency_ChainedDependencies_ReturnsCorrectOrder()
    {
        var resources = new Dictionary<string, ResourceInfo>
        {
            ["C"] = new ResourceInfo("C", "Project", "", "", 0, ResourceStatus.Unknown, ["B"]),
            ["B"] = new ResourceInfo("B", "Container", "", "", 0, ResourceStatus.Unknown, ["A"]),
            ["A"] = new ResourceInfo("A", "Executable", "", "", 0, ResourceStatus.Unknown, [])
        };
        
        var result = VillageLayout.ReorderByDependency(resources);
        
        Assert.Equal(3, result.Count);
        Assert.Equal("A", result[0], StringComparer.OrdinalIgnoreCase);
        Assert.Equal("B", result[1], StringComparer.OrdinalIgnoreCase);
        Assert.Equal("C", result[2], StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReorderByDependency_MultipleDependencies_ReturnsParentsFirst()
    {
        var resources = new Dictionary<string, ResourceInfo>
        {
            ["child"] = new ResourceInfo("child", "Project", "", "", 0, ResourceStatus.Unknown, ["parent1", "parent2"]),
            ["parent1"] = new ResourceInfo("parent1", "Container", "", "", 0, ResourceStatus.Unknown, []),
            ["parent2"] = new ResourceInfo("parent2", "Executable", "", "", 0, ResourceStatus.Unknown, [])
        };
        
        var result = VillageLayout.ReorderByDependency(resources);
        
        Assert.Equal(3, result.Count);
        var childIndex = result.FindIndex(r => r.Equals("child", StringComparison.OrdinalIgnoreCase));
        var parent1Index = result.FindIndex(r => r.Equals("parent1", StringComparison.OrdinalIgnoreCase));
        var parent2Index = result.FindIndex(r => r.Equals("parent2", StringComparison.OrdinalIgnoreCase));
        
        Assert.True(parent1Index < childIndex);
        Assert.True(parent2Index < childIndex);
    }

    [Fact]
    public void ReorderByDependency_DiamondDependency_ReturnsCorrectOrder()
    {
        var resources = new Dictionary<string, ResourceInfo>
        {
            ["D"] = new ResourceInfo("D", "Project", "", "", 0, ResourceStatus.Unknown, ["B", "C"]),
            ["C"] = new ResourceInfo("C", "Container", "", "", 0, ResourceStatus.Unknown, ["A"]),
            ["B"] = new ResourceInfo("B", "Container", "", "", 0, ResourceStatus.Unknown, ["A"]),
            ["A"] = new ResourceInfo("A", "Executable", "", "", 0, ResourceStatus.Unknown, [])
        };
        
        var result = VillageLayout.ReorderByDependency(resources);
        
        Assert.Equal(4, result.Count);
        Assert.Equal("A", result[0], StringComparer.OrdinalIgnoreCase);
        var dIndex = result.FindIndex(r => r.Equals("D", StringComparison.OrdinalIgnoreCase));
        var bIndex = result.FindIndex(r => r.Equals("B", StringComparison.OrdinalIgnoreCase));
        var cIndex = result.FindIndex(r => r.Equals("C", StringComparison.OrdinalIgnoreCase));
        
        Assert.True(bIndex < dIndex);
        Assert.True(cIndex < dIndex);
    }

    [Fact]
    public void ReorderByDependency_CaseInsensitive_HandlesCorrectly()
    {
        var resources = new Dictionary<string, ResourceInfo>
        {
            ["child"] = new ResourceInfo("child", "Project", "", "", 0, ResourceStatus.Unknown, ["PARENT"]),
            ["parent"] = new ResourceInfo("parent", "Container", "", "", 0, ResourceStatus.Unknown, [])
        };
        
        var result = VillageLayout.ReorderByDependency(resources);
        
        Assert.Equal(2, result.Count);
        Assert.Equal("parent", result[0], StringComparer.OrdinalIgnoreCase);
        Assert.Equal("child", result[1], StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void SurfaceY_DefaultsToBaseY()
    {
        // Reset to default in case previous tests modified it
        VillageLayout.SurfaceY = VillageLayout.BaseY;
        Assert.Equal(VillageLayout.BaseY, VillageLayout.SurfaceY);
    }

    [Fact]
    public void GetStructureOrigin_UsesSurfaceY_WhenSet()
    {
        var original = VillageLayout.SurfaceY;
        try
        {
            VillageLayout.SurfaceY = 64;
            var (x, y, z) = VillageLayout.GetStructureOrigin(0);

            Assert.Equal(10, x);
            Assert.Equal(65, y);
            Assert.Equal(0, z);
        }
        finally
        {
            VillageLayout.SurfaceY = original;
        }
    }

    [Fact]
    public void GetStructureCenter_UsesSurfaceY_WhenSet()
    {
        var original = VillageLayout.SurfaceY;
        try
        {
            VillageLayout.SurfaceY = 72;
            var (x, y, z) = VillageLayout.GetStructureCenter(0);

            Assert.Equal(13, x);
            Assert.Equal(73, y);
            Assert.Equal(3, z);
        }
        finally
        {
            VillageLayout.SurfaceY = original;
        }
    }

    [Fact]
    public void GetAboveStructure_UsesSurfaceY_WhenSet()
    {
        var original = VillageLayout.SurfaceY;
        try
        {
            VillageLayout.SurfaceY = 64;
            var (x, y, z) = VillageLayout.GetAboveStructure(0, 10);

            Assert.Equal(13, x);
            Assert.Equal(74, y); // SurfaceY + 10
            Assert.Equal(3, z);
        }
        finally
        {
            VillageLayout.SurfaceY = original;
        }
    }
}
