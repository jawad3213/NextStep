using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextStep.Migrations
{
    public partial class AddSourcedOffers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "scrape_session",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    keywords = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    providers_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    country_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    posted_window = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    contract_types_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    limit_value = table.Column<int>(type: "integer", nullable: false, defaultValue: 20),
                    result_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    warnings_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    errors_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    created_at_utc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scrape_session", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sourced_offer",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    provider_job_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    external_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    company = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    posted_at_text = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    posted_window = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    raw_contract_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    normalized_contract_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    employment_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    seniority_level = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    matched_it_terms_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    source_query_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    dedupe_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_saved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_shortlisted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    promoted_offer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    first_seen_at_utc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()"),
                    last_seen_at_utc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()"),
                    scraped_at_utc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()"),
                    created_at_utc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sourced_offer", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_scrape_session_user_created",
                table: "scrape_session",
                columns: new[] { "user_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_sourced_offer_user_dedupe",
                table: "sourced_offer",
                columns: new[] { "user_id", "dedupe_key" });

            migrationBuilder.CreateIndex(
                name: "ix_sourced_offer_user_provider_job",
                table: "sourced_offer",
                columns: new[] { "user_id", "provider", "provider_job_id" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "scrape_session");
            migrationBuilder.DropTable(name: "sourced_offer");
        }
    }
}
