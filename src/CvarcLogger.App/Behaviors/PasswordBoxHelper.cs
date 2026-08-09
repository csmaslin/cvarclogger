using System.Windows;
using System.Windows.Controls;

namespace CvarcLogger.App.Behaviors;

/// <summary>Enables a placeholder/hint text on PasswordBox (which, unlike TextBox, has no Text
/// dependency property to trigger a template-based placeholder off of -- Password is a plain CLR
/// property by design, so PasswordChanged is the only hook available). Set
/// PasswordBoxHelper.Placeholder="..." on any PasswordBox to show it via HasText, which the
/// PasswordBox style watches with a Trigger.</summary>
public static class PasswordBoxHelper
{
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.RegisterAttached(
        "Placeholder", typeof(string), typeof(PasswordBoxHelper), new PropertyMetadata(null, OnPlaceholderChanged));

    public static readonly DependencyProperty HasTextProperty = DependencyProperty.RegisterAttached(
        "HasText", typeof(bool), typeof(PasswordBoxHelper), new PropertyMetadata(false));

    public static string GetPlaceholder(DependencyObject obj) => (string)obj.GetValue(PlaceholderProperty);
    public static void SetPlaceholder(DependencyObject obj, string value) => obj.SetValue(PlaceholderProperty, value);
    public static bool GetHasText(DependencyObject obj) => (bool)obj.GetValue(HasTextProperty);
    public static void SetHasText(DependencyObject obj, bool value) => obj.SetValue(HasTextProperty, value);

    private static void OnPlaceholderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox pb) return;
        pb.PasswordChanged -= Pb_PasswordChanged;
        pb.PasswordChanged += Pb_PasswordChanged;
        SetHasText(pb, pb.Password.Length > 0);
    }

    private static void Pb_PasswordChanged(object sender, RoutedEventArgs e)
    {
        var pb = (PasswordBox)sender;
        SetHasText(pb, pb.Password.Length > 0);
    }
}
