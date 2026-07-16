namespace CvarcLogger.Core.Rig;

/// <summary>Derives an amateur band name from a frequency, for CAT auto-fill. Ranges match
/// QsoEntryViewModel.Bands exactly.</summary>
public static class BandCalculator
{
    private static readonly (decimal MinMhz, decimal MaxMhz, string Band)[] Bands =
    {
        (1.8m, 2.0m, "160m"),
        (3.5m, 4.0m, "80m"),
        (5.06m, 5.45m, "60m"),
        (7.0m, 7.3m, "40m"),
        (10.1m, 10.15m, "30m"),
        (14.0m, 14.35m, "20m"),
        (18.068m, 18.168m, "17m"),
        (21.0m, 21.45m, "15m"),
        (24.89m, 24.99m, "12m"),
        (28.0m, 29.7m, "10m"),
        (50.0m, 54.0m, "6m"),
        (144.0m, 148.0m, "2m"),
        (420.0m, 450.0m, "70cm"),
    };

    public static string? FromFrequencyMhz(decimal frequencyMhz)
    {
        foreach (var (minMhz, maxMhz, band) in Bands)
        {
            if (frequencyMhz >= minMhz && frequencyMhz <= maxMhz) return band;
        }
        return null;
    }
}
