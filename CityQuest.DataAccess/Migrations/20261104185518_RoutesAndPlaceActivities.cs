using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityQuest.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class RoutesAndPlaceActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Places_Category",
                table: "Places");

            migrationBuilder.AddColumn<string>(
                name: "Challenge",
                table: "Places",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Fact",
                table: "Places",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Hint",
                table: "Places",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Routes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartLatitude = table.Column<double>(type: "double precision", nullable: false),
                    StartLongitude = table.Column<double>(type: "double precision", nullable: false),
                    ReturnToStart = table.Column<bool>(type: "boolean", nullable: false),
                    TimeBudgetMinutes = table.Column<int>(type: "integer", nullable: false),
                    TotalMinutes = table.Column<int>(type: "integer", nullable: false),
                    WalkingMinutes = table.Column<int>(type: "integer", nullable: false),
                    DistanceMeters = table.Column<int>(type: "integer", nullable: false),
                    ReturnWalkingMinutes = table.Column<int>(type: "integer", nullable: false),
                    ReturnDistanceMeters = table.Column<int>(type: "integer", nullable: false),
                    Interests = table.Column<string[]>(type: "text[]", nullable: false),
                    IncludeFood = table.Column<bool>(type: "boolean", nullable: false),
                    FoodType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CalculationMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Routes", x => x.Id);
                    table.CheckConstraint("CK_Routes_TimeBudget", "\"TotalMinutes\" > 0 AND \"TotalMinutes\" <= \"TimeBudgetMinutes\"");
                    table.ForeignKey(
                        name: "FK_Routes_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RouteLikes",
                columns: table => new
                {
                    RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteLikes", x => new { x.RouteId, x.UserId });
                    table.ForeignKey(
                        name: "FK_RouteLikes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RouteLikes_Routes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "Routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RouteStops",
                columns: table => new
                {
                    RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    PlaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    VisitMinutes = table.Column<int>(type: "integer", nullable: false),
                    WalkingMinutes = table.Column<int>(type: "integer", nullable: false),
                    DistanceMeters = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteStops", x => new { x.RouteId, x.Position });
                    table.ForeignKey(
                        name: "FK_RouteStops_Places_PlaceId",
                        column: x => x.PlaceId,
                        principalTable: "Places",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RouteStops_Routes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "Routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavedRoutes",
                columns: table => new
                {
                    RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedRoutes", x => new { x.RouteId, x.UserId });
                    table.ForeignKey(
                        name: "FK_SavedRoutes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SavedRoutes_Routes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "Routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Places_Category",
                table: "Places",
                sql: "\"Category\" IN ('architecture', 'nature', 'monument', 'museum', 'cafe', 'restaurant')");

            migrationBuilder.CreateIndex(
                name: "IX_RouteLikes_UserId",
                table: "RouteLikes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Routes_IsPublic_CreatedAt",
                table: "Routes",
                columns: new[] { "IsPublic", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Routes_OwnerId",
                table: "Routes",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteStops_PlaceId",
                table: "RouteStops",
                column: "PlaceId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteStops_RouteId_PlaceId",
                table: "RouteStops",
                columns: new[] { "RouteId", "PlaceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedRoutes_UserId",
                table: "SavedRoutes",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RouteLikes");

            migrationBuilder.DropTable(
                name: "RouteStops");

            migrationBuilder.DropTable(
                name: "SavedRoutes");

            migrationBuilder.DropTable(
                name: "Routes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Places_Category",
                table: "Places");

            migrationBuilder.DropColumn(
                name: "Challenge",
                table: "Places");

            migrationBuilder.DropColumn(
                name: "Fact",
                table: "Places");

            migrationBuilder.DropColumn(
                name: "Hint",
                table: "Places");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Places_Category",
                table: "Places",
                sql: "\"Category\" IN ('architecture', 'nature')");
        }
    }
}
