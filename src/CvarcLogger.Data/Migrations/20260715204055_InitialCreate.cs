using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CvarcLogger.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DxccEntities",
                columns: table => new
                {
                    EntityCode = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EntityName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Continent = table.Column<string>(type: "TEXT", nullable: true),
                    CqZone = table.Column<int>(type: "INTEGER", nullable: true),
                    ItuZone = table.Column<int>(type: "INTEGER", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DxccEntities", x => x.EntityCode);
                });

            migrationBuilder.CreateTable(
                name: "StationProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Callsign = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    OperatorCallsign = table.Column<string>(type: "TEXT", nullable: true),
                    MyGridSquare = table.Column<string>(type: "TEXT", nullable: true),
                    MyState = table.Column<string>(type: "TEXT", nullable: true),
                    MyCounty = table.Column<string>(type: "TEXT", nullable: true),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StationProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrefixMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Prefix = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    DxccEntityCode = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrefixMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrefixMappings_DxccEntities_DxccEntityCode",
                        column: x => x.DxccEntityCode,
                        principalTable: "DxccEntities",
                        principalColumn: "EntityCode",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Qsos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Callsign = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    QsoDateTimeOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    QsoDateTimeOffUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Band = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Mode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SubMode = table.Column<string>(type: "TEXT", nullable: true),
                    FrequencyMhz = table.Column<decimal>(type: "TEXT", nullable: true),
                    FrequencyRxMhz = table.Column<decimal>(type: "TEXT", nullable: true),
                    RstSent = table.Column<string>(type: "TEXT", nullable: true),
                    RstRcvd = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    GridSquare = table.Column<string>(type: "TEXT", nullable: true),
                    County = table.Column<string>(type: "TEXT", nullable: true),
                    State = table.Column<string>(type: "TEXT", nullable: true),
                    Country = table.Column<string>(type: "TEXT", nullable: true),
                    DxccEntityCode = table.Column<int>(type: "INTEGER", nullable: true),
                    DxccEntityOverride = table.Column<bool>(type: "INTEGER", nullable: false),
                    Continent = table.Column<string>(type: "TEXT", nullable: true),
                    CqZone = table.Column<int>(type: "INTEGER", nullable: true),
                    ItuZone = table.Column<int>(type: "INTEGER", nullable: true),
                    TxPowerWatts = table.Column<decimal>(type: "TEXT", nullable: true),
                    QslSent = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    QslRcvd = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    QslSentDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    QslRcvdDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LotwQslSent = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    LotwQslRcvd = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    LotwQslSentDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LotwQslRcvdDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    QslViaCallsign = table.Column<string>(type: "TEXT", nullable: true),
                    Comment = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    StationProfileId = table.Column<int>(type: "INTEGER", nullable: true),
                    StationCallsign = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    OperatorCallsign = table.Column<string>(type: "TEXT", nullable: true),
                    MyGridSquare = table.Column<string>(type: "TEXT", nullable: true),
                    MyState = table.Column<string>(type: "TEXT", nullable: true),
                    MyCounty = table.Column<string>(type: "TEXT", nullable: true),
                    ContestId = table.Column<string>(type: "TEXT", nullable: true),
                    StxSerial = table.Column<int>(type: "INTEGER", nullable: true),
                    SrxSerial = table.Column<int>(type: "INTEGER", nullable: true),
                    AdifExtraFieldsJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Qsos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Qsos_DxccEntities_DxccEntityCode",
                        column: x => x.DxccEntityCode,
                        principalTable: "DxccEntities",
                        principalColumn: "EntityCode",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Qsos_StationProfiles_StationProfileId",
                        column: x => x.StationProfileId,
                        principalTable: "StationProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrefixMappings_DxccEntityCode",
                table: "PrefixMappings",
                column: "DxccEntityCode");

            migrationBuilder.CreateIndex(
                name: "IX_PrefixMappings_Prefix",
                table: "PrefixMappings",
                column: "Prefix",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Qsos_Callsign",
                table: "Qsos",
                column: "Callsign");

            migrationBuilder.CreateIndex(
                name: "IX_Qsos_DxccEntityCode",
                table: "Qsos",
                column: "DxccEntityCode");

            migrationBuilder.CreateIndex(
                name: "IX_Qsos_QsoDateTimeOnUtc",
                table: "Qsos",
                column: "QsoDateTimeOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Qsos_StationProfileId",
                table: "Qsos",
                column: "StationProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrefixMappings");

            migrationBuilder.DropTable(
                name: "Qsos");

            migrationBuilder.DropTable(
                name: "DxccEntities");

            migrationBuilder.DropTable(
                name: "StationProfiles");
        }
    }
}
