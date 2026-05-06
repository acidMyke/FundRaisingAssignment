using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FundRaisingAssignment.Application.Migrations
{
    /// <inheritdoc />
    public partial class improveInLogicForFundingGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "Campaigns",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "Campaigns");
        }
    }
}
