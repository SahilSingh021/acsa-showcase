using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace acsa_web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServerPidToLobby : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ServerPid",
                table: "Lobbies",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ServerPid",
                table: "Lobbies");
        }
    }
}
