using Aspire.Hosting.Minecraft.Worker.Services;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests.Services;

public class VillageLayoutTests
{
    [Theory]
    [InlineData(1, 0, new[] { 10, -59, 0 })]
    [InlineData(2, 0, new[] { 10, -59, 0 })]
    [InlineData(2, 1, new[] { 46, -59, 0 })]
    [InlineData(4, 0, new[] { 10, -59, 0 })]
    [InlineData(4, 1, new[] { 46, -59, 0 })]
    [InlineData(4, 2, new[] { 10, -59, 36 })]
    [InlineData(4, 3, new[] { 46, -59, 36 })]
    [InlineData(8, 0, new[] { 10, -59, 0 })]
    [InlineData(8, 7, new[] { 46, -59, 108 })]
    [InlineData(10, 0, new[] { 10, -59, 0 })]
    [InlineData(10, 9, new[] { 46, -59, 144 })]
    public void GetStructureOrigin_ReturnsCorrectCoordinates(int totalResources, int index, int[] expected)
    {
        var (x, y, z) = VillageLayout.GetStructureOrigin(index);
        
        Assert.Equal(expected[0], x);
        Assert.Equal(expected[1], y);
        Assert.Equal(expected[2], z);
    }

    [Theory]
    [InlineData(0, 17, -59, 7)]
    [InlineData(1, 53, -59, 7)]
    [InlineData(2, 17, -59, 43)]
    [InlineData(3, 53, -59, 43)]
    public void GetStructureCenter_ReturnsCorrectOffset(int index, int expectedX, int expectedY, int expectedZ)
    {
        var (x, y, z) = VillageLayout.GetStructureCenter(index);
        
        Assert.Equal(expectedX, x);
        Assert.Equal(expectedY, y);
        Assert.Equal(expectedZ, z);
    }

    [Theory]
    [InlineData(1, 10, 0, 24, 14)]
    [InlineData(2, 10, 0, 60, 14)]
    [InlineData(4, 10, 0, 60, 50)]
    [InlineData(8, 10, 0, 60, 122)]
    [InlineData(10, 10, 0, 60, 158)]
    public void GetVillageBounds_ReturnsCorrectBoundingBox(int resourceCount, int expectedMinX, int expectedMinZ, int expectedMaxX, int expectedMaxZ)
    {
        var (minX, minZ, maxX, maxZ) = VillageLayout.GetVillageBounds(resourceCount);
        
        Assert.Equal(expectedMinX, minX);
        Assert.Equal(expectedMinZ, minZ);
        Assert.Equal(expectedMaxX, maxX);
        Assert.Equal(expectedMaxZ, maxZ);
    }

    [Theory]
    [InlineData(1, 0, -10, 34, 24)]
    [InlineData(2, 0, -10, 70, 24)]
    [InlineData(4, 0, -10, 70, 60)]
    [InlineData(8, 0, -10, 70, 132)]
    [InlineData(10, 0, -10, 70, 168)]
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

            // Grand layout: BaseX=10, StructureSize=15, center at x+7 = 17
            Assert.Equal(17, x);
            Assert.Equal(73, y);
            Assert.Equal(7, z); // BaseZ=0, center at z+7 = 7
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

            // Grand layout: center at x+7 = 17, z+7 = 7
            Assert.Equal(17, x);
            Assert.Equal(74, y); // SurfaceY + 10
            Assert.Equal(7, z);
        }
        finally
        {
            VillageLayout.SurfaceY = original;
        }
    }

    // --- Configurable layout tests ---

    [Fact]
    public void DefaultLayout_MatchesGrandValues()
    {
        Assert.Equal(36, VillageLayout.Spacing);
        Assert.Equal(15, VillageLayout.StructureSize);
        Assert.Equal(10, VillageLayout.FenceClearance);
        Assert.Equal(5, VillageLayout.GateWidth);
    }

    [Fact]
    public void GetStructureCenter_UsesStructureSizeHalf()
    {
        // Grand: StructureSize=15, half=7
        var (x, y, z) = VillageLayout.GetStructureCenter(0);
        Assert.Equal(17, x); // 10 + 7
        Assert.Equal(7, z);  // 0 + 7
    }

    [Fact]
    public void GetRailEntrance_ReturnsCorrectPosition()
    {
        var originalSurface = VillageLayout.SurfaceY;
        try
        {
            VillageLayout.SurfaceY = VillageLayout.BaseY;
            var (x, y, z) = VillageLayout.GetRailEntrance(0);
            Assert.Equal(17, x);  // 10 + 15/2 = 10 + 7
            Assert.Equal(-59, y); // SurfaceY + 1
            Assert.Equal(-1, z);  // 0 - 1
        }
        finally
        {
            VillageLayout.SurfaceY = originalSurface;
        }
    }

    [Fact]
    public void GetRailEntrance_SecondIndex_ReturnsCorrectPosition()
    {
        var originalSurface = VillageLayout.SurfaceY;
        try
        {
            VillageLayout.SurfaceY = VillageLayout.BaseY;
            var (x, y, z) = VillageLayout.GetRailEntrance(1);
            Assert.Equal(53, x);  // 46 + 7 (grand layout: 10 + 36 + 15/2)
            Assert.Equal(-59, y);
            Assert.Equal(-1, z);
        }
        finally
        {
            VillageLayout.SurfaceY = originalSurface;
        }
    }

    [Fact]
    public void GetVillageBounds_UsesGrandStructureSize()
    {
        var (minX, minZ, maxX, maxZ) = VillageLayout.GetVillageBounds(1);
        Assert.Equal(10, minX);
        Assert.Equal(0, minZ);
        Assert.Equal(24, maxX); // 10 + 15 - 1
        Assert.Equal(14, maxZ); // 0 + 15 - 1
    }

    [Fact]
    public void GetFencePerimeter_Uses10BlockClearance()
    {
        var (minX, minZ, maxX, maxZ) = VillageLayout.GetFencePerimeter(1);
        Assert.Equal(0, minX);   // 10 - 10
        Assert.Equal(-10, minZ); // 0 - 10
        Assert.Equal(34, maxX);  // 24 + 10
        Assert.Equal(24, maxZ);  // 14 + 10
    }

    // --- Grand Village layout tests ---

    [Fact]
    public void GetStructureCenter_ReturnsCenterOfFifteenByFifteen()
    {
        // Grand: StructureSize=15, half=7
        var (x, y, z) = VillageLayout.GetStructureCenter(0);
        Assert.Equal(17, x); // 10 + 7
        Assert.Equal(-59, y);
        Assert.Equal(7, z);  // 0 + 7
    }
}
