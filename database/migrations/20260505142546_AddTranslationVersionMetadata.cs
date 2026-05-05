using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SekaiPlatform.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddTranslationVersionMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "metadata",
                table: "translation_versions",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "metadata",
                table: "translation_versions");
        }
    }
}
