using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextStep.Modules.Applications.Infrastructure.Persistence.Migrations {
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "applications");

            migrationBuilder.CreateTable(
                name: "offres_emploi",
                schema: "applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    utilisateur_id = table.Column<Guid>(type: "uuid", nullable: false),
                    texte_brut = table.Column<string>(type: "text", nullable: false),
                    analyse_json = table.Column<string>(type: "jsonb", nullable: true),
                    date_creation = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offres_emploi", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "candidature",
                schema: "applications",
                columns: table => new
                {
                    id_candidature = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    id_utilisateur = table.Column<Guid>(type: "uuid", nullable: false),
                    id_offre = table.Column<Guid>(type: "uuid", nullable: true),
                    date_creation = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()"),
                    inclure_lettre_motivation = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    statut = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "ENVOYE"),
                    channel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "EMAIL"),
                    channel_url = table.Column<string>(type: "text", nullable: true),
                    channel_contact = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    application_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()"),
                    applied_manually = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    offer_source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    language = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false, defaultValue: "AUTO"),
                    response_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "EN_ATTENTE"),
                    has_response = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    last_checked_at_utc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    last_response_at_utc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    last_response_from = table.Column<string>(type: "text", nullable: true),
                    last_response_snippet = table.Column<string>(type: "text", nullable: true),
                    response_summary = table.Column<string>(type: "text", nullable: true),
                    recommended_action = table.Column<string>(type: "text", nullable: true),
                    response_confidence = table.Column<double>(type: "double precision", nullable: true),
                    response_classified_at_utc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    follow_up_needed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    last_follow_up_at_utc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidature", x => x.id_candidature);
                    table.ForeignKey(
                        name: "FK_candidature_offres_emploi_id_offre",
                        column: x => x.id_offre,
                        principalSchema: "applications",
                        principalTable: "offres_emploi",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "candidature_note",
                schema: "applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    id_candidature = table.Column<Guid>(type: "uuid", nullable: false),
                    contenu = table.Column<string>(type: "text", nullable: false),
                    auteur = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "user"),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidature_note", x => x.id);
                    table.ForeignKey(
                        name: "FK_candidature_note_candidature_id_candidature",
                        column: x => x.id_candidature,
                        principalSchema: "applications",
                        principalTable: "candidature",
                        principalColumn: "id_candidature",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "candidature_status_history",
                schema: "applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    id_candidature = table.Column<Guid>(type: "uuid", nullable: false),
                    ancien_statut = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    nouveau_statut = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "user"),
                    details = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidature_status_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_candidature_status_history_candidature_id_candidature",
                        column: x => x.id_candidature,
                        principalSchema: "applications",
                        principalTable: "candidature",
                        principalColumn: "id_candidature",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_genere",
                schema: "applications",
                columns: table => new
                {
                    id_document = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    id_candidature = table.Column<Guid>(type: "uuid", nullable: false),
                    cv_contenu_ia_json = table.Column<string>(type: "jsonb", nullable: true),
                    lettre_motiv_contenu_ia = table.Column<string>(type: "text", nullable: true),
                    chemin_pdf_cv = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    chemin_pdf_lettre = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    date_generation = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_genere", x => x.id_document);
                    table.ForeignKey(
                        name: "FK_document_genere_candidature_id_candidature",
                        column: x => x.id_candidature,
                        principalSchema: "applications",
                        principalTable: "candidature",
                        principalColumn: "id_candidature",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_candidature_id_offre",
                schema: "applications",
                table: "candidature",
                column: "id_offre");

            migrationBuilder.CreateIndex(
                name: "ix_candidature_id_utilisateur",
                schema: "applications",
                table: "candidature",
                column: "id_utilisateur");

            migrationBuilder.CreateIndex(
                name: "ix_candidature_note_candidature",
                schema: "applications",
                table: "candidature_note",
                column: "id_candidature");

            migrationBuilder.CreateIndex(
                name: "ix_candidature_status_history_candidature",
                schema: "applications",
                table: "candidature_status_history",
                column: "id_candidature");

            migrationBuilder.CreateIndex(
                name: "IX_document_genere_id_candidature",
                schema: "applications",
                table: "document_genere",
                column: "id_candidature",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_offres_emploi_utilisateur_id",
                schema: "applications",
                table: "offres_emploi",
                column: "utilisateur_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidature_note",
                schema: "applications");

            migrationBuilder.DropTable(
                name: "candidature_status_history",
                schema: "applications");

            migrationBuilder.DropTable(
                name: "document_genere",
                schema: "applications");

            migrationBuilder.DropTable(
                name: "candidature",
                schema: "applications");

            migrationBuilder.DropTable(
                name: "offres_emploi",
                schema: "applications");
        }
    }
}
