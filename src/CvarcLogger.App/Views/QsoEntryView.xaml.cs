using System.Windows;
using System.Windows.Controls;
using CvarcLogger.App.ViewModels;

namespace CvarcLogger.App.Views;

public partial class QsoEntryView : UserControl
{
    public QsoEntryView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is QsoEntryViewModel oldViewModel)
            oldViewModel.FieldVisibilityChanged -= OnFieldVisibilityChanged;

        if (e.NewValue is QsoEntryViewModel newViewModel)
            newViewModel.FieldVisibilityChanged += OnFieldVisibilityChanged;

        ApplyFieldVisibility();
    }

    private void OnFieldVisibilityChanged(object? sender, EventArgs e) => ApplyFieldVisibility();

    /// <summary>Hides/shows the entry form's few fields that have no Log-Mode-based visibility rule at
    /// all (they're either always shown, or gated purely by the Choose Columns picker) in step with that
    /// picker. Everything else -- every field whose visibility also depends on Log Mode -- used to be set
    /// here too, but setting a WPF property directly in code clears any active data Binding on it, which
    /// silently broke each of those panels' Log-Mode-based Show* binding the first time this ran (see the
    /// comment above QsoEntryViewModel.ShowSkccField). Those are now folded into binding-driven *Field
    /// properties in the ViewModel instead and must NOT be set here.</summary>
    private void ApplyFieldVisibility()
    {
        if (DataContext is not QsoEntryViewModel viewModel) return;

        FreqPanel.Visibility = ToVisibility(viewModel.IsFieldVisible("Freq"));
        RstSentPanel.Visibility = ToVisibility(viewModel.IsFieldVisible("Rst"));
        RstRcvdPanel.Visibility = ToVisibility(viewModel.IsFieldVisible("Rst"));
        NamePanel.Visibility = ToVisibility(viewModel.IsFieldVisible("Name"));
    }

    private static Visibility ToVisibility(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;
}
