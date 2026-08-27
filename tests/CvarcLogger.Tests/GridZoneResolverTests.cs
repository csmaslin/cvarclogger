using CvarcLogger.Core.Geo;

namespace CvarcLogger.Tests;

public class GridZoneResolverTests
{
    private readonly GridZoneResolver _resolver = new();

    [Fact]
    public void Resolve_DM04_MatchesLiveGridRadioAndZoneCheckLookup()
    {
        // Ground truth cross-checked directly against grid.radio and zone-check.eu for grid DM04
        // (both agreed: CQ Zone 3, ITU Zone 6) before this offline port was written.
        var (cqZone, ituZone) = _resolver.Resolve("DM04");

        Assert.Equal(3, cqZone);
        Assert.Equal(6, ituZone);
    }

    [Fact]
    public void Resolve_SixCharacterGrid_UsesSameFieldSquareAsFourCharacter()
    {
        var (cqZone, ituZone) = _resolver.Resolve("DM04mm");

        Assert.Equal(3, cqZone);
        Assert.Equal(6, ituZone);
    }

    [Fact]
    public void Resolve_IsCaseInsensitive()
    {
        var (cqZone, ituZone) = _resolver.Resolve("dm04");

        Assert.Equal(3, cqZone);
        Assert.Equal(6, ituZone);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("XX")]
    [InlineData("NOTAGRID")]
    public void Resolve_InvalidOrUnusableGrid_ReturnsNullsWithoutThrowing(string? grid)
    {
        var (cqZone, ituZone) = _resolver.Resolve(grid);

        Assert.Null(cqZone);
        Assert.Null(ituZone);
    }
}
