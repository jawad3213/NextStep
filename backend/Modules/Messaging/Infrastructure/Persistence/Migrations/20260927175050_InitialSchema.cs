using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextStep.Modules.Messaging.Infrastructure.Persistence.Migrations {
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "messaging");

            migrationBuilder.CreateTable(
                name: "email_draft",
                schema: "messaging",
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
                    date_approbation = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    provider_message_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    provider_thread_id = table.Column<string>(type: "text", nullable: true),
                    nb_tentatives_envoi = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_draft", x => x.id_email_draft);
                });

            migrationBuilder.CreateTable(
                name: "oauth_state",
                schema: "messaging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    id_utilisateur = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    state_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expire_utc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    utilise = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    date_creation = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()"),
                    date_utilisation = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oauth_state", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_email_connection",
                schema: "messaging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    id_utilisateur = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    adresse_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    access_token_chiffre = table.Column<string>(type: "text", nullable: false),
                    refresh_token_chiffre = table.Column<string>(type: "text", nullable: false),
                    access_token_expire_utc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    date_creation = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()"),
                    date_modification = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_email_connection", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_oauth_credential",
                schema: "messaging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    id_utilisateur = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    client_id_chiffre = table.Column<string>(type: "text", nullable: false),
                    client_secret_chiffre = table.Column<string>(type: "text", nullable: false),
                    redirect_uri_override = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    date_creation = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()"),
                    date_modification = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_oauth_credential", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_email_draft_candidature",
                schema: "messaging",
                table: "email_draft",
                column: "id_candidature");

            migrationBuilder.CreateIndex(
                name: "IX_oauth_state_state_token_hash",
                schema: "messaging",
                table: "oauth_state",
                column: "state_token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_email_connection_id_utilisateur_provider",
                schema: "messaging",
                table: "user_email_connection",
                columns: new[] { "id_utilisateur", "provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_oauth_credential_id_utilisateur_provider",
                schema: "messaging",
                table: "user_oauth_credential",
                columns: new[] { "id_utilisateur", "provider" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_draft",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "oauth_state",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "user_email_connection",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "user_oauth_credential",
                schema: "messaging");
        }
    }
}
