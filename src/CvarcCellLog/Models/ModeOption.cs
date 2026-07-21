using CvarcLogger.Core.Models;

namespace CvarcCellLog.Models;

/// <summary>Wraps a QsoFieldOptions.Modes entry so the Mode picker can show an abbreviated label (just
/// "DV" for "DIGITALVOICE" today) while Qso.Mode/ADIF export keep the real, unabbreviated ADIF 3.1.4
/// Mode token -- MAUI's Picker has no ItemDisplayBinding for a plain string ItemsSource, so it renders
/// whatever ToString() returns for each item. Display-only: never compared against or persisted as Value
/// anywhere outside this picker-binding layer.</summary>
public sealed class ModeOption
{
    public string Value { get; }
    private readonly string _display;

    public ModeOption(string value, string? display = null)
    {
        Value = value;
        _display = display ?? value;
    }

    public override string ToString() => _display;
    public override bool Equals(object? obj) => obj is ModeOption other && other.Value == Value;
    public override int GetHashCode() => Value.GetHashCode();
}

public static class ModeOptions
{
    public static readonly IReadOnlyList<ModeOption> All = QsoFieldOptions.Modes
        .Select(m => new ModeOption(m, string.Equals(m, "DIGITALVOICE", StringComparison.OrdinalIgnoreCase) ? "DV" : m))
        .ToList();

    public static ModeOption For(string value) =>
        All.FirstOrDefault(o => string.Equals(o.Value, value, StringComparison.OrdinalIgnoreCase)) ?? new ModeOption(value);
}
