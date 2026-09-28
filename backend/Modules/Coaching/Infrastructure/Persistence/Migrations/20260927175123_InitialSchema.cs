using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextStep.Modules.Coaching.Infrastructure.Persistence.Migrations {
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "coaching");

            migrationBuilder.CreateTable(
                name: "session_coaching",
                schema: "coaching",
                columns: table => new
                {
                    id_session = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    id_candidature = table.Column<Guid>(type: "uuid", nullable: true),
                    id_utilisateur = table.Column<Guid>(type: "uuid", nullable: false),
                    mode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "offer"),
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "en"),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 20),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    domain = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    focus_areas = table.Column<string>(type: "jsonb", nullable: true),
                    score_entretien = table.Column<int>(type: "integer", nullable: true),
                    feedback_json = table.Column<string>(type: "jsonb", nullable: true),
                    date_session = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()"),
                    completed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_coaching", x => x.id_session);
                });

            migrationBuilder.CreateTable(
                name: "question_entrainement",
                schema: "coaching",
                columns: table => new
                {
                    id_question = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    id_session = table.Column<Guid>(type: "uuid", nullable: false),
                    texte_question = table.Column<string>(type: "text", nullable: false),
                    type_question = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    company_specific = table.Column<bool>(type: "boolean", nullable: false),
                    conseil_reponse = table.Column<string>(type: "text", nullable: true),
                    reponse_utilisateur = table.Column<string>(type: "text", nullable: true),
                    correction_ia = table.Column<string>(type: "text", nullable: true),
                    score_reponse = table.Column<int>(type: "integer", nullable: true),
                    ordre = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_entrainement", x => x.id_question);
                    table.ForeignKey(
                        name: "FK_question_entrainement_session_coaching_id_session",
                        column: x => x.id_session,
                        principalSchema: "coaching",
                        principalTable: "session_coaching",
                        principalColumn: "id_session",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_question_session",
                schema: "coaching",
                table: "question_entrainement",
                column: "id_session");

            migrationBuilder.CreateIndex(
                name: "idx_session_candidature",
                schema: "coaching",
                table: "session_coaching",
                column: "id_candidature");

            migrationBuilder.CreateIndex(
                name: "idx_session_user",
                schema: "coaching",
                table: "session_coaching",
                column: "id_utilisateur");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "question_entrainement",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "session_coaching",
                schema: "coaching");
        }
    }
}
