using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextStep.Migrations
{
    public partial class AddCvHtmlRenderingState : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "design_config_json",
                table: "cv_history",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "html_snapshot",
                table: "cv_history",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "design_config_json",
                table: "cv_history");

            migrationBuilder.DropColumn(
                name: "html_snapshot",
                table: "cv_history");
        }
    }
}
