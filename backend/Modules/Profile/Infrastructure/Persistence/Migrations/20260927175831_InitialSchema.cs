using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextStep.Modules.Profile.Infrastructure.Persistence.Migrations {
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "profile");

            migrationBuilder.CreateTable(
                name: "skill_keyword",
                schema: "profile",
                columns: table => new
                {
                    id_skill_keyword = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    mot = table.Column<string>(type: "text", nullable: false),
                    categorie = table.Column<string>(type: "text", nullable: false, defaultValue: "Technique")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skill_keyword", x => x.id_skill_keyword);
                });

            migrationBuilder.CreateTable(
                name: "utilisateur",
                schema: "profile",
                columns: table => new
                {
                    id_utilisateur = table.Column<Guid>(type: "uuid", nullable: false),
                    keycloak_id = table.Column<string>(type: "text", nullable: false),
                    nom = table.Column<string>(type: "text", nullable: true),
                    prenom = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: false),
                    lien_linkedin = table.Column<string>(type: "text", nullable: true),
                    lien_github = table.Column<string>(type: "text", nullable: true),
                    lien_portfolio = table.Column<string>(type: "text", nullable: true),
                    titre_poste = table.Column<string>(type: "text", nullable: true),
                    photo_url = table.Column<string>(type: "text", nullable: true),
                    ville = table.Column<string>(type: "text", nullable: true),
                    pays = table.Column<string>(type: "text", nullable: true),
                    telephone = table.Column<string>(type: "text", nullable: true),
                    resume_professionnel = table.Column<string>(type: "text", nullable: true),
                    coordonnees = table.Column<string>(type: "text", nullable: true),
                    titres_sections = table.Column<string>(type: "jsonb", nullable: true),
                    objectif = table.Column<string>(type: "text", nullable: true),
                    niveau = table.Column<string>(type: "text", nullable: true),
                    secteur = table.Column<string>(type: "text", nullable: true),
                    onboarding_completed = table.Column<bool>(type: "boolean", nullable: false),
                    profile_completed = table.Column<bool>(type: "boolean", nullable: false),
                    onboarding_step = table.Column<int>(type: "integer", nullable: false),
                    onboarding_data = table.Column<string>(type: "jsonb", nullable: true),
                    profile_score = table.Column<int>(type: "integer", nullable: false),
                    date_inscription = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_utilisateur", x => x.id_utilisateur);
                });

            migrationBuilder.CreateTable(
                name: "certification",
                schema: "profile",
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
                    table.ForeignKey(
                        name: "FK_certification_utilisateur_id_utilisateur",
                        column: x => x.id_utilisateur,
                        principalSchema: "profile",
                        principalTable: "utilisateur",
                        principalColumn: "id_utilisateur",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "competence",
                schema: "profile",
                columns: table => new
                {
                    id_competence = table.Column<Guid>(type: "uuid", nullable: false),
                    id_utilisateur = table.Column<Guid>(type: "uuid", nullable: false),
                    nom = table.Column<string>(type: "text", nullable: true),
                    niveau = table.Column<int>(type: "integer", nullable: false),
                    type_competence = table.Column<string>(type: "text", nullable: true),
                    is_valid = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_competence", x => x.id_competence);
                    table.ForeignKey(
                        name: "FK_competence_utilisateur_id_utilisateur",
                        column: x => x.id_utilisateur,
                        principalSchema: "profile",
                        principalTable: "utilisateur",
                        principalColumn: "id_utilisateur",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "experience",
                schema: "profile",
                columns: table => new
                {
                    id_experience = table.Column<Guid>(type: "uuid", nullable: false),
                    id_utilisateur = table.Column<Guid>(type: "uuid", nullable: false),
                    entreprise = table.Column<string>(type: "text", nullable: true),
                    poste = table.Column<string>(type: "text", nullable: true),
                    date_debut = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    date_fin = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    missions = table.Column<string>(type: "text", nullable: true),
                    ville = table.Column<string>(type: "text", nullable: true),
                    type_contrat = table.Column<string>(type: "text", nullable: true),
                    taches = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    is_valid = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_experience", x => x.id_experience);
                    table.ForeignKey(
                        name: "FK_experience_utilisateur_id_utilisateur",
                        column: x => x.id_utilisateur,
                        principalSchema: "profile",
                        principalTable: "utilisateur",
                        principalColumn: "id_utilisateur",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "formation",
                schema: "profile",
                columns: table => new
                {
                    id_formation = table.Column<Guid>(type: "uuid", nullable: false),
                    id_utilisateur = table.Column<Guid>(type: "uuid", nullable: false),
                    etablissement = table.Column<string>(type: "text", nullable: true),
                    diplome = table.Column<string>(type: "text", nullable: true),
                    annee = table.Column<int>(type: "integer", nullable: false),
                    ville = table.Column<string>(type: "text", nullable: true),
                    specialisation = table.Column<string>(type: "text", nullable: true),
                    mention = table.Column<string>(type: "text", nullable: true),
                    annee_fin = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_formation", x => x.id_formation);
                    table.ForeignKey(
                        name: "FK_formation_utilisateur_id_utilisateur",
                        column: x => x.id_utilisateur,
                        principalSchema: "profile",
                        principalTable: "utilisateur",
                        principalColumn: "id_utilisateur",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "projet",
                schema: "profile",
                columns: table => new
                {
                    id_projet = table.Column<Guid>(type: "uuid", nullable: false),
                    id_utilisateur = table.Column<Guid>(type: "uuid", nullable: false),
                    titre_projet = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    technologies_utilisees = table.Column<string>(type: "text", nullable: true),
                    lien_projet = table.Column<string>(type: "text", nullable: true),
                    date_realisation = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    demo_url = table.Column<string>(type: "text", nullable: true),
                    image_url = table.Column<string>(type: "text", nullable: true),
                    is_university = table.Column<bool>(type: "boolean", nullable: false),
                    taches = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    is_valid = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projet", x => x.id_projet);
                    table.ForeignKey(
                        name: "FK_projet_utilisateur_id_utilisateur",
                        column: x => x.id_utilisateur,
                        principalSchema: "profile",
                        principalTable: "utilisateur",
                        principalColumn: "id_utilisateur",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_certification_id_utilisateur",
                schema: "profile",
                table: "certification",
                column: "id_utilisateur");

            migrationBuilder.CreateIndex(
                name: "IX_competence_id_utilisateur",
                schema: "profile",
                table: "competence",
                column: "id_utilisateur");

            migrationBuilder.CreateIndex(
                name: "IX_experience_id_utilisateur",
                schema: "profile",
                table: "experience",
                column: "id_utilisateur");

            migrationBuilder.CreateIndex(
                name: "IX_formation_id_utilisateur",
                schema: "profile",
                table: "formation",
                column: "id_utilisateur");

            migrationBuilder.CreateIndex(
                name: "IX_projet_id_utilisateur",
                schema: "profile",
                table: "projet",
                column: "id_utilisateur");

            migrationBuilder.CreateIndex(
                name: "idx_utilisateur_email",
                schema: "profile",
                table: "utilisateur",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "idx_utilisateur_keycloak_id",
                schema: "profile",
                table: "utilisateur",
                column: "keycloak_id");

            // Case-insensitive uniqueness of suggestions (expression index: not expressible in the EF model)
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX IF NOT EXISTS ux_skill_keyword_mot_categorie ON profile.skill_keyword (lower(mot), categorie);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "certification",
                schema: "profile");

            migrationBuilder.DropTable(
                name: "competence",
                schema: "profile");

            migrationBuilder.DropTable(
                name: "experience",
                schema: "profile");

            migrationBuilder.DropTable(
                name: "formation",
                schema: "profile");

            migrationBuilder.DropTable(
                name: "projet",
                schema: "profile");

            migrationBuilder.DropTable(
                name: "skill_keyword",
                schema: "profile");

            migrationBuilder.DropTable(
                name: "utilisateur",
                schema: "profile");
        }
    }
}
