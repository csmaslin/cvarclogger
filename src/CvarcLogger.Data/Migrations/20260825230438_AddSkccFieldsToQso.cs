using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CvarcLogger.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSkccFieldsToQso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SkccMemberNumber",
                table: "Qsos",
                type: "TEXT",
                nullable: true,
                comment: "What they sent: \"1234\" or \"1234S\" (with tier suffix)");

            migrationBuilder.AddColumn<string>(
                name: "SkccMemberStatus",
                table: "Qsos",
                type: "TEXT",
                nullable: true,
                comment: "Parsed from member number suffix: \"C\", \"T\", \"S\", or null");

            migrationBuilder.AddColumn<string>(
                name: "SkccOperatorName",
                table: "Qsos",
                type: "TEXT",
                nullable: true,
                comment: "Their operator name: \"PETE\"");

            migrationBuilder.AddColumn<string>(
                name: "SkccEventType",
                table: "Qsos",
                type: "TEXT",
                nullable: true,
                comment: "Event type: \"SKS\", \"WES\", \"SKCC-QSO\", etc.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SkccEventType",
                table: "Qsos");

            migrationBuilder.DropColumn(
                name: "SkccOperatorName",
                table: "Qsos");

            migrationBuilder.DropColumn(
                name: "SkccMemberStatus",
                table: "Qsos");

            migrationBuilder.DropColumn(
                name: "SkccMemberNumber",
                table: "Qsos");
        }
    }
}
