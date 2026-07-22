namespace CvarcLogger.App.Services;

/// <summary>Translates the Elecraft K4's numeric MD; mode digit (K4 Programmer's Reference, rev. C10) to
/// the rigctld-style mode string CvarcLogger.Core.Rig.RigModeMapper already understands, so that existing,
/// unmodified Core logic (including its "ambiguous digital mode defaults to FT8" reasoning for PKTUSB/
/// PKTLSB) does the real translation into CvarcLogger's own Mode vocabulary instead of being
/// reimplemented here.</summary>
public static class K4ModeAdapter
{
    public static string? ToRigctldModeString(char k4ModeDigit) => k4ModeDigit switch
    {
        '1' => "LSB",
        '2' => "USB",
        '3' => "CW",
        '4' => "FM",
        '5' => "AM",
        '6' => "PKTUSB", // DATA, ambiguous digital mode; RigModeMapper defaults this to FT8
        '9' => "PKTUSB", // DATA REV, same ambiguity as DATA
        '7' => "CWR",    // CW REV
        _ => null,       // 0/8 = N/A, or an unrecognized digit
    };
}
