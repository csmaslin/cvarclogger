using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CvarcLogger.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStationTimeZoneFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ObservesDaylightSavingTime",
                table: "StationProfiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "UtcOffsetHours",
                table: "StationProfiles",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "ObservesDaylightSavingTime",
                table: "Qsos",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "UtcOffsetHours",
                table: "Qsos",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ObservesDaylightSavingTime",
                table: "StationProfiles");

            migrationBuilder.DropColumn(
                name: "UtcOffsetHours",
                table: "StationProfiles");

            migrationBuilder.DropColumn(
                name: "ObservesDaylightSavingTime",
                table: "Qsos");

            migrationBuilder.DropColumn(
                name: "UtcOffsetHours",
                table: "Qsos");
        }
    }
}
