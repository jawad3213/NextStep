using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextStep.Modules.Messaging.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailConnectionReconnectReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "reconnect_reason",
                schema: "messaging",
                table: "user_email_connection",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reconnect_required_at_utc",
                schema: "messaging",
                table: "user_email_connection",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reconnect_reason",
                schema: "messaging",
                table: "user_email_connection");

            migrationBuilder.DropColumn(
                name: "reconnect_required_at_utc",
                schema: "messaging",
                table: "user_email_connection");
        }
    }
}
