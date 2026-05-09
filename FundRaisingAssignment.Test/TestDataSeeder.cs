using FundRaisingAssignment.Application.Data.Seeding;

namespace FundRaisingAssignment.Test;

/// <summary>
/// Thin synchronous wrapper around DataSeeder for in-memory SQLite test fixtures.
/// The real seeding logic lives in FundRaisingAssignment.Application.Data.Seeding.DataSeeder
/// so it can be reused by the `dotnet run -- --seed` CLI flag.
/// </summary>
internal static class TestDataSeeder
{
    public static SeedResult SeedLargeDataset(
        TestDb db,
        int ownerCount = 20,
        int donorCount = 50,
        int campaignCount = 100,
        int donationCount = 1000,
        int randomSeed = 12345) =>
        DataSeeder.SeedLargeDatasetAsync(
            db.Context,
            ownerCount: ownerCount,
            donorCount: donorCount,
            campaignCount: campaignCount,
            donationCount: donationCount,
            randomSeed: randomSeed).GetAwaiter().GetResult();
}
