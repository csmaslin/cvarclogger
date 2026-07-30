using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CvarcLogger.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContestAndSkccFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SkccNr",
                table: "StationProfiles",
                type: "TEXT",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Check",
                table: "Qsos",
                type: "TEXT",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Class",
                table: "Qsos",
                type: "TEXT",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MySkccNr",
                table: "Qsos",
                type: "TEXT",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Precedence",
                table: "Qsos",
                type: "TEXT",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SkccNr",
                table: "Qsos",
                type: "TEXT",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SkccNr",
                table: "StationProfiles");

            migrationBuilder.DropColumn(
                name: "Check",
                table: "Qsos");

            migrationBuilder.DropColumn(
                name: "Class",
                table: "Qsos");

            migrationBuilder.DropColumn(
                name: "MySkccNr",
                table: "Qsos");

            migrationBuilder.DropColumn(
                name: "Precedence",
                table: "Qsos");

            migrationBuilder.DropColumn(
                name: "SkccNr",
                table: "Qsos");
        }
    }
}
