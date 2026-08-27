using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Serilog;

namespace CvarcLogger.App.Views;

public partial class HelpWindow : Window
{
    private const string ManualJsonFilename = "CvarcLogger User Manual.json";
    private const string ManualPdfFilename = "CvarcLogger User Manual.pdf";

    private readonly ObservableCollection<ManualChapter> _chapters = new();

    private class MatchLocation
    {
        public int ChapterIndex;
        public int BlockIndex;
        public int Offset;
        public int Length;
    }

    private readonly List<MatchLocation> _matches = new();
    private int _currentMatchIndex = -1;
    private int _pendingHighlightBlockIndex = -1;
    private int _pendingHighlightOffset = -1;
    private int _pendingHighlightLength;
    private Run? _highlightedRun;

    public class ContentBlock
    {
        public required string Type { get; set; }
        public string? Text { get; set; }
        public List<List<string>>? Rows { get; set; }
    }

    public class ManualChapter
    {
        public required string Title { get; set; }
        public required List<ContentBlock> Blocks { get; set; }

        public string Preview
        {
            get
            {
                var firstText = Blocks.Find(b => b.Type == "text" && !string.IsNullOrWhiteSpace(b.Text));
                string text = firstText?.Text ?? "";
                return text.Length > 80 ? text.Substring(0, 77) + "..." : text;
            }
        }
    }

    public HelpWindow()
    {
        InitializeComponent();
        ResultsList.ItemsSource = _chapters;
        LoadManual();
    }

    private void LoadManual()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string jsonPath = Path.Combine(baseDir, ManualJsonFilename);
        string pdfPath = Path.Combine(baseDir, ManualPdfFilename);

        if (TryLoadJsonManual(jsonPath))
            return;

        if (TryLoadPdfFallback(pdfPath))
            return;

        DisplayManualNotFound(baseDir);
    }

    private bool TryLoadJsonManual(string jsonPath)
    {
        if (!File.Exists(jsonPath))
            return false;

        try
        {
            string json = File.ReadAllText(jsonPath);
            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                var chapters = doc.RootElement.GetProperty("chapters").EnumerateArray();

                _chapters.Clear();
                foreach (var chapterElement in chapters)
                {
                    string? title = chapterElement.GetProperty("title").GetString();
                    if (string.IsNullOrWhiteSpace(title))
                        continue;

                    var blocks = new List<ContentBlock>();
                    foreach (var blockElement in chapterElement.GetProperty("blocks").EnumerateArray())
                    {
                        string blockType = blockElement.GetProperty("type").GetString() ?? "text";

                        if (blockType == "table")
                        {
                            var rows = new List<List<string>>();
                            foreach (var rowElement in blockElement.GetProperty("rows").EnumerateArray())
                            {
                                var row = new List<string>();
                                foreach (var cellElement in rowElement.EnumerateArray())
                                    row.Add(cellElement.GetString() ?? "");
                                rows.Add(row);
                            }
                            blocks.Add(new ContentBlock { Type = "table", Rows = rows });
                        }
                        else
                        {
                            string text = blockElement.GetProperty("text").GetString() ?? "";
                            blocks.Add(new ContentBlock { Type = "text", Text = text });
                        }
                    }

                    _chapters.Add(new ManualChapter { Title = title, Blocks = blocks });
                }
            }

            if (_chapters.Count > 0)
            {
                ResultsList.SelectedIndex = 0;
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load JSON manual from {JsonPath}", jsonPath);
            return false;
        }
    }

    private bool TryLoadPdfFallback(string pdfPath)
    {
        if (!File.Exists(pdfPath))
            return false;

        try
        {
            _chapters.Add(new ManualChapter
            {
                Title = "User Manual (PDF Format)",
                Blocks = new List<ContentBlock>
                {
                    new ContentBlock
                    {
                        Type = "text",
                        Text = $"The searchable manual is not available. A PDF version is located at:\n\n{pdfPath}\n\nPlease open this file with your PDF reader to access the full documentation."
                    }
                }
            });
            ResultsList.SelectedIndex = 0;
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load PDF fallback from {PdfPath}", pdfPath);
            return false;
        }
    }

    private void DisplayManualNotFound(string baseDir)
    {
        _chapters.Add(new ManualChapter
        {
            Title = "Help Manual Not Found",
            Blocks = new List<ContentBlock>
            {
                new ContentBlock
                {
                    Type = "text",
                    Text = $"No manual file could be found.\n\nExpected locations:\n- {Path.Combine(baseDir, ManualJsonFilename)}\n- {Path.Combine(baseDir, ManualPdfFilename)}\n\nPlease ensure the manual files are in the application directory."
                }
            }
        });
        ResultsList.SelectedIndex = 0;
    }

    private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResultsList.SelectedItem is ManualChapter chapter)
        {
            RenderChapter(chapter, _pendingHighlightBlockIndex, _pendingHighlightOffset, _pendingHighlightLength);
            _pendingHighlightBlockIndex = -1;
            _pendingHighlightOffset = -1;
            _pendingHighlightLength = 0;
            ScrollToHighlight();
        }
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            PerformFind();
    }

    private void FindButton_Click(object sender, RoutedEventArgs e) => PerformFind();

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_matches.Count == 0)
            return;

        _currentMatchIndex = (_currentMatchIndex + 1) % _matches.Count;
        JumpToMatch(_currentMatchIndex);
    }

    private void PerformFind()
    {
        string term = SearchBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(term))
        {
            ResultCount.Text = "";
            NextButton.IsEnabled = false;
            _matches.Clear();
            _currentMatchIndex = -1;
            return;
        }

        _matches.Clear();
        string termLower = term.ToLower();

        for (int ci = 0; ci < _chapters.Count; ci++)
        {
            var blocks = _chapters[ci].Blocks;
            for (int bi = 0; bi < blocks.Count; bi++)
            {
                if (blocks[bi].Type != "text" || string.IsNullOrEmpty(blocks[bi].Text))
                    continue;

                string contentLower = blocks[bi].Text!.ToLower();
                int idx = 0;
                while ((idx = contentLower.IndexOf(termLower, idx, StringComparison.Ordinal)) >= 0)
                {
                    _matches.Add(new MatchLocation { ChapterIndex = ci, BlockIndex = bi, Offset = idx, Length = term.Length });
                    idx += term.Length;
                }
            }
        }

        if (_matches.Count == 0)
        {
            ResultCount.Text = "No matches found";
            NextButton.IsEnabled = false;
            _currentMatchIndex = -1;
            return;
        }

        NextButton.IsEnabled = _matches.Count > 1;
        _currentMatchIndex = 0;
        JumpToMatch(_currentMatchIndex);
    }

    private void JumpToMatch(int matchIndex)
    {
        var match = _matches[matchIndex];
        ResultCount.Text = $"Match {matchIndex + 1} of {_matches.Count}";

        _pendingHighlightBlockIndex = match.BlockIndex;
        _pendingHighlightOffset = match.Offset;
        _pendingHighlightLength = match.Length;

        if (ResultsList.SelectedIndex == match.ChapterIndex)
        {
            RenderChapter(_chapters[match.ChapterIndex], match.BlockIndex, match.Offset, match.Length);
            _pendingHighlightBlockIndex = -1;
            _pendingHighlightOffset = -1;
            _pendingHighlightLength = 0;
            ScrollToHighlight();
        }
        else
        {
            ResultsList.SelectedIndex = match.ChapterIndex;
        }
    }

    private void RenderChapter(ManualChapter chapter, int highlightBlockIndex = -1, int highlightOffset = -1, int highlightLength = 0)
    {
        try
        {
            _highlightedRun = null;
            var document = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI, Roboto, sans-serif"),
                FontSize = 13,
                PagePadding = new Thickness(0)
            };

            document.Blocks.Add(new Paragraph(new Run(chapter.Title))
            {
                FontWeight = FontWeights.Bold,
                FontSize = 18,
                Margin = new Thickness(0, 0, 0, 12)
            });

            for (int bi = 0; bi < chapter.Blocks.Count; bi++)
            {
                var block = chapter.Blocks[bi];

                if (block.Type == "table" && block.Rows != null)
                {
                    document.Blocks.Add(BuildTable(block.Rows));
                }
                else if (block.Type == "text" && block.Text != null)
                {
                    var para = new Paragraph { Margin = new Thickness(0, 0, 0, 12) };

                    if (bi == highlightBlockIndex && highlightOffset >= 0 && highlightOffset + highlightLength <= block.Text.Length)
                    {
                        AppendTextWithBreaks(para, block.Text.Substring(0, highlightOffset));

                        var highlightRun = new Run(block.Text.Substring(highlightOffset, highlightLength))
                        {
                            Background = Brushes.Yellow,
                            FontWeight = FontWeights.Bold
                        };
                        para.Inlines.Add(highlightRun);
                        _highlightedRun = highlightRun;

                        AppendTextWithBreaks(para, block.Text.Substring(highlightOffset + highlightLength));
                    }
                    else
                    {
                        AppendTextWithBreaks(para, block.Text);
                    }

                    document.Blocks.Add(para);
                }
            }

            ContentViewer.Document = document;

            if (highlightBlockIndex < 0)
                ContentViewer.ScrollToHome();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to display chapter {Chapter}", chapter.Title);
            var errorDoc = new FlowDocument(new Paragraph(new Run($"Error displaying chapter: {chapter.Title}\n\n{ex.Message}")));
            ContentViewer.Document = errorDoc;
        }
    }

    private static Table BuildTable(List<List<string>> rows)
    {
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 12) };

        int colCount = 0;
        foreach (var row in rows)
            colCount = Math.Max(colCount, row.Count);

        for (int i = 0; i < colCount; i++)
            table.Columns.Add(new TableColumn());

        var rowGroup = new TableRowGroup();
        table.RowGroups.Add(rowGroup);

        for (int r = 0; r < rows.Count; r++)
        {
            var tableRow = new TableRow();
            bool isHeader = r == 0;

            foreach (var cellText in rows[r])
            {
                var cell = new TableCell(new Paragraph(new Run(cellText)))
                {
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(0.5),
                    Padding = new Thickness(6, 4, 6, 4)
                };

                if (isHeader)
                {
                    cell.Background = Brushes.LightGray;
                    cell.FontWeight = FontWeights.Bold;
                }

                tableRow.Cells.Add(cell);
            }

            rowGroup.Rows.Add(tableRow);
        }

        return table;
    }

    private static void AppendTextWithBreaks(Paragraph para, string text)
    {
        string[] lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Length > 0)
                para.Inlines.Add(new Run(lines[i]));

            if (i < lines.Length - 1)
                para.Inlines.Add(new LineBreak());
        }
    }

    private void ScrollToHighlight()
    {
        if (_highlightedRun == null)
            return;

        ContentViewer.UpdateLayout();
        Rect rect = _highlightedRun.ContentStart.GetCharacterRect(LogicalDirection.Forward);
        ContentViewer.ScrollToVerticalOffset(Math.Max(0, rect.Top - 40));
    }
}
