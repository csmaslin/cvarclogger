using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace CvarcLogger.App.Views;

public partial class HelpWindow : Window
{
    private string _fullManualContent = "";
    private ObservableCollection<SearchResult> _results = new();

    public class SearchResult
    {
        public string Title { get; set; } = "";
        public string Preview { get; set; } = "";
        public int LineNumber { get; set; }
        public string FullContent { get; set; } = "";
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
        string txtPath = Path.Combine(baseDir, "CvarcLogger User Manual.txt");
        string pdfPath = Path.Combine(baseDir, "CvarcLogger User Manual.pdf");

        if (File.Exists(txtPath))
        {
            _fullManualContent = File.ReadAllText(txtPath);
        }
        else if (File.Exists(pdfPath))
        {
            _fullManualContent = "PDF manual found. Opening in default PDF viewer...\n\n";
            _fullManualContent += "To view the full manual, please open: " + pdfPath;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = pdfPath,
                    UseShellExecute = true
                });
            }
            catch { }
        }
        else
        {
            _fullManualContent = "Help manual not found.\n\nThe application comes with a user manual (CvarcLogger User Manual.pdf) that should be in the same folder as this program.\n\nPlease check:\n" + baseDir;
        }

        ContentText.Text = _fullManualContent;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string query = SearchBox.Text.Trim();
        _results.Clear();

        if (string.IsNullOrWhiteSpace(query))
        {
            ResultCount.Text = "";
            ContentText.Text = _fullManualContent;
            return;
        }

        string lowerQuery = query.ToLower();
        var lines = _fullManualContent.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
        var matchedSections = new ObservableCollection<SearchResult>();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.ToLower().Contains(lowerQuery))
            {
                string title = line.Length > 80 ? line.Substring(0, 77) + "..." : line;
                string preview = "";

                if (i + 1 < lines.Length)
                    preview = lines[i + 1].Length > 100 ? lines[i + 1].Substring(0, 97) + "..." : lines[i + 1];

                matchedSections.Add(new SearchResult
                {
                    Title = title,
                    Preview = preview,
                    LineNumber = i,
                    FullContent = string.Join(Environment.NewLine, lines.Skip(Math.Max(0, i - 2)).Take(5))
                });
            }
        }

        foreach (var result in matchedSections)
            _results.Add(result);

        ResultCount.Text = $"({_results.Count} matches)";

        if (_results.Count > 0)
            ResultsList.SelectedIndex = 0;
    }

    private void ResultsList_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ResultsList.SelectedItem is SearchResult result)
        {
            ContentText.Text = result.FullContent;
            ContentScroll.ScrollToHome();
        }
    }
}
