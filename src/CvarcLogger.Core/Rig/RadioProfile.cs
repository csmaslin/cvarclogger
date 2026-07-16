namespace CvarcLogger.Core.Rig;

public class RadioProfile
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Hamlib's numeric rig model ID (see `rigctl --list`). 0 means "not yet configured."</summary>
    public int HamlibModelId { get; set; }

    public string ComPort { get; set; } = "COM1";
    public int BaudRate { get; set; } = 38400;
}
