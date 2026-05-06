using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FundRaisingAssignment.Application.Migrations
{
    /// <inheritdoc />
    public partial class AddFlaggedCampaignSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add FlagReason column to Campaigns
            migrationBuilder.AddColumn<string>(
                name: "FlagReason",
                table: "Campaigns",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            // Create FundRaiserNotifications table
            migrationBuilder.CreateTable(
                name: "FundRaiserNotifications",
                columns: table => new
                {
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewOutcome = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundRaiserNotifications", x => x.NotificationId);
                    table.ForeignKey(
                        name: "FK_FundRaiserNotifications_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FundRaiserNotifications_CampaignId",
                table: "FundRaiserNotifications",
                column: "CampaignId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "FundRaiserNotifications");
            migrationBuilder.DropColumn(name: "FlagReason", table: "Campaigns");
        }
    }
}
