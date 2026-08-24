using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Serilog;

namespace CvarcLogger.App.Views;

public partial class HelpWindow : Window
{
    private const string ManualTxtFilename = "CvarcLogger User Manual.txt";
    private const string ManualPdfFilename = "CvarcLogger User Manual.pdf";
    private const int TitleTruncateLength = 77;
    private const int PreviewTruncateLength = 97;
    private const int ContextLines = 5;

    private string _fullManualContent = "";
    private readonly ObservableCollection<SearchResult> _results = new();
    private string[]? _manualLines;

    public class SearchResult
    {
        public required string Title { get; set; }
        public required string Preview { get; set; }
        public int LineNumber { get; set; }
        public required string FullContent { get; set; }
    }

    public HelpWindow()
    {
        InitializeComponent();
        LoadManual();
        ResultsList.ItemsSource = _results;
    }

    private void LoadManual()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string txtPath = Path.Combine(baseDir, ManualTxtFilename);
        string pdfPath = Path.Combine(baseDir, ManualPdfFilename);

        if (TryLoadTextManual(txtPath))
            return;

        if (TryLoadPdfManual(pdfPath))
            return;

        DisplayManualNotFound(baseDir);
    }

    private bool TryLoadTextManual(string txtPath)
    {
        if (!File.Exists(txtPath))
            return false;

        try
        {
            _fullManualContent = File.ReadAllText(txtPath);
            _manualLines = _fullManualContent.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            ContentText.Text = _fullManualContent;
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load text manual from {TxtPath}", txtPath);
            return false;
        }
    }

    private bool TryLoadPdfManual(string pdfPath)
    {
        if (!File.Exists(pdfPath))
            return false;

        try
        {
            _fullManualContent = $"PDF manual found. Opening in default PDF viewer...\n\nTo view the full manual, please open: {pdfPath}";
            ContentText.Text = _fullManualContent;
            OpenFile(pdfPath);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load PDF manual from {PdfPath}", pdfPath);
            return false;
        }
    }

    private void DisplayManualNotFound(string baseDir)
    {
        _fullManualContent = $"Help manual not found.\n\nThe application comes with a user manual ({ManualPdfFilename}) that should be in the same folder as this program.\n\nPlease check:\n{baseDir}";
        ContentText.Text = _fullManualContent;
    }

    private void OpenFile(string filePath)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open file {FilePath}", filePath);
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string query = SearchBox.Text.Trim();
        _results.Clear();

        if (string.IsNullOrWhiteSpace(query) || _manualLines == null)
        {
            ResultCount.Text = "";
            ContentText.Text = _fullManualContent;
            return;
        }

        var matchedSections = FindMatches(query, _manualLines);
        foreach (var result in matchedSections)
            _results.Add(result);

        ResultCount.Text = $"({_results.Count} matches)";
        if (_results.Count > 0)
            ResultsList.SelectedIndex = 0;
    }

    private static ObservableCollection<SearchResult> FindMatches(string query, string[] lines)
    {
        var results = new ObservableCollection<SearchResult>();
        string lowerQuery = query.ToLower();

        for (int i = 0; i < lines.Length; i++)
        {
            if (!lines[i].ToLower().Contains(lowerQuery))
                continue;

            results.Add(new SearchResult
            {
                Title = TruncateString(lines[i], TitleTruncateLength),
                Preview = i + 1 < lines.Length ? TruncateString(lines[i + 1], PreviewTruncateLength) : "",
                LineNumber = i,
                FullContent = string.Join(Environment.NewLine, lines.Skip(Math.Max(0, i - 2)).Take(ContextLines))
            });
        }

        return results;
    }

    private static string TruncateString(string text, int maxLength)
        => text.Length > maxLength ? text.Substring(0, maxLength - 3) + "..." : text;

    private void ResultsList_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ResultsList.SelectedItem is SearchResult result)
        {
            ContentText.Text = result.FullContent;
            ContentScroll.ScrollToHome();
        }
    }
}
