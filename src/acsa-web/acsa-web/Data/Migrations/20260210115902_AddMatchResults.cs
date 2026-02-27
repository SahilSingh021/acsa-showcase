using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace acsa_web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Level",
                table: "UserLogs",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateTable(
                name: "GameMatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Map = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LengthMs = table.Column<int>(type: "int", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RawJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameMatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameMatchPlayers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    PersistentUid = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Team = table.Column<int>(type: "int", nullable: false),
                    Kills = table.Column<int>(type: "int", nullable: false),
                    Deaths = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameMatchPlayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameMatchPlayers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GameMatchPlayers_GameMatches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "GameMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameMatches_EndedAt",
                table: "GameMatches",
                column: "EndedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GameMatchPlayers_MatchId",
                table: "GameMatchPlayers",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_GameMatchPlayers_MatchId_PersistentUid",
                table: "GameMatchPlayers",
                columns: new[] { "MatchId", "PersistentUid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameMatchPlayers_UserId_MatchId",
                table: "GameMatchPlayers",
                columns: new[] { "UserId", "MatchId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameMatchPlayers");

            migrationBuilder.DropTable(
                name: "GameMatches");

            migrationBuilder.AlterColumn<int>(
                name: "Level",
                table: "UserLogs",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);
        }
    }
}
