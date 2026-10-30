using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace monprojet.NET.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectClientRating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClientRating",
                table: "Projects",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientRating",
                table: "Projects");
        }
    }
}
