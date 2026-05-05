using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FundRaisingAssignment.Application.Migrations
{
    /// <inheritdoc />
    public partial class AddDonationGoalSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BudgetStatus",
                table: "DonationGoals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastEvaluatedAt",
                table: "DonationGoals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetStatus",
                table: "DonationGoals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalDonated",
                table: "DonationGoals",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BudgetStatus",
                table: "DonationGoals");

            migrationBuilder.DropColumn(
                name: "LastEvaluatedAt",
                table: "DonationGoals");

            migrationBuilder.DropColumn(
                name: "TargetStatus",
                table: "DonationGoals");

            migrationBuilder.DropColumn(
                name: "TotalDonated",
                table: "DonationGoals");
        }
    }
}
