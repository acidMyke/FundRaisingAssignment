using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FundRaisingAssignment.Application.Migrations
{
    /// <inheritdoc />
    public partial class AddDigestBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DigestBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    UserCount = table.Column<int>(type: "integer", nullable: true),
                    CampaignCount = table.Column<int>(type: "integer", nullable: true),
                    TriggeredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StatusUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DigestBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DigestEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DigestBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmailId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmailStatus = table.Column<string>(type: "text", nullable: false),
                    EmailReason = table.Column<string>(type: "text", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Sequence = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DigestEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DigestEntries_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DigestEntries_DigestBatches_DigestBatchId",
                        column: x => x.DigestBatchId,
                        principalTable: "DigestBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DigestEntries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DigestEntries_CampaignId",
                table: "DigestEntries",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_DigestEntries_DigestBatchId",
                table: "DigestEntries",
                column: "DigestBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_DigestEntries_UserId",
                table: "DigestEntries",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DigestEntries");

            migrationBuilder.DropTable(
                name: "DigestBatches");
        }
    }
}
