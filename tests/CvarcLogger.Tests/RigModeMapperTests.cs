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
    [InlineData("PKTUSB", "DATA")]
    [InlineData("PKTLSB", "DATA")]
    [InlineData("PKTFM", "DATA")]
    [InlineData("PKTAM", "DATA")]
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
}
