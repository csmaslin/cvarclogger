namespace CvarcLogger.App;

/// <summary>Build version shown in the main window title bar. Format is a plain decimal "1.XX" — not
/// semver — so it's bumped by hand rather than sourced from MSBuild's Version property (which
/// normalizes "1.00" down to "1.0" and drops the leading zero). Bump by 0.01 on every rebuild that
/// gets published to the user.</summary>
public static class AppVersion
{
    public const string Current = "2.03";
}
