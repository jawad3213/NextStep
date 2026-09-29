using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextStep.Modules.CvDocuments.Infrastructure.Persistence.Migrations {
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cv");

            migrationBuilder.CreateTable(
                name: "cv_history",
                schema: "cv",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    template_slug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    template_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    cv_data_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    design_config_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    html_snapshot = table.Column<string>(type: "text", nullable: true),
                    file_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    object_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    bucket_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cv_history", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cv_template",
                schema: "cv",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    slug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    thumbnail_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    industries = table.Column<string>(type: "jsonb", nullable: false),
                    experience_levels = table.Column<string>(type: "jsonb", nullable: false),
                    style = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    layout = table.Column<int>(type: "integer", nullable: false),
                    background_color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false, defaultValue: "#FFFFFF"),
                    tags = table.Column<string>(type: "jsonb", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cv_template", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cv_history_user_created",
                schema: "cv",
                table: "cv_history",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_cv_template_slug",
                schema: "cv",
                table: "cv_template",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cv_history",
                schema: "cv");

            migrationBuilder.DropTable(
                name: "cv_template",
                schema: "cv");
        }
    }
}
