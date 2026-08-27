using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CvarcLogger.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameQsoNotesToComment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Notes",
                table: "Qsos",
                newName: "Comment");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Comment",
                table: "Qsos",
                newName: "Notes");
        }
    }
}
