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

    private readonly ObservableCollection<ManualSection> _sections = new();
    private string[]? _manualLines;

    public class ManualSection
    {
        public required string Title { get; set; }
        public required string Content { get; set; }
        public string SearchKeywords { get; set; } = "";
    }

    public HelpWindow()
    {
        InitializeComponent();
        LoadManual();
        ResultsList.ItemsSource = _sections;
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
            string content = File.ReadAllText(txtPath);
            _manualLines = content.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            ParseSections(_manualLines);
            if (_sections.Count > 0)
            {
                ResultsList.SelectedIndex = 0;
                DisplaySection(_sections[0]);
            }
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load text manual from {TxtPath}", txtPath);
            return false;
        }
    }

    private void ParseSections(string[] lines)
    {
        _sections.Clear();
        var currentSection = new ManualSection { Title = "Table of Contents", Content = "" };
        var contentLines = new List<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            if (line.StartsWith("###") || line.StartsWith("##"))
            {
                if (!string.IsNullOrWhiteSpace(currentSection.Title) && contentLines.Count > 0)
                {
                    currentSection.Content = string.Join(Environment.NewLine, contentLines);
                    currentSection.SearchKeywords = (currentSection.Title + " " + currentSection.Content).ToLower();
                    _sections.Add(currentSection);
                }

                string titleText = line.TrimStart('#').Trim();
                currentSection = new ManualSection { Title = titleText, Content = "" };
                contentLines.Clear();
            }
            else if (!string.IsNullOrWhiteSpace(line))
            {
                contentLines.Add(line);
            }
        }

        if (!string.IsNullOrWhiteSpace(currentSection.Title) && contentLines.Count > 0)
        {
            currentSection.Content = string.Join(Environment.NewLine, contentLines);
            currentSection.SearchKeywords = (currentSection.Title + " " + currentSection.Content).ToLower();
            _sections.Add(currentSection);
        }
    }

    private bool TryLoadPdfManual(string pdfPath)
    {
        if (!File.Exists(pdfPath))
            return false;

        try
        {
            var pdfSection = new ManualSection
            {
                Title = "User Manual (PDF)",
                Content = $"PDF manual found at:\n{pdfPath}\n\nTo view the full manual, please open the PDF file in your Documents folder.",
                SearchKeywords = "manual pdf"
            };
            _sections.Add(pdfSection);
            DisplaySection(pdfSection);
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
        var notFoundSection = new ManualSection
        {
            Title = "Help Not Available",
            Content = $"Help manual not found.\n\nThe application comes with a user manual (CvarcLogger User Manual.pdf) that should be in the same folder as this program.\n\nPlease check:\n{baseDir}",
            SearchKeywords = "help manual not found"
        };
        _sections.Add(notFoundSection);
        DisplaySection(notFoundSection);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string query = SearchBox.Text.Trim().ToLower();

        if (string.IsNullOrWhiteSpace(query))
        {
            ResultCount.Text = "";
            foreach (var section in _sections)
                section.Title = section.Title;
            return;
        }

        var matchedCount = _sections.Count(s => s.SearchKeywords.Contains(query) || s.Title.ToLower().Contains(query));
        ResultCount.Text = $"({matchedCount} matches)";

        ResultsList.Items.Filter = item =>
        {
            if (item is ManualSection section)
                return section.SearchKeywords.Contains(query) || section.Title.ToLower().Contains(query);
            return false;
        };
    }

    private void ResultsList_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ResultsList.SelectedItem is ManualSection section)
            DisplaySection(section);
    }

    private void DisplaySection(ManualSection section)
    {
        ContentText.Text = $"{section.Title}\n{'=' * section.Title.Length}\n\n{section.Content}";
        ContentScroll.ScrollToHome();
    }
}
