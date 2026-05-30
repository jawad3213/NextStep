using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextStep.Migrations
{
    /// <inheritdoc />
    public partial class AddFollowUpFieldsToCandidature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "follow_up_needed",
                schema: "public",
                table: "candidature",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_follow_up_at_utc",
                schema: "public",
                table: "candidature",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "follow_up_needed",
                schema: "public",
                table: "candidature");

            migrationBuilder.DropColumn(
                name: "last_follow_up_at_utc",
                schema: "public",
                table: "candidature");
        }
    }
}
