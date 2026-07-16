using CvarcLogger.Core.Rig;

namespace CvarcLogger.Tests;

public class HamlibRigListParserTests
{
    // A trimmed real sample of `rigctld --list` stdout: fixed-width columns, some multi-word Mfg/Model
    // values, a blank Model (FLRig), and the stderr init noise stripped out (it's on a separate stream).
    private const string SampleOutput =
        " Rig #  Mfg                    Model                   Version         Status      Macro\n" +
        "     1  Hamlib                 Dummy                   20240709.0      Stable      RIG_MODEL_DUMMY\n" +
        "     2  Hamlib                 NET rigctl              20250211.0      Stable      RIG_MODEL_NETRIGCTL\n" +
        "     4  FLRig                                          20250107.0      Stable      RIG_MODEL_FLRIG\n" +
        " 23003  DTTS Microwave Society DttSP IPC               20200319.0      Stable      RIG_MODEL_DTTSP\n" +
        "   135  Yaesu                  FT-991                  20221215.0      Stable      RIG_MODEL_FT991\n";

    [Fact]
    public void Parse_ExtractsIdManufacturerModelAndStatus()
    {
        var rigs = HamlibRigListParser.Parse(SampleOutput);

        var ft991 = Assert.Single(rigs, r => r.Id == 135);
        Assert.Equal("Yaesu", ft991.Manufacturer);
        Assert.Equal("FT-991", ft991.Model);
        Assert.Equal("Stable", ft991.Status);
    }

    [Fact]
    public void Parse_HandlesMultiWordManufacturerAndModel()
    {
        var rigs = HamlibRigListParser.Parse(SampleOutput);

        var dttsp = Assert.Single(rigs, r => r.Id == 23003);
        Assert.Equal("DTTS Microwave Society", dttsp.Manufacturer);
        Assert.Equal("DttSP IPC", dttsp.Model);

        var netRigctl = Assert.Single(rigs, r => r.Id == 2);
        Assert.Equal("NET rigctl", netRigctl.Model);
    }

    [Fact]
    public void Parse_HandlesBlankModelColumn()
    {
        var rigs = HamlibRigListParser.Parse(SampleOutput);

        var flrig = Assert.Single(rigs, r => r.Id == 4);
        Assert.Equal("FLRig", flrig.Manufacturer);
        Assert.Equal(string.Empty, flrig.Model);
        Assert.Equal("FLRig (4)", flrig.DisplayName);
    }

    [Fact]
    public void Parse_ReturnsEmptyForUnparseableOutput()
    {
        Assert.Empty(HamlibRigListParser.Parse("rigctld: command not found"));
    }

    [Fact]
    public void Parse_DisplayName_IncludesManufacturerModelAndId()
    {
        var rigs = HamlibRigListParser.Parse(SampleOutput);
        var ft991 = Assert.Single(rigs, r => r.Id == 135);
        Assert.Equal("Yaesu FT-991 (135)", ft991.DisplayName);
    }
}
