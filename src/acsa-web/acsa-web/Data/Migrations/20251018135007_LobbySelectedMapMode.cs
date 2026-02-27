using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace acsa_web.Data.Migrations
{
    /// <inheritdoc />
    public partial class LobbySelectedMapMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SelectedMap",
                table: "Lobbies",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SelectedMode",
                table: "Lobbies",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SelectedMap",
                table: "Lobbies");

            migrationBuilder.DropColumn(
                name: "SelectedMode",
                table: "Lobbies");
        }
    }
}
