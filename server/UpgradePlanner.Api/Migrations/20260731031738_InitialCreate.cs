using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UpgradePlanner.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Upgrades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    EstimatedCost = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    ProductLink = table.Column<string>(type: "TEXT", nullable: true),
                    PrerequisiteUpgradeId = table.Column<int>(type: "INTEGER", nullable: true),
                    PurchasedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ActualCost = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Upgrades", x => x.Id);
                    table.CheckConstraint("CK_Upgrade_Priority", "Priority BETWEEN 1 AND 100");
                    table.ForeignKey(
                        name: "FK_Upgrades_Upgrades_PrerequisiteUpgradeId",
                        column: x => x.PrerequisiteUpgradeId,
                        principalTable: "Upgrades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Upgrades_PrerequisiteUpgradeId",
                table: "Upgrades",
                column: "PrerequisiteUpgradeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Upgrades");
        }
    }
}
