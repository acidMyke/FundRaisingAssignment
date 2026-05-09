using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FundRaisingAssignment.Application.Migrations
{
    /// <inheritdoc />
    public partial class SyncJoshFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Donations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "DonorEmail",
                table: "Donations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Location",
                table: "Campaigns",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<int>(
                name: "Category",
                table: "Campaigns",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<double>(
                name: "AverageRating",
                table: "Campaigns",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

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

            migrationBuilder.AddColumn<decimal>(
                name: "FundingGoal",
                table: "Campaigns",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "Campaigns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewCount",
                table: "Campaigns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CampaignReviews",
                columns: table => new
                {
                    ReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewerEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Stars = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                name: "IX_CampaignReviews_CampaignId_ReviewerId",
                table: "CampaignReviews",
                columns: new[] { "CampaignId", "ReviewerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignReviews_ReviewerId",
                table: "CampaignReviews",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_FundRaiserNotifications_CampaignId",
                table: "FundRaiserNotifications",
                column: "CampaignId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignReviews");

            migrationBuilder.DropTable(
                name: "FundRaiserNotifications");

            migrationBuilder.DropColumn(
                name: "DonorEmail",
                table: "Donations");

            migrationBuilder.DropColumn(
                name: "AverageRating",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "CoverImageUrl",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "FlagReason",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "FundingGoal",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "ReviewCount",
                table: "Campaigns");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Donations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Location",
                table: "Campaigns",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "Campaigns",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
