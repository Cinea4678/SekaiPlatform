using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SekaiPlatform.Database.Migrations
{
    /// <inheritdoc />
    public partial class ReviewPhase2Constraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_translation_lines_story_source_lines_source_line_id",
                table: "translation_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_translation_lines_translation_versions_version_id",
                table: "translation_lines");

            migrationBuilder.DropIndex(
                name: "IX_translation_lines_source_line_id",
                table: "translation_lines");

            migrationBuilder.DropIndex(
                name: "IX_story_groups_story_type_external_type_external_id",
                table: "story_groups");

            migrationBuilder.AddColumn<long>(
                name: "story_id",
                table: "translation_lines",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_translation_versions_id_story_id",
                table: "translation_versions",
                columns: new[] { "id", "story_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_story_source_lines_id_story_id",
                table: "story_source_lines",
                columns: new[] { "id", "story_id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_user_tenants_role",
                table: "user_tenants",
                sql: "role IN ('normal', 'admin', 'super_admin')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_user_tenants_status",
                table: "user_tenants",
                sql: "status IN ('active', 'disabled', 'deleted')");

            migrationBuilder.CreateIndex(
                name: "IX_translation_versions_tenant_id_created_by",
                table: "translation_versions",
                columns: new[] { "tenant_id", "created_by" });

            migrationBuilder.CreateIndex(
                name: "IX_translation_lines_source_line_id_story_id",
                table: "translation_lines",
                columns: new[] { "source_line_id", "story_id" });

            migrationBuilder.CreateIndex(
                name: "IX_translation_lines_version_id_source_line_id",
                table: "translation_lines",
                columns: new[] { "version_id", "source_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_translation_lines_version_id_story_id",
                table: "translation_lines",
                columns: new[] { "version_id", "story_id" });

            migrationBuilder.CreateIndex(
                name: "IX_tenants_name",
                table: "tenants",
                column: "name",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_sync_jobs_status",
                table: "sync_jobs",
                sql: "status IN ('pending', 'running', 'succeeded', 'failed')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_sync_jobs_trigger_type",
                table: "sync_jobs",
                sql: "trigger_type IN ('manual', 'scheduled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_story_source_lines_line_type",
                table: "story_source_lines",
                sql: "line_type IN ('dialogue', 'scene', 'upper_scene', 'choice', 'separator')");

            migrationBuilder.CreateIndex(
                name: "IX_story_groups_story_type_external_type_external_id",
                table: "story_groups",
                columns: new[] { "story_type", "external_type", "external_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_story_groups_story_type",
                table: "story_groups",
                sql: "story_type IN ('event_story', 'card_story', 'main_story', 'area_talk', 'special_story')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_stories_story_type",
                table: "stories",
                sql: "story_type IN ('event_story', 'card_story', 'main_story', 'area_talk', 'special_story')");

            migrationBuilder.AddForeignKey(
                name: "FK_translation_lines_story_source_lines_source_line_id_story_id",
                table: "translation_lines",
                columns: new[] { "source_line_id", "story_id" },
                principalTable: "story_source_lines",
                principalColumns: new[] { "id", "story_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_translation_lines_translation_versions_version_id_story_id",
                table: "translation_lines",
                columns: new[] { "version_id", "story_id" },
                principalTable: "translation_versions",
                principalColumns: new[] { "id", "story_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_translation_versions_user_tenants_tenant_id_created_by",
                table: "translation_versions",
                columns: new[] { "tenant_id", "created_by" },
                principalTable: "user_tenants",
                principalColumns: new[] { "tenant_id", "user_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_translation_lines_story_source_lines_source_line_id_story_id",
                table: "translation_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_translation_lines_translation_versions_version_id_story_id",
                table: "translation_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_translation_versions_user_tenants_tenant_id_created_by",
                table: "translation_versions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_user_tenants_role",
                table: "user_tenants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_user_tenants_status",
                table: "user_tenants");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_translation_versions_id_story_id",
                table: "translation_versions");

            migrationBuilder.DropIndex(
                name: "IX_translation_versions_tenant_id_created_by",
                table: "translation_versions");

            migrationBuilder.DropIndex(
                name: "IX_translation_lines_source_line_id_story_id",
                table: "translation_lines");

            migrationBuilder.DropIndex(
                name: "IX_translation_lines_version_id_source_line_id",
                table: "translation_lines");

            migrationBuilder.DropIndex(
                name: "IX_translation_lines_version_id_story_id",
                table: "translation_lines");

            migrationBuilder.DropIndex(
                name: "IX_tenants_name",
                table: "tenants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_sync_jobs_status",
                table: "sync_jobs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_sync_jobs_trigger_type",
                table: "sync_jobs");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_story_source_lines_id_story_id",
                table: "story_source_lines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_story_source_lines_line_type",
                table: "story_source_lines");

            migrationBuilder.DropIndex(
                name: "IX_story_groups_story_type_external_type_external_id",
                table: "story_groups");

            migrationBuilder.DropCheckConstraint(
                name: "CK_story_groups_story_type",
                table: "story_groups");

            migrationBuilder.DropCheckConstraint(
                name: "CK_stories_story_type",
                table: "stories");

            migrationBuilder.DropColumn(
                name: "story_id",
                table: "translation_lines");

            migrationBuilder.CreateIndex(
                name: "IX_translation_lines_source_line_id",
                table: "translation_lines",
                column: "source_line_id");

            migrationBuilder.CreateIndex(
                name: "IX_story_groups_story_type_external_type_external_id",
                table: "story_groups",
                columns: new[] { "story_type", "external_type", "external_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_translation_lines_story_source_lines_source_line_id",
                table: "translation_lines",
                column: "source_line_id",
                principalTable: "story_source_lines",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_translation_lines_translation_versions_version_id",
                table: "translation_lines",
                column: "version_id",
                principalTable: "translation_versions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
