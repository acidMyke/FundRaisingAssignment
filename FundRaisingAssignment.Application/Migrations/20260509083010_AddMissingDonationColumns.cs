using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FundRaisingAssignment.Application.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingDonationColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: a prior partial run / manual schema change may have already
            // added these columns before this migration was recorded in the history.
            migrationBuilder.Sql(@"ALTER TABLE ""Donations"" ADD COLUMN IF NOT EXISTS ""Notes"" text;");
            migrationBuilder.Sql(@"ALTER TABLE ""Donations"" ADD COLUMN IF NOT EXISTS ""PaymentMethod"" character varying(50) NOT NULL DEFAULT '';");
            migrationBuilder.Sql(@"ALTER TABLE ""Donations"" ADD COLUMN IF NOT EXISTS ""ReceiptNumber"" character varying(50);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Donations"" DROP COLUMN IF EXISTS ""Notes"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Donations"" DROP COLUMN IF EXISTS ""PaymentMethod"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Donations"" DROP COLUMN IF EXISTS ""ReceiptNumber"";");
        }
    }
}
