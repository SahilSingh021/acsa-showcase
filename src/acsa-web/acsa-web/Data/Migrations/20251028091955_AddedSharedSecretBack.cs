using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace acsa_web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddedSharedSecretBack : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SharedSecret",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SharedSecret",
                table: "AspNetUsers");
        }
    }
}
