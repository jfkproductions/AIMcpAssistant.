using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIMcpAssistant.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJwtTokenToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JwtTokenEncrypted",
                table: "Users",
                type: "TEXT",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "JwtTokenExpiresAt",
                table: "Users",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JwtTokenEncrypted",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "JwtTokenExpiresAt",
                table: "Users");
        }
    }
}