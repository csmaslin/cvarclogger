using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CvarcLogger.Data.Migrations
{
    /// <summary>Data-only migration (no schema change): versions through 1.27 stored the never-valid
    /// ADIF Mode "DATA" with the real digital mode in SubMode (see QsoFieldOptions and
    /// AdifFieldMapper.NormalizeLegacyDataMode, which does the equivalent translation for a re-imported
    /// old .adi export). Rewrites any already-logged QSOs still using that scheme to the current,
    /// ADIF-correct Mode/SubMode pair, so the Edit QSO window's Sub-Mode picker keeps showing the right
    /// choices for them without a permanent backward-compat shim in the UI layer.</summary>
    public partial class MigrateLegacyDataModeToAdifModes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Qsos SET Mode = 'FT8', SubMode = NULL WHERE Mode = 'DATA' AND SubMode = 'FT8';");
            migrationBuilder.Sql("UPDATE Qsos SET Mode = 'FT4', SubMode = NULL WHERE Mode = 'DATA' AND SubMode = 'FT4';");
            migrationBuilder.Sql("UPDATE Qsos SET Mode = 'RTTY', SubMode = NULL WHERE Mode = 'DATA' AND SubMode = 'RTTY';");
            migrationBuilder.Sql("UPDATE Qsos SET Mode = 'PSK', SubMode = 'PSK31' WHERE Mode = 'DATA' AND SubMode = 'PSK31';");
            migrationBuilder.Sql("UPDATE Qsos SET Mode = 'DIGITALVOICE', SubMode = 'DMR' WHERE Mode = 'DATA' AND SubMode = 'DMR';");
            migrationBuilder.Sql("UPDATE Qsos SET Mode = 'DIGITALVOICE', SubMode = 'DSTAR' WHERE Mode = 'DATA' AND SubMode = 'D-STAR';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort: a QSO genuinely imported with Mode=RTTY/FT8/etc. after this migration ran
            // (as opposed to one this migration itself just converted) is indistinguishable from one
            // that was, so rolling back can reclassify some already-legitimate rows back into the old
            // "DATA" bucket. Only run Down if you actually need to un-apply this on a fresh-enough
            // database where that risk doesn't matter.
            migrationBuilder.Sql("UPDATE Qsos SET Mode = 'DATA', SubMode = 'FT8' WHERE Mode = 'FT8' AND SubMode IS NULL;");
            migrationBuilder.Sql("UPDATE Qsos SET Mode = 'DATA', SubMode = 'FT4' WHERE Mode = 'FT4' AND SubMode IS NULL;");
            migrationBuilder.Sql("UPDATE Qsos SET Mode = 'DATA', SubMode = 'RTTY' WHERE Mode = 'RTTY' AND SubMode IS NULL;");
            migrationBuilder.Sql("UPDATE Qsos SET Mode = 'DATA', SubMode = 'PSK31' WHERE Mode = 'PSK' AND SubMode = 'PSK31';");
            migrationBuilder.Sql("UPDATE Qsos SET Mode = 'DATA', SubMode = 'DMR' WHERE Mode = 'DIGITALVOICE' AND SubMode = 'DMR';");
            migrationBuilder.Sql("UPDATE Qsos SET Mode = 'DATA', SubMode = 'D-STAR' WHERE Mode = 'DIGITALVOICE' AND SubMode = 'DSTAR';");
        }
    }
}
