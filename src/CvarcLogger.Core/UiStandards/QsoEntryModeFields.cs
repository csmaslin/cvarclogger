namespace CvarcLogger.Core.UiStandards;

/// <summary>Preset field layouts for the Log QSO / New QSO entry form, shared by CvarcLogger (WPF) and
/// CvarcCellLog (MAUI) so the two apps' preset behavior stays identical. Normal shows every field, same
/// as before this feature existed; Contest/Sota/Pota each collapse the form down to just the fields that
/// mode actually needs. Field sets set directly by the user (csmaslin) on 2026-07-28.</summary>
public enum QsoEntryMode
{
    Normal,
    Contest,
    Sota,
    Pota,
    All,
}

/// <summary>Callsign, Local Time, RST/S, RST/R, Freq (MHz), Mode, and Name are always shown regardless of
/// the selected mode (every preset's field list includes them) and so have no flag here -- only fields
/// that actually vary by mode get one.</summary>
public static class QsoEntryModeFields
{
    public static bool ShowDateTimeUtc(QsoEntryMode mode) => mode is QsoEntryMode.Normal or QsoEntryMode.Sota or QsoEntryMode.Pota or QsoEntryMode.All;

    public static bool ShowTimeOff(QsoEntryMode mode) => mode is QsoEntryMode.Normal or QsoEntryMode.All;

    public static bool ShowBand(QsoEntryMode mode) => mode is QsoEntryMode.Normal or QsoEntryMode.Sota or QsoEntryMode.Pota or QsoEntryMode.All;

    public static bool ShowSubMode(QsoEntryMode mode) => mode is QsoEntryMode.Normal or QsoEntryMode.All;

    public static bool ShowTxPower(QsoEntryMode mode) => mode is QsoEntryMode.Normal or QsoEntryMode.Contest or QsoEntryMode.All;

    public static bool ShowGridSquare(QsoEntryMode mode) => mode is QsoEntryMode.Normal or QsoEntryMode.Contest or QsoEntryMode.All;

    public static bool ShowState(QsoEntryMode mode) => mode is QsoEntryMode.Normal or QsoEntryMode.Contest or QsoEntryMode.All;

    public static bool ShowSotaFields(QsoEntryMode mode) => mode is QsoEntryMode.Normal or QsoEntryMode.Sota or QsoEntryMode.All;

    public static bool ShowPotaFields(QsoEntryMode mode) => mode is QsoEntryMode.Normal or QsoEntryMode.Pota or QsoEntryMode.All;

    public static bool ShowSkccFields(QsoEntryMode mode) => mode is QsoEntryMode.Normal or QsoEntryMode.All;

    /// <summary>Precedence/Check/Class -- the ARRL contest exchange, distinct from ShowSkccFields.</summary>
    public static bool ShowContestExchangeFields(QsoEntryMode mode) => mode is QsoEntryMode.Normal or QsoEntryMode.Contest or QsoEntryMode.All;

    public static bool ShowCityCounty(QsoEntryMode mode) => mode is QsoEntryMode.Normal or QsoEntryMode.All;

    public static bool ShowCountry(QsoEntryMode mode) => mode is QsoEntryMode.Normal or QsoEntryMode.All;

    public static bool ShowArrlSection(QsoEntryMode mode) => mode is QsoEntryMode.Normal or QsoEntryMode.Contest or QsoEntryMode.All;

    public static bool ShowCqItuZone(QsoEntryMode mode) => mode is QsoEntryMode.Normal or QsoEntryMode.Contest or QsoEntryMode.All;

    public static bool ShowComment(QsoEntryMode mode) => mode is QsoEntryMode.Normal or QsoEntryMode.All;
}

/// <summary>Picker/ComboBox item wrapper, same ToString()-override pattern as ModeOption/ArrlPrecedenceOption
/// -- works unmodified in both MAUI's Picker and WPF's ComboBox.</summary>
public sealed class QsoEntryModeOption
{
    public QsoEntryMode Value { get; }
    private readonly string _display;

    public QsoEntryModeOption(QsoEntryMode value, string display)
    {
        Value = value;
        _display = display;
    }

    public override string ToString() => _display;
    public override bool Equals(object? obj) => obj is QsoEntryModeOption other && other.Value == Value;
    public override int GetHashCode() => Value.GetHashCode();
}

public static class QsoEntryModeOptions
{
    public static readonly IReadOnlyList<QsoEntryModeOption> All = new[]
    {
        new QsoEntryModeOption(QsoEntryMode.Normal, "Normal"),
        new QsoEntryModeOption(QsoEntryMode.Contest, "Contest"),
        new QsoEntryModeOption(QsoEntryMode.Sota, "SOTA"),
        new QsoEntryModeOption(QsoEntryMode.Pota, "POTA"),
        new QsoEntryModeOption(QsoEntryMode.All, "All"),
    };

    public static QsoEntryModeOption For(QsoEntryMode value) => All.First(o => o.Value == value);
}
