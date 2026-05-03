using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SekaiPlatform.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "story_groups",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    story_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    external_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    external_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    display_no = table.Column<int>(type: "integer", nullable: true),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    subtitle = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_story_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sync_jobs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    job_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    trigger_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    avatar_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    qq_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    display_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    avatar_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stories",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    group_id = table.Column<long>(type: "bigint", nullable: true),
                    story_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    scenario_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stories", x => x.id);
                    table.ForeignKey(
                        name: "FK_stories_story_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "story_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_oauthes",
                columns: table => new
                {
                    oauth_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    oauth_id = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_oauthes", x => new { x.oauth_type, x.oauth_id });
                    table.ForeignKey(
                        name: "FK_user_oauthes_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_tenants",
                columns: table => new
                {
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_tenants", x => new { x.tenant_id, x.user_id });
                    table.ForeignKey(
                        name: "FK_user_tenants_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_tenants_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "story_source_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    story_id = table.Column<long>(type: "bigint", nullable: false),
                    line_no = table.Column<int>(type: "integer", nullable: false),
                    line_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    speaker = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    text = table.Column<string>(type: "text", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_story_source_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_story_source_lines_stories_story_id",
                        column: x => x.story_id,
                        principalTable: "stories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "translation_versions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    story_id = table.Column<long>(type: "bigint", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translation_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_translation_versions_stories_story_id",
                        column: x => x.story_id,
                        principalTable: "stories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_translation_versions_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_translation_versions_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "translation_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    version_id = table.Column<long>(type: "bigint", nullable: false),
                    source_line_id = table.Column<long>(type: "bigint", nullable: false),
                    line_no = table.Column<int>(type: "integer", nullable: false),
                    speaker = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    text = table.Column<string>(type: "text", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translation_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_translation_lines_story_source_lines_source_line_id",
                        column: x => x.source_line_id,
                        principalTable: "story_source_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_translation_lines_translation_versions_version_id",
                        column: x => x.version_id,
                        principalTable: "translation_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stories_group_id",
                table: "stories",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "IX_stories_story_type_scenario_id",
                table: "stories",
                columns: new[] { "story_type", "scenario_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_story_groups_story_type_external_type_external_id",
                table: "story_groups",
                columns: new[] { "story_type", "external_type", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_story_source_lines_story_id_line_no",
                table: "story_source_lines",
                columns: new[] { "story_id", "line_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sync_jobs_status_created_at",
                table: "sync_jobs",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_translation_lines_source_line_id",
                table: "translation_lines",
                column: "source_line_id");

            migrationBuilder.CreateIndex(
                name: "IX_translation_lines_version_id_line_no",
                table: "translation_lines",
                columns: new[] { "version_id", "line_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_translation_versions_created_by",
                table: "translation_versions",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_translation_versions_story_id",
                table: "translation_versions",
                column: "story_id");

            migrationBuilder.CreateIndex(
                name: "IX_translation_versions_tenant_id_story_id_version_no",
                table: "translation_versions",
                columns: new[] { "tenant_id", "story_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_oauthes_user_id",
                table: "user_oauthes",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_tenants_user_id",
                table: "user_tenants",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_qq_id",
                table: "users",
                column: "qq_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sync_jobs");

            migrationBuilder.DropTable(
                name: "translation_lines");

            migrationBuilder.DropTable(
                name: "user_oauthes");

            migrationBuilder.DropTable(
                name: "user_tenants");

            migrationBuilder.DropTable(
                name: "story_source_lines");

            migrationBuilder.DropTable(
                name: "translation_versions");

            migrationBuilder.DropTable(
                name: "stories");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "story_groups");
        }
    }
}
