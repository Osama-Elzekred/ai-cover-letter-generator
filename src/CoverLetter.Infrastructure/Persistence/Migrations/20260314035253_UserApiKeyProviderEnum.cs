using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoverLetter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserApiKeyProviderEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_api_keys_userid",
                table: "user_api_keys");

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "user_api_keys",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE user_api_keys
                SET "Provider" = 'Groq'
                WHERE "Provider" IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Provider",
                table: "user_api_keys",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_api_keys_userid_provider",
                table: "user_api_keys",
                columns: new[] { "UserId", "Provider" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_api_keys_userid_provider",
                table: "user_api_keys");

            migrationBuilder.AlterColumn<string>(
                name: "Provider",
                table: "user_api_keys",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "user_api_keys");

            migrationBuilder.CreateIndex(
                name: "ix_user_api_keys_userid",
                table: "user_api_keys",
                column: "UserId",
                unique: true);
        }
    }
}
