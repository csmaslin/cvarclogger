using CvarcLogger.Core.Adif;

namespace CvarcLogger.Core.Csv;

/// <summary>Imports QSO records from CSV format with ADIF field names as column headers.
/// Handles CSV escaping (quoted fields, escaped quotes, multi-line values) and returns AdifRecord objects
/// compatible with AdifFieldMapper for conversion to Qso.</summary>
public static class CsvReader
{
    public static List<AdifRecord> ReadAll(TextReader reader) => ReadAll(reader.ReadToEnd());

    public static List<AdifRecord> ReadAll(string content)
    {
        var records = new List<AdifRecord>();
        var lines = ParseCsvLines(content);

        if (lines.Count == 0)
            return records;

        var headers = lines[0];
        if (headers.Count == 0)
            return records;

        for (int i = 1; i < lines.Count; i++)
        {
            var values = lines[i];
            var record = new AdifRecord();

            for (int j = 0; j < headers.Count && j < values.Count; j++)
            {
                string header = headers[j];
                string value = values[j];

                if (!string.IsNullOrEmpty(header) && !string.IsNullOrEmpty(value))
                {
                    record.Set(header, value);
                }
            }

            records.Add(record);
        }

        return records;
    }

    public static List<AdifRecord> ReadAllFromFile(string path)
    {
        using (var reader = new StreamReader(path))
        {
            return ReadAll(reader);
        }
    }

    /// <summary>Parses CSV content handling quoted fields that may span multiple lines and escaped quotes.
    /// Returns a list of records, where each record is a list of field values.</summary>
    private static List<List<string>> ParseCsvLines(string content)
    {
        var records = new List<List<string>>();
        var currentRecord = new List<string>();
        var currentField = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < content.Length; i++)
        {
            char c = content[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < content.Length && content[i + 1] == '"')
                {
                    // Escaped quote (double quote within quoted field)
                    currentField.Append('"');
                    i++; // Skip the next quote
                }
                else
                {
                    // Toggle quote state
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                // Field separator
                currentRecord.Add(currentField.ToString());
                currentField.Clear();
            }
            else if ((c == '\n' || c == '\r') && !inQuotes)
            {
                // Record separator (only when not inside quotes)
                if (currentField.Length > 0 || currentRecord.Count > 0)
                {
                    currentRecord.Add(currentField.ToString());
                    currentField.Clear();
                    if (currentRecord.Count > 0)
                    {
                        records.Add(currentRecord);
                        currentRecord = new List<string>();
                    }
                }
                // Skip following \n if this was \r\n
                if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
                {
                    i++;
                }
            }
            else if (c != '\r' || inQuotes)  // Include \r only if inside quotes
            {
                currentField.Append(c);
            }
        }

        // Add final field and record
        if (currentField.Length > 0 || currentRecord.Count > 0)
        {
            currentRecord.Add(currentField.ToString());
        }
        if (currentRecord.Count > 0)
        {
            records.Add(currentRecord);
        }

        return records;
    }
}
