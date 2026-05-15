using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FundRaisingAssignment.Application.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignDigestUserControlAndVisitsTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsEmailBounced",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCampaignUpdateSent",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReceiveCampaignDigest",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UnsubscribeCooldownUntil",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastDigestSent",
                table: "Campaigns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CampaignVisits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstVisitDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastVisitDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VisitCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignVisits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignVisits_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignVisits_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignVisits_CampaignId",
                table: "CampaignVisits",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignVisits_UserId",
                table: "CampaignVisits",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignVisits");

            migrationBuilder.DropColumn(
                name: "IsEmailBounced",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastCampaignUpdateSent",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ReceiveCampaignDigest",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UnsubscribeCooldownUntil",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastDigestSent",
                table: "Campaigns");
        }
    }
}
