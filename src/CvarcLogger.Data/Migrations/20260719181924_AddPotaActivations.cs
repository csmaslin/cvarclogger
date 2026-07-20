using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CvarcLogger.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPotaActivations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PotaActivations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ParkReference = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ParkName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Activated = table.Column<bool>(type: "INTEGER", nullable: false),
                    ActivationDateUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TotalQsoCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PotaActivations", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PotaActivations");
        }
    }
}
