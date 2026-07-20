using CvarcLogger.Core.Adif;
using CvarcLogger.Core.Models;

namespace CvarcLogger.Tests;

public class AdifRoundTripTests
{
    [Fact]
    public void RoundTrip_PreservesCoreFields()
    {
        var qso = new Qso
        {
            Callsign = "W1AW",
            QsoDateTimeOnUtc = new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
            Band = "20m",
            Mode = "SSB",
            FrequencyMhz = 14.250m,
            RstSent = "59",
            RstRcvd = "57",
            Name = "Hiram",
            GridSquare = "FN31",
            City = "Newington",
            State = "CT",
            Country = "United States",
            ArrlSection = "CT",
            CqZone = 5,
            ItuZone = 8,
            StationCallsign = "N0CALL",
            Comment = "Test contact",
            QslSent = QslStatus.Sent,
            QslRcvd = QslStatus.NotSent,
            Qth = "Downtown Clubhouse",
            Op = "Jane",
        };

        var record = AdifFieldMapper.ToAdifRecord(qso);
        var writer = new StringWriter();
        AdifWriter.WriteRecord(writer, record);

        var parsedRecords = AdifReader.ReadAll(writer.ToString());
        Assert.Single(parsedRecords);

        var roundTripped = AdifFieldMapper.ToQso(parsedRecords[0]);

        Assert.Equal(qso.Callsign, roundTripped.Callsign);
        Assert.Equal(qso.QsoDateTimeOnUtc, roundTripped.QsoDateTimeOnUtc);
        Assert.Equal(qso.Band, roundTripped.Band);
        Assert.Equal(qso.Mode, roundTripped.Mode);
        Assert.Equal(qso.FrequencyMhz, roundTripped.FrequencyMhz);
        Assert.Equal(qso.RstSent, roundTripped.RstSent);
        Assert.Equal(qso.RstRcvd, roundTripped.RstRcvd);
        Assert.Equal(qso.Name, roundTripped.Name);
        Assert.Equal(qso.GridSquare, roundTripped.GridSquare);
        Assert.Equal(qso.City, roundTripped.City);
        Assert.Equal(qso.State, roundTripped.State);
        Assert.Equal(qso.Country, roundTripped.Country);
        Assert.Equal(qso.ArrlSection, roundTripped.ArrlSection);
        Assert.Equal(qso.CqZone, roundTripped.CqZone);
        Assert.Equal(qso.ItuZone, roundTripped.ItuZone);
        Assert.Equal(qso.StationCallsign, roundTripped.StationCallsign);
        Assert.Equal(qso.Comment, roundTripped.Comment);
        Assert.Equal(qso.QslSent, roundTripped.QslSent);
        Assert.Equal(qso.QslRcvd, roundTripped.QslRcvd);
        Assert.Equal(qso.Qth, roundTripped.Qth);
        Assert.Equal(qso.Op, roundTripped.Op);
    }

    [Fact]
    public void RoundTrip_PreservesNonAsciiFieldsAndSubsequentFields()
    {
        // A non-ASCII name is 2-4 bytes per accented character in UTF-8 but only 1 .NET char each --
        // if the length tag were written/read as a char count instead of a byte count, the field after
        // it (GRIDSQUARE here) would desync and misparse. Also covers an astral (surrogate-pair)
        // character, which is 4 bytes in UTF-8 but 2 UTF-16 chars.
        var qso = new Qso
        {
            Callsign = "W1AW",
            Band = "20m",
            Mode = "SSB",
            StationCallsign = "N0CALL",
            QsoDateTimeOnUtc = DateTime.UtcNow,
            Name = "François 😀 Müller",
            GridSquare = "FN31",
        };

        var record = AdifFieldMapper.ToAdifRecord(qso);
        var writer = new StringWriter();
        AdifWriter.WriteRecord(writer, record);
        string adifText = writer.ToString();

        Assert.Contains($"<NAME:{System.Text.Encoding.UTF8.GetByteCount(qso.Name)}>{qso.Name}", adifText);

        var roundTripped = AdifFieldMapper.ToQso(AdifReader.ReadAll(adifText)[0]);
        Assert.Equal(qso.Name, roundTripped.Name);
        Assert.Equal(qso.GridSquare, roundTripped.GridSquare); // proves the field *after* Name parsed correctly too
    }

    [Fact]
    public void ToQso_RecoversLatin1EncodedByte_InsteadOfCorruptingToReplacementChar()
    {
        // Confirmed against a real QRZ Logbook export: its ADIF, despite being nominally UTF-8, contains
        // a raw Windows-1252/Latin-1 byte (0xF1 = ntilde) for an accented name instead of a proper 2-byte
        // UTF-8 sequence, with QRZ's own length tag computed against *that* (1-byte) encoding. The
        // .NET-default UTF-8 decode (Encoding.UTF8.GetString, what StreamReader/File.ReadAllText use)
        // would silently replace that one byte with U+FFFD -- which then re-encodes to 3 bytes, not the
        // original 1, desyncing every field after it in the same record. AdifReader.ReadAll(byte[]) must
        // decode leniently (byte value == code point for any byte that isn't part of a valid UTF-8
        // sequence) to avoid that.
        byte[] bytes = System.Text.Encoding.ASCII.GetBytes("<CALL:4>TEST<NAME:10>Edgar Pe")
            .Concat(new byte[] { 0xF1 })
            .Concat(System.Text.Encoding.ASCII.GetBytes("a<GRIDSQUARE:4>FN31<EOR>"))
            .ToArray();

        var qso = AdifFieldMapper.ToQso(AdifReader.ReadAll(bytes)[0]);

        Assert.Equal("Edgar Peña", qso.Name);
        Assert.Equal("FN31", qso.GridSquare); // proves the field *after* Name parsed correctly too
    }

    [Fact]
    public void ToQso_RecoversFromWrongLengthTag_ByScanningToNextTagBoundary()
    {
        // Also confirmed against the same real QRZ export: some fields already contain an embedded
        // U+FFFD (a *prior*, unrelated corruption event on QRZ's own side, encoded correctly as 3 valid
        // UTF-8 bytes) with QRZ's own length tag undercounting it as 1 character instead of 3 bytes --
        // "<NAME:14>Bj" + U+FFFD (3 bytes) + "rn Karlsson" (11 bytes) is 16 bytes, not the declared 14.
        // A byte-exact reader would trust "14" and truncate the value ("Bj<FFFD>rn Karlss", losing the
        // last two letters) and, worse, leave the parser position two bytes into what should be the next
        // field. Since ADIF's grammar guarantees a value can never itself contain an unescaped '<', the
        // reader should notice the declared length doesn't land on a real tag boundary and recover by
        // scanning forward to the next literal '<' instead -- this can't magically restore the already-
        // lost character, but it must not lose or corrupt anything around it.
        byte[] name = System.Text.Encoding.ASCII.GetBytes("Bj")
            .Concat(new byte[] { 0xEF, 0xBF, 0xBD }) // U+FFFD, correctly encoded as 3 UTF-8 bytes
            .Concat(System.Text.Encoding.ASCII.GetBytes("rn Karlsson"))
            .ToArray();
        byte[] bytes = System.Text.Encoding.ASCII.GetBytes("<CALL:6>SM6LJU<NAME:14>")
            .Concat(name)
            .Concat(System.Text.Encoding.ASCII.GetBytes("<GRIDSQUARE:6>JN45OO<EOR>"))
            .ToArray();

        var qso = AdifFieldMapper.ToQso(AdifReader.ReadAll(bytes)[0]);

        Assert.Equal("Bj�rn Karlsson", qso.Name); // full name recovered except the already-lost character
        Assert.Equal("JN45OO", qso.GridSquare); // proves the field *after* Name parsed correctly too, not just Name itself
    }

    [Fact]
    public void ToAdifRecord_QslSent_NeverEmitsVerified_QslRcvd_NeverEmitsQueued()
    {
        // QSL_SENT's ADIF enumeration is Y/N/R/Q/I (no V); QSL_RCVD's is Y/N/R/V/I (no Q) -- see
        // QslStatus.cs's own "SENT only"/"RCVD only" comments on Queued/Verified.
        var qso = new Qso
        {
            Callsign = "W1AW",
            Band = "20m",
            Mode = "SSB",
            StationCallsign = "N0CALL",
            QsoDateTimeOnUtc = DateTime.UtcNow,
            QslSent = QslStatus.Verified,
            QslRcvd = QslStatus.Queued,
            LotwQslSent = QslStatus.Verified,
            LotwQslRcvd = QslStatus.Queued,
        };

        var record = AdifFieldMapper.ToAdifRecord(qso);

        Assert.NotEqual("V", record.Get("QSL_SENT"));
        Assert.NotEqual("Q", record.Get("QSL_RCVD"));
        Assert.NotEqual("V", record.Get("LOTW_QSL_SENT"));
        Assert.NotEqual("Q", record.Get("LOTW_QSL_RCVD"));
    }

    [Fact]
    public void ToAdifRecord_QslSent_WritesQueued_QslRcvd_WritesVerified()
    {
        var qso = new Qso
        {
            Callsign = "W1AW",
            Band = "20m",
            Mode = "SSB",
            StationCallsign = "N0CALL",
            QsoDateTimeOnUtc = DateTime.UtcNow,
            QslSent = QslStatus.Queued,
            QslRcvd = QslStatus.Verified,
        };

        var record = AdifFieldMapper.ToAdifRecord(qso);

        Assert.Equal("Q", record.Get("QSL_SENT"));
        Assert.Equal("V", record.Get("QSL_RCVD"));
    }

    [Theory]
    [InlineData("1.2M", "1.25M")]
    [InlineData("1.25M", "1.25M")]
    [InlineData("20m", "20m")]
    public void ToAdifRecord_NormalizesBandToken(string internalBand, string expectedAdifBand)
    {
        var qso = new Qso
        {
            Callsign = "W1AW", Mode = "SSB", StationCallsign = "N0CALL", QsoDateTimeOnUtc = DateTime.UtcNow,
            Band = internalBand,
        };

        var record = AdifFieldMapper.ToAdifRecord(qso);

        Assert.Equal(expectedAdifBand, record.Get("BAND"));
    }

    [Theory]
    [InlineData("FT8", null, "FT8", null)]
    [InlineData("FT4", null, "FT4", null)]
    [InlineData("RTTY", null, "RTTY", null)]
    [InlineData("PSK", "PSK31", "PSK", "PSK31")]
    [InlineData("DIGITALVOICE", "DMR", "DIGITALVOICE", "DMR")]
    [InlineData("DIGITALVOICE", "DSTAR", "DIGITALVOICE", "DSTAR")]
    [InlineData("SSB", "USB", "SSB", "USB")]
    public void ToAdifRecord_WritesModeAndSubMode_AsPlainPassthrough(
        string internalMode, string? internalSubMode, string expectedAdifMode, string? expectedAdifSubMode)
    {
        // Mode/SubMode now match ADIF's real vocabulary directly (see QsoFieldOptions), so no
        // translation happens on export -- whatever's stored is written through as-is.
        var qso = new Qso
        {
            Callsign = "W1AW", StationCallsign = "N0CALL", QsoDateTimeOnUtc = DateTime.UtcNow, Band = "20m",
            Mode = internalMode, SubMode = internalSubMode,
        };

        var record = AdifFieldMapper.ToAdifRecord(qso);

        Assert.Equal(expectedAdifMode, record.Get("MODE"));
        Assert.Equal(expectedAdifSubMode, record.Get("SUBMODE"));
    }

    [Theory]
    [InlineData("FT8", null, "FT8", null)]
    [InlineData("FT4", null, "FT4", null)]
    [InlineData("RTTY", null, "RTTY", null)]
    [InlineData("PSK", "PSK31", "PSK", "PSK31")]
    [InlineData("DIGITALVOICE", "DMR", "DIGITALVOICE", "DMR")]
    [InlineData("DIGITALVOICE", "DSTAR", "DIGITALVOICE", "DSTAR")]
    [InlineData("SSB", "USB", "SSB", "USB")]
    public void ToQso_ReadsStandardAdifModes_WithNoTranslation(
        string adifMode, string? adifSubMode, string expectedInternalMode, string? expectedInternalSubMode)
    {
        // Simulates importing a file from other software (WSJT-X, QRZ, etc.) using ADIF's real
        // Mode/SubMode tokens -- these now match our internal representation exactly, so no translation
        // is needed (contrast with the legacy "DATA" case below, which does still need one).
        string adif = adifSubMode is null
            ? $"<CALL:4>TEST<MODE:{adifMode.Length}>{adifMode}<EOR>"
            : $"<CALL:4>TEST<MODE:{adifMode.Length}>{adifMode}<SUBMODE:{adifSubMode.Length}>{adifSubMode}<EOR>";

        var qso = AdifFieldMapper.ToQso(AdifReader.ReadAll(adif)[0]);

        Assert.Equal(expectedInternalMode, qso.Mode);
        Assert.Equal(expectedInternalSubMode, qso.SubMode);
    }

    [Theory]
    [InlineData("FT8", "FT8", null)]
    [InlineData("FT4", "FT4", null)]
    [InlineData("RTTY", "RTTY", null)]
    [InlineData("PSK31", "PSK", "PSK31")]
    [InlineData("DMR", "DIGITALVOICE", "DMR")]
    [InlineData("D-STAR", "DIGITALVOICE", "DSTAR")]
    public void ToQso_TranslatesLegacyDataMode_FromOldCvarcLoggerExports(
        string legacySubMode, string expectedMode, string? expectedSubMode)
    {
        // Backward compatibility: versions through 1.27 wrote the never-valid ADIF Mode "DATA" with the
        // real digital mode in SubMode. Re-importing an old export like that should still land on the
        // current, ADIF-correct representation.
        string adif = $"<CALL:4>TEST<MODE:4>DATA<SUBMODE:{legacySubMode.Length}>{legacySubMode}<EOR>";

        var qso = AdifFieldMapper.ToQso(AdifReader.ReadAll(adif)[0]);

        Assert.Equal(expectedMode, qso.Mode);
        Assert.Equal(expectedSubMode, qso.SubMode);
    }

    [Fact]
    public void RoundTrip_PreservesSotaAndPotaFields()
    {
        var qso = new Qso
        {
            Callsign = "W1AW",
            Band = "20m",
            Mode = "SSB",
            StationCallsign = "N0CALL",
            QsoDateTimeOnUtc = DateTime.UtcNow,
            MySotaRef = "W4G/NG-003",
            SotaRef = "W4T/SU-004",
            MySigInfo = "US-1234",
            SigInfo = "US-5678",
        };

        var record = AdifFieldMapper.ToAdifRecord(qso);
        Assert.Equal("W4G/NG-003", record.Get("MY_SOTA_REF"));
        Assert.Equal("W4T/SU-004", record.Get("SOTA_REF"));
        Assert.Equal("US-1234", record.Get("MY_SIG_INFO"));
        Assert.Equal("US-5678", record.Get("SIG_INFO"));

        var writer = new StringWriter();
        AdifWriter.WriteRecord(writer, record);
        var roundTripped = AdifFieldMapper.ToQso(AdifReader.ReadAll(writer.ToString())[0]);

        Assert.Equal(qso.MySotaRef, roundTripped.MySotaRef);
        Assert.Equal(qso.SotaRef, roundTripped.SotaRef);
        Assert.Equal(qso.MySigInfo, roundTripped.MySigInfo);
        Assert.Equal(qso.SigInfo, roundTripped.SigInfo);
    }

    [Fact]
    public void ToAdifRecord_WritesCountyWithStatePrefix_ForCountyHuntingSoftware()
    {
        var qso = new Qso
        {
            Callsign = "W1AW",
            Band = "20m",
            Mode = "SSB",
            StationCallsign = "N0CALL",
            QsoDateTimeOnUtc = DateTime.UtcNow,
            State = "OH",
            County = "Franklin",
            MyState = "CT",
            MyCounty = "Hartford",
        };

        var record = AdifFieldMapper.ToAdifRecord(qso);

        Assert.Equal("OH,Franklin", record.Get("CNTY"));
        Assert.Equal("CT,Hartford", record.Get("MY_CNTY"));

        var roundTripped = AdifFieldMapper.ToQso(record);
        Assert.Equal("Franklin", roundTripped.County);
        Assert.Equal("Hartford", roundTripped.MyCounty);
    }

    [Fact]
    public void ToQso_ParsesCountyWithoutStatePrefix_FromThirdPartyAdif()
    {
        string adif = "<CALL:4>TEST<CNTY:8>Franklin<EOR>";
        var qso = AdifFieldMapper.ToQso(AdifReader.ReadAll(adif)[0]);

        Assert.Equal("Franklin", qso.County);
    }

    [Fact]
    public void Reader_ParsesMultiLineCommentByLength_NotByLineBreaks()
    {
        string multilineComment = "Line one\r\nLine two\r\nLine three";
        string adif = $"<CALL:4>TEST<COMMENT:{multilineComment.Length}>{multilineComment}<EOR>";

        var records = AdifReader.ReadAll(adif);

        Assert.Single(records);
        Assert.Equal("TEST", records[0].Get("CALL"));
        Assert.Equal(multilineComment, records[0].Get("COMMENT"));
    }

    [Fact]
    public void Reader_HandlesHeaderlessFile()
    {
        string adif = "<CALL:4>TEST<BAND:3>20m<EOR>";

        var records = AdifReader.ReadAll(adif);

        Assert.Single(records);
        Assert.Equal("TEST", records[0].Get("CALL"));
        Assert.Equal("20m", records[0].Get("BAND"));
    }

    [Fact]
    public void Reader_DiscardsHeaderFieldsBeforeEoh()
    {
        string adif = "Generated by Test\r\n<ADIF_VER:5>3.1.4<PROGRAMID:9>CvarcLogger<EOH>\r\n<CALL:4>TEST<EOR>";

        var records = AdifReader.ReadAll(adif);

        Assert.Single(records);
        Assert.Equal("TEST", records[0].Get("CALL"));
        Assert.Null(records[0].Get("ADIF_VER"));
        Assert.Null(records[0].Get("PROGRAMID"));
    }

    [Fact]
    public void UnknownField_RoundTripsThroughExtraFieldsJson()
    {
        string adif = "<CALL:4>TEST<BAND:3>20m<MY_RIG:7>IC-7300<EOR>";
        var records = AdifReader.ReadAll(adif);
        var qso = AdifFieldMapper.ToQso(records[0]);

        Assert.NotNull(qso.AdifExtraFieldsJson);
        Assert.Contains("MY_RIG", qso.AdifExtraFieldsJson);

        var record = AdifFieldMapper.ToAdifRecord(qso);
        Assert.Equal("IC-7300", record.Get("MY_RIG"));
    }

    [Fact]
    public void WriteAll_ProducesParsableFileWithHeader()
    {
        var qsos = new List<Qso>
        {
            new() { Callsign = "AA1AA", Band = "40m", Mode = "CW", StationCallsign = "N0CALL", QsoDateTimeOnUtc = DateTime.UtcNow },
            new() { Callsign = "BB2BB", Band = "15m", Mode = "FT8", StationCallsign = "N0CALL", QsoDateTimeOnUtc = DateTime.UtcNow },
        };

        var writer = new StringWriter();
        AdifWriter.WriteAll(writer, qsos.Select(AdifFieldMapper.ToAdifRecord));

        var parsed = AdifReader.ReadAll(writer.ToString());

        Assert.Equal(2, parsed.Count);
        Assert.Equal("AA1AA", parsed[0].Get("CALL"));
        Assert.Equal("BB2BB", parsed[1].Get("CALL"));
    }
}
