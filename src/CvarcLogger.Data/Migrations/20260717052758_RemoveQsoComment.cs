using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CvarcLogger.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveQsoComment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Comment",
                table: "Qsos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Comment",
                table: "Qsos",
                type: "TEXT",
                nullable: true);
        }
    }
}
