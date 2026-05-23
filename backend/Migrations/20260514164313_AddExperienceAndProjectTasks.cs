using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextStep.Migrations
{
    /// <inheritdoc />
    public partial class AddExperienceAndProjectTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "taches",
                schema: "public",
                table: "projet",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "taches",
                schema: "public",
                table: "experience",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "taches",
                schema: "public",
                table: "projet");

            migrationBuilder.DropColumn(
                name: "taches",
                schema: "public",
                table: "experience");
        }
    }
}
