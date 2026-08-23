using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CvarcLogger.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContestSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContestSubmissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ContestId = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Callsign = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CategoryOperator = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CategoryAssisted = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CategoryBand = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CategoryMode = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CategoryPower = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CategoryStation = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CategoryTransmitter = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CategoryOverlay = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ClaimedScore = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Club = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AddressCity = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AddressStateProvince = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    AddressPostalCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AddressCountry = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Operators = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SoapBox = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContestSubmissions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContestSubmissions_ContestId",
                table: "ContestSubmissions",
                column: "ContestId");

            migrationBuilder.CreateIndex(
                name: "IX_ContestSubmissions_ModifiedAtUtc",
                table: "ContestSubmissions",
                column: "ModifiedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContestSubmissions");
        }
    }
}
