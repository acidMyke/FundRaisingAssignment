using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FundRaisingAssignment.Application.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelWithDb : Migration
    {
        // Schema changes (DonationRecords, Donees, Campaigns.Category/Location) are already
        // covered by AddDonationRecords + UpdateCampaignAndDonee. This migration exists only
        // to refresh ApplicationDbContextModelSnapshot, which had drifted from the model
        // after the NicholasBranch merge.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) { }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
