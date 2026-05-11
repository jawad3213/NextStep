using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextStep.Migrations
{
    /// <inheritdoc />
    public partial class AddOffersAndDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.RenameTable(
                name: "utilisateur",
                newName: "utilisateur",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "projet",
                newName: "projet",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "formation",
                newName: "formation",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "experience",
                newName: "experience",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "competence",
                newName: "competence",
                newSchema: "public");

            migrationBuilder.AlterColumn<DateTime>(
                name: "date_inscription",
                schema: "public",
                table: "utilisateur",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "pays",
                schema: "public",
                table: "utilisateur",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "photo_url",
                schema: "public",
                table: "utilisateur",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "telephone",
                schema: "public",
                table: "utilisateur",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "titre_poste",
                schema: "public",
                table: "utilisateur",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "titres_sections",
                schema: "public",
                table: "utilisateur",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ville",
                schema: "public",
                table: "utilisateur",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "date_realisation",
                schema: "public",
                table: "projet",
                type: "timestamp without time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "demo_url",
                schema: "public",
                table: "projet",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "image_url",
                schema: "public",
                table: "projet",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_university",
                schema: "public",
                table: "projet",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "annee_fin",
                schema: "public",
                table: "formation",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "mention",
                schema: "public",
                table: "formation",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "specialisation",
                schema: "public",
                table: "formation",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ville",
                schema: "public",
                table: "formation",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "date_fin",
                schema: "public",
                table: "experience",
                type: "timestamp without time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "date_debut",
                schema: "public",
                table: "experience",
                type: "timestamp without time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "type_contrat",
                schema: "public",
                table: "experience",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ville",
                schema: "public",
                table: "experience",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "certification",
                schema: "public",
                columns: table => new
                {
                    id_certification = table.Column<Guid>(type: "uuid", nullable: false),
                    id_utilisateur = table.Column<Guid>(type: "uuid", nullable: false),
                    titre = table.Column<string>(type: "text", nullable: true),
                    organisation = table.Column<string>(type: "text", nullable: true),
                    date_obtention = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    id_credential = table.Column<string>(type: "text", nullable: true),
                    url_credential = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certification", x => x.id_certification);
                });

            migrationBuilder.CreateTable(
                name: "cv_history",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    template_slug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    template_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    cv_data_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
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
                schema: "public",
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

            migrationBuilder.CreateTable(
                name: "offres_emploi",
                schema: "public",
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
                name: "skill_keyword",
                schema: "public",
                columns: table => new
                {
                    id_skill_keyword = table.Column<Guid>(type: "uuid", nullable: false),
                    mot = table.Column<string>(type: "text", nullable: false),
                    categorie = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skill_keyword", x => x.id_skill_keyword);
                });

            migrationBuilder.CreateTable(
                name: "candidature",
                schema: "public",
                columns: table => new
                {
                    id_candidature = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    id_utilisateur = table.Column<Guid>(type: "uuid", nullable: false),
                    id_offre = table.Column<Guid>(type: "uuid", nullable: false),
                    date_creation = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()"),
                    inclure_lettre_motivation = table.Column<bool>(type: "boolean", nullable: false),
                    statut = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "EN_ATTENTE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidature", x => x.id_candidature);
                    table.ForeignKey(
                        name: "FK_candidature_offres_emploi_id_offre",
                        column: x => x.id_offre,
                        principalSchema: "public",
                        principalTable: "offres_emploi",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_genere",
                schema: "public",
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
                        principalSchema: "public",
                        principalTable: "candidature",
                        principalColumn: "id_candidature",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "email_draft",
                schema: "public",
                columns: table => new
                {
                    id_email_draft = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    id_candidature = table.Column<Guid>(type: "uuid", nullable: false),
                    type_email = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "application"),
                    recipient_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    objet = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    corps = table.Column<string>(type: "text", nullable: false),
                    langue = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "fr"),
                    est_approuve = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    est_envoye = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    date_creation = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()"),
                    date_modification = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    date_envoi = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_draft", x => x.id_email_draft);
                    table.ForeignKey(
                        name: "FK_email_draft_candidature_id_candidature",
                        column: x => x.id_candidature,
                        principalSchema: "public",
                        principalTable: "candidature",
                        principalColumn: "id_candidature",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_candidature_id_offre",
                schema: "public",
                table: "candidature",
                column: "id_offre");

            migrationBuilder.CreateIndex(
                name: "ix_cv_history_user_created",
                schema: "public",
                table: "cv_history",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_cv_template_slug",
                schema: "public",
                table: "cv_template",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_genere_id_candidature",
                schema: "public",
                table: "document_genere",
                column: "id_candidature",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_draft_id_candidature",
                schema: "public",
                table: "email_draft",
                column: "id_candidature");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "certification",
                schema: "public");

            migrationBuilder.DropTable(
                name: "cv_history",
                schema: "public");

            migrationBuilder.DropTable(
                name: "cv_template",
                schema: "public");

            migrationBuilder.DropTable(
                name: "document_genere",
                schema: "public");

            migrationBuilder.DropTable(
                name: "email_draft",
                schema: "public");

            migrationBuilder.DropTable(
                name: "skill_keyword",
                schema: "public");

            migrationBuilder.DropTable(
                name: "candidature",
                schema: "public");

            migrationBuilder.DropTable(
                name: "offres_emploi",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "pays",
                schema: "public",
                table: "utilisateur");

            migrationBuilder.DropColumn(
                name: "photo_url",
                schema: "public",
                table: "utilisateur");

            migrationBuilder.DropColumn(
                name: "telephone",
                schema: "public",
                table: "utilisateur");

            migrationBuilder.DropColumn(
                name: "titre_poste",
                schema: "public",
                table: "utilisateur");

            migrationBuilder.DropColumn(
                name: "titres_sections",
                schema: "public",
                table: "utilisateur");

            migrationBuilder.DropColumn(
                name: "ville",
                schema: "public",
                table: "utilisateur");

            migrationBuilder.DropColumn(
                name: "demo_url",
                schema: "public",
                table: "projet");

            migrationBuilder.DropColumn(
                name: "image_url",
                schema: "public",
                table: "projet");

            migrationBuilder.DropColumn(
                name: "is_university",
                schema: "public",
                table: "projet");

            migrationBuilder.DropColumn(
                name: "annee_fin",
                schema: "public",
                table: "formation");

            migrationBuilder.DropColumn(
                name: "mention",
                schema: "public",
                table: "formation");

            migrationBuilder.DropColumn(
                name: "specialisation",
                schema: "public",
                table: "formation");

            migrationBuilder.DropColumn(
                name: "ville",
                schema: "public",
                table: "formation");

            migrationBuilder.DropColumn(
                name: "type_contrat",
                schema: "public",
                table: "experience");

            migrationBuilder.DropColumn(
                name: "ville",
                schema: "public",
                table: "experience");

            migrationBuilder.RenameTable(
                name: "utilisateur",
                schema: "public",
                newName: "utilisateur");

            migrationBuilder.RenameTable(
                name: "projet",
                schema: "public",
                newName: "projet");

            migrationBuilder.RenameTable(
                name: "formation",
                schema: "public",
                newName: "formation");

            migrationBuilder.RenameTable(
                name: "experience",
                schema: "public",
                newName: "experience");

            migrationBuilder.RenameTable(
                name: "competence",
                schema: "public",
                newName: "competence");

            migrationBuilder.AlterColumn<DateTime>(
                name: "date_inscription",
                table: "utilisateur",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "date_realisation",
                table: "projet",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "date_fin",
                table: "experience",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "date_debut",
                table: "experience",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true);
        }
    }
}
