using CvarcLogger.Core.Rig;

namespace CvarcLogger.Tests;

public class BandCalculatorTests
{
    [Theory]
    [InlineData(1.8, "160m")]
    [InlineData(2.0, "160m")]
    [InlineData(3.5, "80m")]
    [InlineData(7.0, "40m")]
    [InlineData(7.3, "40m")]
    [InlineData(14.0, "20m")]
    [InlineData(14.35, "20m")]
    [InlineData(21.0, "15m")]
    [InlineData(28.0, "10m")]
    [InlineData(29.7, "10m")]
    [InlineData(50.0, "6m")]
    [InlineData(144.0, "2m")]
    [InlineData(222.0, "1.25M")]
    [InlineData(225.0, "1.25M")]
    [InlineData(420.0, "70cm")]
    public void FromFrequencyMhz_ReturnsExpectedBand(double freq, string expectedBand)
    {
        Assert.Equal(expectedBand, BandCalculator.FromFrequencyMhz((decimal)freq));
    }

    [Theory]
    [InlineData(13.999)]
    [InlineData(29.8)]
    [InlineData(60.0)]
    [InlineData(0.5)]
    public void FromFrequencyMhz_OutsideAllBands_ReturnsNull(double freq)
    {
        Assert.Null(BandCalculator.FromFrequencyMhz((decimal)freq));
    }
}
