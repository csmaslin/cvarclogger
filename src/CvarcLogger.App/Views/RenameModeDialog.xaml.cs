using System.Windows;
using System.Windows.Input;

namespace CvarcLogger.App.Views;

/// <summary>Minimal text-prompt modal -- DialogService has no generic input prompt, and this is only
/// needed for one thing (renaming a Column Visibility picker tab, see ColumnPickerWindow), so a small
/// dedicated window is simpler than adding a general-purpose prompt abstraction for a single call site.</summary>
public partial class RenameModeDialog : Window
{
    public string? Result { get; private set; }

    public RenameModeDialog(string currentLabel)
    {
        InitializeComponent();
        LabelTextBox.Text = currentLabel;
        LabelTextBox.SelectAll();
        Loaded += (_, _) => LabelTextBox.Focus();
    }

    private void LabelTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Ok_Click(sender, e);
        else if (e.Key == Key.Escape) Cancel_Click(sender, e);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var text = LabelTextBox.Text.Trim();
        if (text.Length == 0) return;

        Result = text;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
