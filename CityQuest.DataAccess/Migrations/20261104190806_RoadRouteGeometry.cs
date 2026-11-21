using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityQuest.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class RoadRouteGeometry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Attribution",
                table: "Routes",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeometryJson",
                table: "Routes",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "RoadWarnings",
                table: "Routes",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Attribution",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "GeometryJson",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "RoadWarnings",
                table: "Routes");
        }
    }
}
