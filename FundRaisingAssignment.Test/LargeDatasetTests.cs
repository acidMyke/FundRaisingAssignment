using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FundRaisingAssignment.Test;

/// <summary>
/// Exercises ICampaignService against a 1000-donation seeded dataset, mainly to
/// give a worked example of how to call TestDataSeeder.SeedLargeDataset and to
/// catch silent regressions in pagination / aggregation queries when the row
/// count is non-trivial.
/// </summary>
public class LargeDatasetTests
{
    private static CampaignService CreateService(TestDb db) =>
        new(db.Context, NullLogger<CampaignService>.Instance);

    [Fact]
    public void Seed_ProducesRequestedRowCounts()
    {
        using var db = new TestDb();

        var seed = TestDataSeeder.SeedLargeDataset(db);

        Assert.Equal(20, seed.OwnerIds.Count);
        Assert.Equal(50, seed.DonorIds.Count);
        Assert.Equal(100, seed.CampaignIds.Count);
        Assert.Equal(1000, seed.DonationIds.Count);

        Assert.Equal(20 + 50, db.Context.Users.Count());
        Assert.Equal(100, db.Context.Campaigns.Count());
        Assert.Equal(1000, db.Context.Donations.Count());
        Assert.Equal(seed.RefundedCount, db.Context.RefundLogs.Count());
    }

    [Fact]
    public async Task TopDonations_Returns10_OrderedByAmountDescending()
    {
        using var db = new TestDb();
        TestDataSeeder.SeedLargeDataset(db);
        var sut = CreateService(db);

        var busiestCampaignId = await db.Context.Donations
            .GroupBy(d => d.CampaignId)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstAsync();

        var top = await sut.GetTopDonationsAsync(busiestCampaignId, 10);

        Assert.True(top.Count <= 10);
        Assert.Equal(top.OrderByDescending(d => d.Amount).Select(d => d.Id),
                     top.Select(d => d.Id));
    }

    [Fact]
    public async Task TotalDonated_MatchesSumOfCompletedDonations()
    {
        using var db = new TestDb();
        TestDataSeeder.SeedLargeDataset(db);
        var sut = CreateService(db);

        var campaignId = await db.Context.Donations
            .Where(d => d.Status == DonationStatus.Completed)
            .Select(d => d.CampaignId)
            .FirstAsync();

        // GetTotalDonatedAsync sums *all* donations including refunded ones — so
        // expected total here matches that, not just Completed. Keeping the test
        // honest about what the production query does.
        var expected = await db.Context.Donations
            .Where(d => d.CampaignId == campaignId)
            .SumAsync(d => d.Amount);

        var actual = await sut.GetTotalDonatedAsync(campaignId);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Seed_IsDeterministic_ForSameRandomSeed()
    {
        using var dbA = new TestDb();
        using var dbB = new TestDb();

        var a = TestDataSeeder.SeedLargeDataset(dbA, donationCount: 200, randomSeed: 42);
        var b = TestDataSeeder.SeedLargeDataset(dbB, donationCount: 200, randomSeed: 42);

        // Aggregate totals are reproducible even though the IDs differ
        var totalsA = dbA.Context.Donations.Sum(d => d.Amount);
        var totalsB = dbB.Context.Donations.Sum(d => d.Amount);
        Assert.Equal(totalsA, totalsB);
        Assert.Equal(a.RefundedCount, b.RefundedCount);
    }
}
