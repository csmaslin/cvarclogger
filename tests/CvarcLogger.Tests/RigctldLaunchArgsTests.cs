using CvarcLogger.Core.Rig;

namespace CvarcLogger.Tests;

public class RigctldLaunchArgsTests
{
    [Fact]
    public void Build_ProducesExpectedArgumentString()
    {
        var profile = new RadioProfile
        {
            Name = "Yaesu FT-991A",
            HamlibModelId = 1035,
            ComPort = "COM4",
            BaudRate = 38400,
        };

        string args = RigctldLaunchArgs.Build(profile, tcpPort: 4532);

        Assert.Equal("-m 1035 -r COM4 -s 38400 -t 4532", args);
    }
}
