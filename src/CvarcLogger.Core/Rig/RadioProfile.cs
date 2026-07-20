namespace CvarcLogger.Core.Rig;

public class RadioProfile
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Hamlib's numeric rig model ID (see `rigctl --list`). 0 means "not yet configured."</summary>
    public int HamlibModelId { get; set; }

    public string ComPort { get; set; } = "COM1";
    public int BaudRate { get; set; } = 38400;

    /// <summary>This radio's maximum RF output in watts. Hamlib/rigctld's RFPOWER level is a 0.0-1.0
    /// fraction of the rig's own power-control range, not real watts, for the vast majority of rigs --
    /// this is what turns that fraction into an estimated TX Power for the log (see
    /// RigctldClient.PollAsync and QsoEntryViewModel.OnCatPollTick).</summary>
    public int MaxPowerWatts { get; set; } = 100;
}
