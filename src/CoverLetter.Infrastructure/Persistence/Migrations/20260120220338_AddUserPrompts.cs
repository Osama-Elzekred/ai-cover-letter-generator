using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoverLetter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPrompts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_prompts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PromptType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_prompts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_prompts_userid_prompttype",
                table: "user_prompts",
                columns: new[] { "UserId", "PromptType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_prompts");
        }
    }
}
