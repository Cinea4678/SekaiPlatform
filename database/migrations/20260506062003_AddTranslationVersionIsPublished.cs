using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SekaiPlatform.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddTranslationVersionIsPublished : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_published",
                table: "translation_versions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_published",
                table: "translation_versions");
        }
    }
}
