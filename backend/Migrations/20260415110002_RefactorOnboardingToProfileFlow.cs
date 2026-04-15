using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class RefactorOnboardingToProfileFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ensure other tables exist
            migrationBuilder.Sql("CREATE TABLE IF NOT EXISTS competence (id_competence uuid PRIMARY KEY, id_utilisateur uuid, nom text, niveau integer, type_competence text, is_valid boolean);");
            migrationBuilder.Sql("CREATE TABLE IF NOT EXISTS experience (id_experience uuid PRIMARY KEY, id_utilisateur uuid, entreprise text, poste text, date_debut timestamptz, date_fin timestamptz, missions text, is_valid boolean);");
            migrationBuilder.Sql("CREATE TABLE IF NOT EXISTS formation (id_formation uuid PRIMARY KEY, id_utilisateur uuid, etablissement text, diplome text, annee integer);");
            migrationBuilder.Sql("CREATE TABLE IF NOT EXISTS projet (id_projet uuid PRIMARY KEY, id_utilisateur uuid, titre_projet text, description text, technologies_utilisees text, lien_projet text, date_realisation timestamptz, is_valid boolean);");

            // Patch utilisateur table
            migrationBuilder.Sql("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS objectif TEXT;");
            migrationBuilder.Sql("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS niveau TEXT;");
            migrationBuilder.Sql("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS secteur TEXT;");
            migrationBuilder.Sql("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS onboarding_completed BOOLEAN DEFAULT FALSE;");
            migrationBuilder.Sql("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS onboarding_step INTEGER DEFAULT 0;");
            migrationBuilder.Sql("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS onboarding_data JSONB;");
            migrationBuilder.Sql("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS profile_score INTEGER DEFAULT 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "competence");

            migrationBuilder.DropTable(
                name: "experience");

            migrationBuilder.DropTable(
                name: "formation");

            migrationBuilder.DropTable(
                name: "projet");

            migrationBuilder.DropTable(
                name: "utilisateur");
        }
    }
}
