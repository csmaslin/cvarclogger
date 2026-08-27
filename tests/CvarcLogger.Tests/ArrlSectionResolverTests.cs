using CvarcLogger.Core.Geo;

namespace CvarcLogger.Tests;

public class ArrlSectionResolverTests
{
    [Theory]
    [InlineData("OH", null, "OH")]
    [InlineData("oh", null, "OH")]
    [InlineData("MD", null, "MDC")]
    [InlineData("DC", null, "MDC")]
    [InlineData("QC", null, "QC")]
    [InlineData("NS", null, "MAR")]
    public void Resolve_SingleSectionState_IgnoresCounty(string state, string? county, string expected)
    {
        Assert.Equal(expected, ArrlSectionResolver.Resolve(state, county));
    }

    [Theory]
    [InlineData("CA", "Los Angeles", "LAX")]
    [InlineData("CA", "Los Angeles County", "LAX")]
    [InlineData("CA", "Santa Clara", "SCV")]
    [InlineData("NY", "New York", "NLI")]
    [InlineData("NY", "Erie", "WNY")]
    [InlineData("PA", "Philadelphia", "EPA")]
    [InlineData("PA", "Allegheny", "WPA")]
    [InlineData("MA", "Suffolk", "EMA")]
    [InlineData("MA", "Berkshire", "WMA")]
    [InlineData("NJ", "Bergen", "NNJ")]
    [InlineData("NJ", "Camden", "SNJ")]
    [InlineData("WA", "King", "WWA")]
    [InlineData("WA", "Spokane", "EWA")]
    [InlineData("TX", "Dallas", "NTX")]
    [InlineData("TX", "Harris", "STX")]
    [InlineData("TX", "El Paso", "WTX")]
    [InlineData("FL", "Duval", "NFL")]
    [InlineData("FL", "Miami-Dade", "SFL")]
    [InlineData("FL", "Hillsborough", "WCF")]
    public void Resolve_SplitState_UsesCounty(string state, string county, string expected)
    {
        Assert.Equal(expected, ArrlSectionResolver.Resolve(state, county));
    }

    [Theory]
    [InlineData("CA", null)]
    [InlineData("CA", "")]
    [InlineData("CA", "Nowhere County")]
    [InlineData("TX", "Nowhere")]
    public void Resolve_SplitState_UnrecognizedOrMissingCounty_ReturnsNull(string state, string? county)
    {
        Assert.Null(ArrlSectionResolver.Resolve(state, county));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("ZZ", null)]
    [InlineData("ON", "Toronto")]
    public void Resolve_UnrecognizedState_ReturnsNull(string? state, string? county)
    {
        Assert.Null(ArrlSectionResolver.Resolve(state, county));
    }
}
