using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CvarcLogger.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStationQthOp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Op",
                table: "StationProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Qth",
                table: "StationProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Op",
                table: "Qsos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Qth",
                table: "Qsos",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Op",
                table: "StationProfiles");

            migrationBuilder.DropColumn(
                name: "Qth",
                table: "StationProfiles");

            migrationBuilder.DropColumn(
                name: "Op",
                table: "Qsos");

            migrationBuilder.DropColumn(
                name: "Qth",
                table: "Qsos");
        }
    }
}
