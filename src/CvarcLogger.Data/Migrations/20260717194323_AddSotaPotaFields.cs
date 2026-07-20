using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CvarcLogger.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSotaPotaFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MySigInfo",
                table: "Qsos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MySotaRef",
                table: "Qsos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SigInfo",
                table: "Qsos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SotaRef",
                table: "Qsos",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MySigInfo",
                table: "Qsos");

            migrationBuilder.DropColumn(
                name: "MySotaRef",
                table: "Qsos");

            migrationBuilder.DropColumn(
                name: "SigInfo",
                table: "Qsos");

            migrationBuilder.DropColumn(
                name: "SotaRef",
                table: "Qsos");
        }
    }
}
