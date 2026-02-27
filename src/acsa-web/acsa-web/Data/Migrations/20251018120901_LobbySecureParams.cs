using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace acsa_web.Data.Migrations
{
    /// <inheritdoc />
    public partial class LobbySecureParams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrentMap",
                table: "Lobbies",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentMode",
                table: "Lobbies",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasStarted",
                table: "Lobbies",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LaunchArgs",
                table: "Lobbies",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PPassword",
                table: "Lobbies",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Port",
                table: "Lobbies",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TokenY",
                table: "Lobbies",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "XPassword",
                table: "Lobbies",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentMap",
                table: "Lobbies");

            migrationBuilder.DropColumn(
                name: "CurrentMode",
                table: "Lobbies");

            migrationBuilder.DropColumn(
                name: "HasStarted",
                table: "Lobbies");

            migrationBuilder.DropColumn(
                name: "LaunchArgs",
                table: "Lobbies");

            migrationBuilder.DropColumn(
                name: "PPassword",
                table: "Lobbies");

            migrationBuilder.DropColumn(
                name: "Port",
                table: "Lobbies");

            migrationBuilder.DropColumn(
                name: "TokenY",
                table: "Lobbies");

            migrationBuilder.DropColumn(
                name: "XPassword",
                table: "Lobbies");
        }
    }
}
