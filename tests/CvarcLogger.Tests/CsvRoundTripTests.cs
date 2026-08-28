using CvarcLogger.Core.Adif;
using CvarcLogger.Core.Csv;
using CvarcLogger.Core.Models;

namespace CvarcLogger.Tests;

public class CsvRoundTripTests
{
    [Fact]
    public void ExportAndImportPreservesQsoData()
    {
        // Create test QSOs
        var qso1 = new Qso
        {
            Callsign = "W5XYZ",
            Band = "20M",
            Mode = "CW",
            SubMode = null,
            QsoDateTimeOnUtc = new DateTime(2024, 8, 15, 14, 30, 0, DateTimeKind.Utc),
            Name = "John Doe",
            GridSquare = "DM34",
            City = "Denver",
            State = "CO",
            Country = "USA",
            Comment = "Nice signal, 599"
        };

        var qso2 = new Qso
        {
            Callsign = "VE3ABC",
            Band = "40M",
            Mode = "SSB",
            SubMode = null,
            QsoDateTimeOnUtc = new DateTime(2024, 8, 15, 15, 45, 0, DateTimeKind.Utc),
            Name = "Jane Smith",
            Comment = "Had 'quotes' and, commas in comment"
        };

        var qsos = new List<Qso> { qso1, qso2 };

        // Export to CSV
        var csvContent = ExportToString(qsos);

        // Verify CSV structure
        var lines = csvContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        Assert.True(lines.Length >= 3, "CSV should have header + 2 data rows");
        Assert.Contains("CALL", lines[0]);
        Assert.Contains("QSO_DATE", lines[0]);

        // Import from CSV
        var importedRecords = CsvReader.ReadAll(csvContent);
        Assert.Equal(2, importedRecords.Count);

        var importedQso1 = AdifFieldMapper.ToQso(importedRecords[0]);
        var importedQso2 = AdifFieldMapper.ToQso(importedRecords[1]);

        // Verify first QSO
        Assert.Equal("W5XYZ", importedQso1.Callsign);
        Assert.Equal("20M", importedQso1.Band);
        Assert.Equal("CW", importedQso1.Mode);
        Assert.Equal("John Doe", importedQso1.Name);
        Assert.Equal("Denver", importedQso1.City);
        Assert.Equal("CO", importedQso1.State);
        Assert.Equal("USA", importedQso1.Country);
        Assert.Equal("Nice signal, 599", importedQso1.Comment);

        // Verify second QSO (with quotes and commas)
        Assert.Equal("VE3ABC", importedQso2.Callsign);
        Assert.Equal("40M", importedQso2.Band);
        Assert.Equal("SSB", importedQso2.Mode);
        Assert.Equal("Jane Smith", importedQso2.Name);
        Assert.Equal("Had 'quotes' and, commas in comment", importedQso2.Comment);
    }

    [Fact]
    public void CsvHandlesSpecialCharactersCorrectly()
    {
        var qso = new Qso
        {
            Callsign = "K1ABC",
            Band = "10M",
            Mode = "FT8",
            QsoDateTimeOnUtc = DateTime.UtcNow,
            Comment = "Line1\nLine2\rLine3\r\nLine4 with \"quotes\" and, commas"
        };

        var qsos = new List<Qso> { qso };
        var csvContent = ExportToString(qsos);
        var importedRecords = CsvReader.ReadAll(csvContent);
        var importedQso = AdifFieldMapper.ToQso(importedRecords[0]);

        Assert.Equal(qso.Comment, importedQso.Comment);
    }

    [Fact]
    public void CsvHandlesEmptyFields()
    {
        var qso = new Qso
        {
            Callsign = "N0CALL",
            Band = "20M",
            Mode = "CW",
            QsoDateTimeOnUtc = DateTime.UtcNow,
            Name = null,
            Comment = null
        };

        var qsos = new List<Qso> { qso };
        var csvContent = ExportToString(qsos);
        var importedRecords = CsvReader.ReadAll(csvContent);
        var importedQso = AdifFieldMapper.ToQso(importedRecords[0]);

        Assert.Equal("N0CALL", importedQso.Callsign);
        Assert.Null(importedQso.Name);
        Assert.Null(importedQso.Comment);
    }

    private static string ExportToString(List<Qso> qsos)
    {
        var adifRecords = qsos.Select(AdifFieldMapper.ToAdifRecord);
        using var writer = new StringWriter();
        CsvWriter.WriteAll(writer, adifRecords);
        return writer.ToString();
    }
}
