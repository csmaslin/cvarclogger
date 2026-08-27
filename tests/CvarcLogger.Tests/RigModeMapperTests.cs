using CvarcLogger.Core.Rig;

namespace CvarcLogger.Tests;

public class RigModeMapperTests
{
    [Theory]
    [InlineData("USB", "SSB")]
    [InlineData("LSB", "SSB")]
    [InlineData("usb", "SSB")]
    [InlineData("CW", "CW")]
    [InlineData("CWR", "CW")]
    [InlineData("FM", "FM")]
    [InlineData("WFM", "FM")]
    [InlineData("AM", "AM")]
    [InlineData("RTTY", "RTTY")]
    [InlineData("RTTYR", "RTTY")]
    [InlineData("PKTUSB", "FT8")]
    [InlineData("PKTLSB", "FT8")]
    [InlineData("PKTFM", "FT8")]
    [InlineData("PKTAM", "FT8")]
    public void ToCvarcLoggerMode_MapsKnownRigctldModes(string rigctldMode, string expected)
    {
        Assert.Equal(expected, RigModeMapper.ToCvarcLoggerMode(rigctldMode));
    }

    [Fact]
    public void ToCvarcLoggerMode_UnknownMode_PassesThroughRaw()
    {
        Assert.Equal("XYZ", RigModeMapper.ToCvarcLoggerMode("XYZ"));
    }

    [Fact]
    public void ToCvarcLoggerMode_EmptyString_FallsBackToSsb()
    {
        Assert.Equal("SSB", RigModeMapper.ToCvarcLoggerMode(""));
    }

    [Theory]
    [InlineData("USB", "USB")]
    [InlineData("usb", "USB")]
    [InlineData("LSB", "LSB")]
    [InlineData("lsb", "LSB")]
    public void ToCvarcLoggerSubMode_MapsSsbRawModes(string rigctldMode, string expected)
    {
        Assert.Equal(expected, RigModeMapper.ToCvarcLoggerSubMode(rigctldMode));
    }

    [Theory]
    [InlineData("CW")]
    [InlineData("PKTUSB")]
    [InlineData("FM")]
    [InlineData("")]
    public void ToCvarcLoggerSubMode_NonSsbRawModes_ReturnsNull(string rigctldMode)
    {
        Assert.Null(RigModeMapper.ToCvarcLoggerSubMode(rigctldMode));
    }
}
