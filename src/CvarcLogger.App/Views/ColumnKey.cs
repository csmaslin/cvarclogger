using System.Windows;

namespace CvarcLogger.App.Views;

/// <summary>Attached property giving a DataGridColumn a stable string key, independent of its Header
/// text or x:Name, for persisting column order (DataGridColumn isn't a FrameworkElement, so it has no
/// Tag property to reuse for this).</summary>
public static class ColumnKey
{
    public static readonly DependencyProperty KeyProperty =
        DependencyProperty.RegisterAttached("Key", typeof(string), typeof(ColumnKey));

    public static string? GetKey(DependencyObject obj) => (string?)obj.GetValue(KeyProperty);
    public static void SetKey(DependencyObject obj, string? value) => obj.SetValue(KeyProperty, value);
}
