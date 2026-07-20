using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CvarcLogger.App.Behaviors;

/// <summary>Attached behavior that constrains a TextBox to typing digits only and auto-inserts the
/// literal separators from a fixed-width date/time format string as each digit group fills up, so every
/// date field in the app is entered in the same shape (e.g. "2026-07-16 14:30") instead of relying on
/// free-text discipline. The Format value should be one of the exact format strings the owning
/// ViewModel already parses with DateTime.TryParseExact -- 'y', 'M', 'd', 'H', 'm' are digit slots,
/// everything else (space, '-', ':') is a literal. Editing is append/backspace-at-the-end only (no
/// mid-string caret editing) -- simple to reason about and matches how these short fields are actually
/// used in practice (retype the whole value rather than nudge one digit).</summary>
public static class DateInputMask
{
    public static readonly DependencyProperty FormatProperty = DependencyProperty.RegisterAttached(
        "Format", typeof(string), typeof(DateInputMask), new PropertyMetadata(null, OnFormatChanged));

    public static string? GetFormat(DependencyObject obj) => (string?)obj.GetValue(FormatProperty);
    public static void SetFormat(DependencyObject obj, string? value) => obj.SetValue(FormatProperty, value);

    private static void OnFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox) return;

        textBox.PreviewTextInput -= OnPreviewTextInput;
        textBox.PreviewKeyDown -= OnPreviewKeyDown;
        textBox.GotFocus -= OnGotFocus;
        textBox.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
        DataObject.RemovePastingHandler(textBox, OnPaste);

        if (e.NewValue is string format && !string.IsNullOrEmpty(format))
        {
            textBox.PreviewTextInput += OnPreviewTextInput;
            textBox.PreviewKeyDown += OnPreviewKeyDown;
            textBox.GotFocus += OnGotFocus;
            textBox.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            DataObject.AddPastingHandler(textBox, OnPaste);
        }
    }

    private static void OnGotFocus(object sender, RoutedEventArgs e) => PinCaretToEnd((TextBox)sender);

    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Let the click focus the box normally, then snap the caret to the end once focused -- clicking
        // mid-string must not let the user edit a digit out of place, since AppendDigit/RemoveLastDigit
        // below always operate on the end of the digit buffer regardless of visible caret position.
        var textBox = (TextBox)sender;
        textBox.Dispatcher.BeginInvoke(new Action(() => PinCaretToEnd(textBox)));
    }

    private static void PinCaretToEnd(TextBox textBox) => textBox.CaretIndex = textBox.Text.Length;

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        var textBox = (TextBox)sender;
        e.Handled = true;

        string digits = ExtractDigits(textBox.Text);
        if (textBox.SelectionLength >= textBox.Text.Length && textBox.Text.Length > 0) digits = string.Empty;

        string format = GetFormat(textBox)!;
        int maxDigits = CountDigitSlots(format);

        foreach (char c in e.Text)
        {
            if (!char.IsDigit(c) || digits.Length >= maxDigits) continue;
            digits += c;
        }

        SetRendered(textBox, format, digits);
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Back && e.Key != Key.Delete) return;

        var textBox = (TextBox)sender;
        e.Handled = true;

        string format = GetFormat(textBox)!;
        string digits = ExtractDigits(textBox.Text);
        if (digits.Length == 0) return;

        SetRendered(textBox, format, digits[..^1]);
    }

    private static void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        var textBox = (TextBox)sender;
        e.CancelCommand();

        if (!e.SourceDataObject.GetDataPresent(DataFormats.Text)) return;
        string pasted = (string)e.SourceDataObject.GetData(DataFormats.Text);

        string format = GetFormat(textBox)!;
        int maxDigits = CountDigitSlots(format);
        string digits = string.Empty;
        foreach (char c in pasted)
        {
            if (!char.IsDigit(c) || digits.Length >= maxDigits) continue;
            digits += c;
        }

        SetRendered(textBox, format, digits);
    }

    private static void SetRendered(TextBox textBox, string format, string digits)
    {
        textBox.Text = Render(format, digits);
        PinCaretToEnd(textBox);
    }

    private static bool IsDigitSlot(char formatChar) => formatChar is 'y' or 'M' or 'd' or 'H' or 'm' or 's';

    private static int CountDigitSlots(string format)
    {
        int count = 0;
        foreach (char c in format)
        {
            if (IsDigitSlot(c)) count++;
        }
        return count;
    }

    private static string ExtractDigits(string text)
    {
        var chars = new List<char>(text.Length);
        foreach (char c in text)
        {
            if (char.IsDigit(c)) chars.Add(c);
        }
        return new string(chars.ToArray());
    }

    /// <summary>Renders as many characters of the format template as the available digits justify,
    /// including any literal separator immediately after a digit group completes (e.g. typing the 4th
    /// year digit immediately shows the trailing "-"), then stops -- no placeholder characters for
    /// not-yet-typed digits.</summary>
    private static string Render(string format, string digits)
    {
        var result = new System.Text.StringBuilder(format.Length);
        int digitIndex = 0;
        foreach (char c in format)
        {
            if (IsDigitSlot(c))
            {
                if (digitIndex >= digits.Length) break;
                result.Append(digits[digitIndex]);
                digitIndex++;
            }
            else
            {
                result.Append(c);
            }
        }
        return result.ToString();
    }
}
