using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIMcpAssistant.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserModuleSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserModuleSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ModuleId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsSubscribed = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserModuleSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserModuleSubscriptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserModuleSubscriptions_ModuleId",
                table: "UserModuleSubscriptions",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserModuleSubscriptions_UserId_ModuleId",
                table: "UserModuleSubscriptions",
                columns: new[] { "UserId", "ModuleId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserModuleSubscriptions");
        }
    }
}
