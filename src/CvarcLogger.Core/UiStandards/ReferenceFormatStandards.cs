using System.Text.RegularExpressions;

namespace CvarcLogger.Core.UiStandards;

/// <summary>Format-validation regexes for SOTA and POTA references, entered freehand in several places
/// across both apps (QSO entry/edit, and CvarcCellLog's SOTA OPR/POTA OPR "my default reference" pages).
/// Both formats use a variable-length prefix -- SOTA's association is 1-4 alphanumeric characters
/// (G, W6, W0C, SV9), POTA's program prefix is 1-3 (K, VE, VP8) -- so a rigid character-by-character
/// input mask can't express either one without locking out valid references (W6/CT-003 needs a 2-char
/// prefix, W0C/FR-043 needs 3, G/LD-001 needs 1); regex is used here instead of a fixed mask for exactly
/// that reason.</summary>
public static class ReferenceFormatStandards
{
    // Association (1-4 alphanumeric) / Region (2 alpha) - Summit number (3 digits). e.g. W6/CT-003, W0C/FR-043, G/LD-001.
    private static readonly Regex SotaRefRegex = new(@"^[A-Z0-9]{1,4}/[A-Z]{2}-\d{3}$", RegexOptions.Compiled);

    // Program prefix (1-3 alphanumeric) - Park number (4-5 digits, zero-padded). e.g. K-0001, VE-1234, K-12345.
    private static readonly Regex PotaRefRegex = new(@"^[A-Z0-9]{1,3}-\d{4,5}$", RegexOptions.Compiled);

    /// <summary>An empty/whitespace-only value is considered valid -- these fields are optional, so a
    /// blank field shouldn't show a format warning.</summary>
    public static bool IsValidSotaRef(string? value) => string.IsNullOrWhiteSpace(value) || SotaRefRegex.IsMatch(value.Trim());

    public static bool IsValidPotaRef(string? value) => string.IsNullOrWhiteSpace(value) || PotaRefRegex.IsMatch(value.Trim());
}
