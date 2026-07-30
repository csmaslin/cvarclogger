namespace CvarcLogger.Core.Models;

/// <summary>Wraps an ARRL Sweepstakes precedence code with its plain-English definition so a picker/
/// combobox can show the definition while choosing, not just the bare letter -- both MAUI's Picker and
/// WPF's ComboBox render a plain-object ItemsSource via ToString() when no explicit display binding/
/// member path is set, so this same class works unmodified in both apps. Display-only: never compared
/// against or persisted as Value anywhere outside a picker-binding layer -- Qso.Precedence always stores
/// just the bare code.</summary>
public sealed class ArrlPrecedenceOption
{
    public string Value { get; }
    private readonly string _display;

    public ArrlPrecedenceOption(string value, string display)
    {
        Value = value;
        _display = display;
    }

    public override string ToString() => _display;
    public override bool Equals(object? obj) => obj is ArrlPrecedenceOption other && other.Value == Value;
    public override int GetHashCode() => Value.GetHashCode();
}

public static class ArrlPrecedenceOptions
{
    // Definitions per the ARRL Sweepstakes rules.
    public static readonly IReadOnlyList<ArrlPrecedenceOption> All = new[]
    {
        new ArrlPrecedenceOption("Q", "Q - QRP (5W or less)"),
        new ArrlPrecedenceOption("A", "A - Low Power (100W or less)"),
        new ArrlPrecedenceOption("B", "B - High Power (more than 100W)"),
        new ArrlPrecedenceOption("U", "U - Unlimited (uses spotting assistance)"),
        new ArrlPrecedenceOption("M", "M - Multioperator"),
        new ArrlPrecedenceOption("S", "S - School Club"),
    };

    public static ArrlPrecedenceOption For(string? value) =>
        All.FirstOrDefault(o => string.Equals(o.Value, value, StringComparison.OrdinalIgnoreCase))
        ?? new ArrlPrecedenceOption(value ?? string.Empty, value ?? string.Empty);

    /// <summary>Shown as the Check field's explanation popup/tooltip in both apps.</summary>
    public const string CheckExplanation =
        "The last two digits of the year you were first licensed (not the current year). " +
        "It's not scored -- it exists so logs can be cross-checked against each other after the contest.";
}
