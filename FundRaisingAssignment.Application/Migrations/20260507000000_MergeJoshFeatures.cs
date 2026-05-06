using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FundRaisingAssignment.Application.Migrations
{
    /// <summary>
    /// Adds Josh's campaign lifecycle, reviews, notifications and donation extras
    /// onto Karthik's existing schema without touching existing tables.
    /// </summary>
    public partial class MergeJoshFeatures : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Campaign: new columns ─────────────────────────────────────────

            // Change Category from varchar(50) to integer (CampaignCategory enum)
            // First set all existing rows to 6 (Other) then alter type
            migrationBuilder.Sql(
                "ALTER TABLE \"Campaigns\" ALTER COLUMN \"Category\" TYPE integer USING 6;");

            migrationBuilder.AddColumn<string>(
                name: "CoverImageUrl",
                table: "Campaigns",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FlagReason",
                table: "Campaigns",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageRating",
                table: "Campaigns",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "ReviewCount",
                table: "Campaigns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "FundingGoal",
                table: "Campaigns",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "Campaigns",
                type: "timestamp with time zone",
                nullable: true);

            // Sync FundingGoal from TargetAmount for existing rows
            migrationBuilder.Sql(
                "UPDATE \"Campaigns\" SET \"FundingGoal\" = \"TargetAmount\" WHERE \"FundingGoal\" = 0;");

            // ── Donations: new columns ────────────────────────────────────────
            migrationBuilder.AddColumn<string>(
                name: "DonorEmail",
                table: "Donations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "Anonymous");

            migrationBuilder.AddColumn<bool>(
                name: "IsAnonymous",
                table: "Donations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Make UserId nullable (to support guest donations)
            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Donations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            // ── CampaignReviews table ─────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "CampaignReviews",
                columns: table => new
                {
                    ReviewId      = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId    = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewerId    = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewerEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Stars         = table.Column<int>(type: "integer", nullable: false),
                    Comment       = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt     = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignReviews", x => x.ReviewId);
                    table.ForeignKey(
                        name: "FK_CampaignReviews_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignReviews_Users_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignReviews_CampaignId_ReviewerId",
                table: "CampaignReviews",
                columns: new[] { "CampaignId", "ReviewerId" },
                unique: true);

            // ── FundRaiserNotifications table ─────────────────────────────────
            migrationBuilder.CreateTable(
                name: "FundRaiserNotifications",
                columns: table => new
                {
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId     = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewOutcome  = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SentAt         = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRead         = table.Column<bool>(type: "boolean", nullable: false)
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

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CampaignReviews");
            migrationBuilder.DropTable(name: "FundRaiserNotifications");

            migrationBuilder.DropColumn(name: "CoverImageUrl",  table: "Campaigns");
            migrationBuilder.DropColumn(name: "FlagReason",     table: "Campaigns");
            migrationBuilder.DropColumn(name: "AverageRating",  table: "Campaigns");
            migrationBuilder.DropColumn(name: "ReviewCount",    table: "Campaigns");
            migrationBuilder.DropColumn(name: "FundingGoal",    table: "Campaigns");
            migrationBuilder.DropColumn(name: "PublishedAt",    table: "Campaigns");
            migrationBuilder.DropColumn(name: "DonorEmail",     table: "Donations");
            migrationBuilder.DropColumn(name: "IsAnonymous",    table: "Donations");

            migrationBuilder.Sql(
                "ALTER TABLE \"Campaigns\" ALTER COLUMN \"Category\" TYPE varchar(50) USING 'Other';");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Donations",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
