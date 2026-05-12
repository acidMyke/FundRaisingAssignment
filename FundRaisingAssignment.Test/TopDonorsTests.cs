using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Services;
using Microsoft.Extensions.Logging.Abstractions;

// ─────────────────────────────────────────────────────────────────────────────
// Test plan: 10.4 Top donors leaderboard
// User Story: PM06 – View Top Donors Leaderboard
// Backs: ICampaignService.GetTopDonationsAsync
// ─────────────────────────────────────────────────────────────────────────────

namespace FundRaisingAssignment.Test;

public class TopDonorsTests
{
    private static CampaignService CreateService(TestDb db) =>
        new(db.Context, NullLogger<CampaignService>.Instance);

    [Fact]
    public async Task GetTopDonations_OrdersByAmountDescending()
    {
        using var db = new TestDb();
        var campaign = TestSeedHelpers.SeedCampaign(db, target: 10_000m);
        TestSeedHelpers.SeedDonation(db, campaign.Id, amount: 50m, donorEmail: "small@x.test");
        TestSeedHelpers.SeedDonation(db, campaign.Id, amount: 500m, donorEmail: "big@x.test");
        TestSeedHelpers.SeedDonation(db, campaign.Id, amount: 250m, donorEmail: "mid@x.test");
        var sut = CreateService(db);

        var top = await sut.GetTopDonationsAsync(campaign.Id, 10);

        Assert.Equal(3, top.Count);
        Assert.Equal(500m, top[0].Amount);
        Assert.Equal(250m, top[1].Amount);
        Assert.Equal(50m, top[2].Amount);
    }

    [Fact]
    public async Task GetTopDonations_HonorsTakeCount()
    {
        using var db = new TestDb();
        var campaign = TestSeedHelpers.SeedCampaign(db, target: 10_000m);
        for (int i = 1; i <= 12; i++)
            TestSeedHelpers.SeedDonation(db, campaign.Id, amount: i * 10m, donorEmail: $"d{i}@x.test");
        var sut = CreateService(db);

        var top = await sut.GetTopDonationsAsync(campaign.Id, 5);

        Assert.Equal(5, top.Count);
        Assert.Equal(120m, top[0].Amount);
        Assert.Equal(80m, top[4].Amount);
    }

    [Fact]
    public async Task GetTopDonations_TieBrokenByCreatedAtAscending()
    {
        using var db = new TestDb();
        var campaign = TestSeedHelpers.SeedCampaign(db, target: 10_000m);

        var older = TestSeedHelpers.SeedDonation(db, campaign.Id, amount: 100m,
            donorEmail: "older@x.test", createdAt: DateTime.UtcNow.AddDays(-2));
        var newer = TestSeedHelpers.SeedDonation(db, campaign.Id, amount: 100m,
            donorEmail: "newer@x.test", createdAt: DateTime.UtcNow);
        var sut = CreateService(db);

        var top = await sut.GetTopDonationsAsync(campaign.Id, 10);

        Assert.Equal(2, top.Count);
        Assert.Equal(older.Id, top[0].Id);
        Assert.Equal(newer.Id, top[1].Id);
    }

    [Fact]
    public async Task GetTopDonations_OnlyReturnsCampaignSpecificDonations()
    {
        using var db = new TestDb();
        var c1 = TestSeedHelpers.SeedCampaign(db, target: 10_000m, title: "C1");
        var c2 = TestSeedHelpers.SeedCampaign(db, target: 10_000m, title: "C2");
        TestSeedHelpers.SeedDonation(db, c1.Id, amount: 200m, donorEmail: "alpha@x.test");
        TestSeedHelpers.SeedDonation(db, c2.Id, amount: 300m, donorEmail: "beta@x.test");
        var sut = CreateService(db);

        var top = await sut.GetTopDonationsAsync(c1.Id, 10);

        Assert.Single(top);
        Assert.Equal(200m, top[0].Amount);
    }

    [Fact]
    public async Task GetTopDonations_EmptyCampaign_ReturnsEmpty()
    {
        using var db = new TestDb();
        var campaign = TestSeedHelpers.SeedCampaign(db);
        var sut = CreateService(db);

        var top = await sut.GetTopDonationsAsync(campaign.Id, 10);

        Assert.Empty(top);
    }
}
