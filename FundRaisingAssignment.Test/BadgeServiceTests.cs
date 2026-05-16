using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Services;
using Microsoft.EntityFrameworkCore;

// ─────────────────────────────────────────────────────────────────────────────
// Test plan: 9.12 View Badges
// User Story: DN05 – View Badges
// Backs: BadgeService.GetUserBadgesAsync (tier computation, progress, next-tier
//        requirement, border colour) and BadgeService.UpdateUserMetricsAsync
//        (per-donation metric accumulation).
// ─────────────────────────────────────────────────────────────────────────────

namespace FundRaisingAssignment.Test;

public class BadgeServiceTests
{
    private static BadgeService CreateService(TestDb db) => new(db.Context);

    private const string LockedBorder = "#e5e7eb";
    private const string BronzeBorder = "#cd7f32";
    private const string SilverBorder = "#c0c0c0";
    private const string GoldBorder = "#ffd700";
    private const string SpecialBorder = "#6366f1";

    private static void SeedMetrics(TestDb db, Guid userId, int donationCount, int uniqueCampaigns, decimal highDonation)
    {
        db.Context.UserMetrics.Add(new UserMetrics
        {
            UserId = userId,
            DonationCount = donationCount,
            UniqueCampaigns = uniqueCampaigns,
            HighDonation = highDonation,
            LastUpdated = DateTime.UtcNow,
        });
        db.Context.SaveChanges();
    }

    private static Badge Of(UserBadgeProgress p, BadgeType t) => p.Badges.Single(b => b.Type == t);

    // ── GetUserBadgesAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetUserBadges_NoMetricsRow_ReturnsFiveLockedBadgesWithInitialRequirements()
    {
        using var db = new TestDb();
        var userId = TestSeedHelpers.SeedUser(db, "no-metrics");
        var sut = CreateService(db);

        var progress = await sut.GetUserBadgesAsync(userId);

        Assert.Equal(userId, progress.UserId);
        Assert.Equal(5, progress.Badges.Count);

        var donationCount = Of(progress, BadgeType.DonationCount);
        Assert.Null(donationCount.Tier);
        Assert.Equal(0, donationCount.Progress);
        Assert.Equal(1, donationCount.NextTierRequirement);
        Assert.Equal("🪙", donationCount.Icon);
        Assert.Equal(LockedBorder, donationCount.BorderColor);

        var highDonation = Of(progress, BadgeType.HighDonationCount);
        Assert.Null(highDonation.Tier);
        Assert.Equal(0, highDonation.Progress);
        Assert.Equal(1, highDonation.NextTierRequirement);
        Assert.Equal("💰", highDonation.Icon);
        Assert.Equal(LockedBorder, highDonation.BorderColor);

        var hugeSupporter = Of(progress, BadgeType.HugeSupporter);
        Assert.Null(hugeSupporter.Tier);
        Assert.Equal(0, hugeSupporter.Progress);
        Assert.Equal(100, hugeSupporter.NextTierRequirement);
        Assert.Equal("🏆", hugeSupporter.Icon);
        Assert.Equal(LockedBorder, hugeSupporter.BorderColor);

        var bigHeart = Of(progress, BadgeType.BigHeart);
        Assert.Null(bigHeart.Tier);
        Assert.Equal(0, bigHeart.Progress);
        Assert.Equal(1, bigHeart.NextTierRequirement);
        Assert.Equal("💖", bigHeart.Icon);
        Assert.Equal(LockedBorder, bigHeart.BorderColor);

        var firstDonation = Of(progress, BadgeType.FirstDonation);
        Assert.Null(firstDonation.Tier);
        Assert.Equal(0, firstDonation.Progress);
        Assert.Equal(1, firstDonation.NextTierRequirement);
        Assert.Equal("🎉", firstDonation.Icon);
        Assert.Equal(LockedBorder, firstDonation.BorderColor);
    }

    [Theory]
    [InlineData(0, null, 1, LockedBorder)]
    [InlineData(1, BadgeTier.Bronze, 5, BronzeBorder)]
    [InlineData(4, BadgeTier.Bronze, 5, BronzeBorder)]
    [InlineData(5, BadgeTier.Silver, 10, SilverBorder)]
    [InlineData(9, BadgeTier.Silver, 10, SilverBorder)]
    [InlineData(10, BadgeTier.Gold, 0, GoldBorder)]
    [InlineData(25, BadgeTier.Gold, 0, GoldBorder)]
    public async Task GetUserBadges_DonationCount_TierThresholds(int donationCount, BadgeTier? expectedTier, int expectedNext, string expectedBorder)
    {
        using var db = new TestDb();
        var userId = TestSeedHelpers.SeedUser(db, $"dc-{donationCount}");
        SeedMetrics(db, userId, donationCount, uniqueCampaigns: 0, highDonation: 0);
        var sut = CreateService(db);

        var badge = Of(await sut.GetUserBadgesAsync(userId), BadgeType.DonationCount);

        Assert.Equal(expectedTier, badge.Tier);
        Assert.Equal(donationCount, badge.Progress);
        Assert.Equal(expectedNext, badge.NextTierRequirement);
        Assert.Equal(expectedBorder, badge.BorderColor);
    }

    [Fact]
    public async Task GetUserBadges_HighDonationCount_OnlyCountsDonationsAtOrAbove100()
    {
        using var db = new TestDb();
        var userId = TestSeedHelpers.SeedUser(db, "hd-mix");
        var campaign = TestSeedHelpers.SeedCampaign(db);
        TestSeedHelpers.SeedDonation(db, campaign.Id, 50m, userId: userId);     // excluded
        TestSeedHelpers.SeedDonation(db, campaign.Id, 99.99m, userId: userId);  // excluded
        TestSeedHelpers.SeedDonation(db, campaign.Id, 100m, userId: userId);    // included
        TestSeedHelpers.SeedDonation(db, campaign.Id, 250m, userId: userId);    // included
        var sut = CreateService(db);

        var badge = Of(await sut.GetUserBadgesAsync(userId), BadgeType.HighDonationCount);

        Assert.Equal(2, badge.Progress);
        Assert.Equal(BadgeTier.Bronze, badge.Tier);
    }

    [Theory]
    [InlineData(0, null, 1, LockedBorder)]
    [InlineData(1, BadgeTier.Bronze, 5, BronzeBorder)]
    [InlineData(5, BadgeTier.Silver, 10, SilverBorder)]
    [InlineData(10, BadgeTier.Gold, 0, GoldBorder)]
    public async Task GetUserBadges_HighDonationCount_TierThresholds(int highDonationCount, BadgeTier? expectedTier, int expectedNext, string expectedBorder)
    {
        using var db = new TestDb();
        var userId = TestSeedHelpers.SeedUser(db, $"hd-{highDonationCount}");
        var campaign = TestSeedHelpers.SeedCampaign(db);
        for (var i = 0; i < highDonationCount; i++)
            TestSeedHelpers.SeedDonation(db, campaign.Id, 150m, userId: userId);
        var sut = CreateService(db);

        var badge = Of(await sut.GetUserBadgesAsync(userId), BadgeType.HighDonationCount);

        Assert.Equal(expectedTier, badge.Tier);
        Assert.Equal(highDonationCount, badge.Progress);
        Assert.Equal(expectedNext, badge.NextTierRequirement);
        Assert.Equal(expectedBorder, badge.BorderColor);
    }

    [Fact]
    public async Task GetUserBadges_HugeSupporter_SumsPerCampaign_AndPicksMax()
    {
        using var db = new TestDb();
        var userId = TestSeedHelpers.SeedUser(db, "hs-sum");
        var campaignA = TestSeedHelpers.SeedCampaign(db, title: "A");
        var campaignB = TestSeedHelpers.SeedCampaign(db, title: "B");
        // Campaign A receives 200 + 250 = 450 total from this user
        TestSeedHelpers.SeedDonation(db, campaignA.Id, 200m, userId: userId);
        TestSeedHelpers.SeedDonation(db, campaignA.Id, 250m, userId: userId);
        // Campaign B receives a single 600 — wins the max even though A had more donations
        TestSeedHelpers.SeedDonation(db, campaignB.Id, 600m, userId: userId);
        var sut = CreateService(db);

        var badge = Of(await sut.GetUserBadgesAsync(userId), BadgeType.HugeSupporter);

        Assert.Equal(600, badge.Progress);
        Assert.Equal(BadgeTier.Silver, badge.Tier);
        Assert.Equal(1000, badge.NextTierRequirement);
    }

    [Theory]
    [InlineData(0, null, 100, LockedBorder)]
    [InlineData(100, BadgeTier.Bronze, 500, BronzeBorder)]
    [InlineData(499, BadgeTier.Bronze, 500, BronzeBorder)]
    [InlineData(500, BadgeTier.Silver, 1000, SilverBorder)]
    [InlineData(999, BadgeTier.Silver, 1000, SilverBorder)]
    [InlineData(1000, BadgeTier.Gold, 0, GoldBorder)]
    [InlineData(5000, BadgeTier.Gold, 0, GoldBorder)]
    public async Task GetUserBadges_HugeSupporter_TierThresholds(decimal donationAmount, BadgeTier? expectedTier, int expectedNext, string expectedBorder)
    {
        using var db = new TestDb();
        var userId = TestSeedHelpers.SeedUser(db, $"hs-{donationAmount}");
        if (donationAmount > 0)
        {
            var campaign = TestSeedHelpers.SeedCampaign(db);
            TestSeedHelpers.SeedDonation(db, campaign.Id, donationAmount, userId: userId);
        }
        var sut = CreateService(db);

        var badge = Of(await sut.GetUserBadgesAsync(userId), BadgeType.HugeSupporter);

        Assert.Equal(expectedTier, badge.Tier);
        Assert.Equal((int)donationAmount, badge.Progress);
        Assert.Equal(expectedNext, badge.NextTierRequirement);
        Assert.Equal(expectedBorder, badge.BorderColor);
    }

    [Theory]
    [InlineData(0, null, 1, LockedBorder)]
    [InlineData(1, BadgeTier.Bronze, 5, BronzeBorder)]
    [InlineData(4, BadgeTier.Bronze, 5, BronzeBorder)]
    [InlineData(5, BadgeTier.Silver, 10, SilverBorder)]
    [InlineData(9, BadgeTier.Silver, 10, SilverBorder)]
    [InlineData(10, BadgeTier.Gold, 0, GoldBorder)]
    public async Task GetUserBadges_BigHeart_TierThresholds(int uniqueCampaigns, BadgeTier? expectedTier, int expectedNext, string expectedBorder)
    {
        using var db = new TestDb();
        var userId = TestSeedHelpers.SeedUser(db, $"bh-{uniqueCampaigns}");
        SeedMetrics(db, userId, donationCount: 0, uniqueCampaigns: uniqueCampaigns, highDonation: 0);
        var sut = CreateService(db);

        var badge = Of(await sut.GetUserBadgesAsync(userId), BadgeType.BigHeart);

        Assert.Equal(expectedTier, badge.Tier);
        Assert.Equal(uniqueCampaigns, badge.Progress);
        Assert.Equal(expectedNext, badge.NextTierRequirement);
        Assert.Equal(expectedBorder, badge.BorderColor);
    }

    [Fact]
    public async Task GetUserBadges_FirstDonation_LockedWhenZeroDonations()
    {
        using var db = new TestDb();
        var userId = TestSeedHelpers.SeedUser(db, "fd-zero");
        SeedMetrics(db, userId, donationCount: 0, uniqueCampaigns: 0, highDonation: 0);
        var sut = CreateService(db);

        var badge = Of(await sut.GetUserBadgesAsync(userId), BadgeType.FirstDonation);

        Assert.Null(badge.Tier);
        Assert.Equal(0, badge.Progress);
        Assert.Equal(1, badge.NextTierRequirement);
        Assert.Equal(LockedBorder, badge.BorderColor);
    }

    [Fact]
    public async Task GetUserBadges_FirstDonation_SpecialTierAwardedAtFirstDonation()
    {
        using var db = new TestDb();
        var userId = TestSeedHelpers.SeedUser(db, "fd-one");
        SeedMetrics(db, userId, donationCount: 1, uniqueCampaigns: 1, highDonation: 25m);
        var sut = CreateService(db);

        var badge = Of(await sut.GetUserBadgesAsync(userId), BadgeType.FirstDonation);

        Assert.Equal(BadgeTier.Special, badge.Tier);
        Assert.Equal(1, badge.Progress);
        Assert.Equal(SpecialBorder, badge.BorderColor);
    }

    [Fact]
    public async Task GetUserBadges_NextTierRequirement_IsZeroForEveryProgressBadgeAtGold()
    {
        using var db = new TestDb();
        var userId = TestSeedHelpers.SeedUser(db, "gold");
        SeedMetrics(db, userId, donationCount: 10, uniqueCampaigns: 10, highDonation: 0);
        var campaign = TestSeedHelpers.SeedCampaign(db);
        for (var i = 0; i < 10; i++)
            TestSeedHelpers.SeedDonation(db, campaign.Id, 150m, userId: userId);
        var sut = CreateService(db);

        var progress = await sut.GetUserBadgesAsync(userId);

        Assert.Equal(0, Of(progress, BadgeType.DonationCount).NextTierRequirement);
        Assert.Equal(0, Of(progress, BadgeType.HighDonationCount).NextTierRequirement);
        Assert.Equal(0, Of(progress, BadgeType.HugeSupporter).NextTierRequirement);
        Assert.Equal(0, Of(progress, BadgeType.BigHeart).NextTierRequirement);
    }

    // ── UpdateUserMetricsAsync ────────────────────────────────────────────

    [Fact]
    public async Task UpdateUserMetrics_FirstCall_CreatesRowWithCountOneAndUniqueCampaignsOne()
    {
        using var db = new TestDb();
        var userId = TestSeedHelpers.SeedUser(db, "um-first");
        var campaign = TestSeedHelpers.SeedCampaign(db);
        var before = DateTime.UtcNow;
        var sut = CreateService(db);

        await sut.UpdateUserMetricsAsync(userId, campaign.Id, 75m);

        var metrics = await db.Context.UserMetrics.SingleAsync(m => m.UserId == userId);
        Assert.Equal(1, metrics.DonationCount);
        Assert.Equal(1, metrics.UniqueCampaigns);
        Assert.Equal(75m, metrics.HighDonation);
        Assert.True(metrics.LastUpdated >= before);
    }

    [Fact]
    public async Task UpdateUserMetrics_SubsequentCall_IncrementsDonationCount()
    {
        using var db = new TestDb();
        var userId = TestSeedHelpers.SeedUser(db, "um-inc");
        var campaign = TestSeedHelpers.SeedCampaign(db);
        SeedMetrics(db, userId, donationCount: 3, uniqueCampaigns: 2, highDonation: 100m);
        var sut = CreateService(db);

        await sut.UpdateUserMetricsAsync(userId, campaign.Id, 50m);

        var metrics = await db.Context.UserMetrics.SingleAsync(m => m.UserId == userId);
        Assert.Equal(4, metrics.DonationCount);
    }

    [Fact]
    public async Task UpdateUserMetrics_HigherAmount_UpdatesHighDonation()
    {
        using var db = new TestDb();
        var userId = TestSeedHelpers.SeedUser(db, "um-hi");
        var campaign = TestSeedHelpers.SeedCampaign(db);
        SeedMetrics(db, userId, donationCount: 1, uniqueCampaigns: 1, highDonation: 100m);
        var sut = CreateService(db);

        await sut.UpdateUserMetricsAsync(userId, campaign.Id, 250m);

        var metrics = await db.Context.UserMetrics.SingleAsync(m => m.UserId == userId);
        Assert.Equal(250m, metrics.HighDonation);
    }

    [Fact]
    public async Task UpdateUserMetrics_LowerAmount_LeavesHighDonationUnchanged()
    {
        using var db = new TestDb();
        var userId = TestSeedHelpers.SeedUser(db, "um-lo");
        var campaign = TestSeedHelpers.SeedCampaign(db);
        SeedMetrics(db, userId, donationCount: 1, uniqueCampaigns: 1, highDonation: 500m);
        var sut = CreateService(db);

        await sut.UpdateUserMetricsAsync(userId, campaign.Id, 10m);

        var metrics = await db.Context.UserMetrics.SingleAsync(m => m.UserId == userId);
        Assert.Equal(500m, metrics.HighDonation);
    }

    [Fact]
    public async Task UpdateUserMetrics_NewCampaign_NoPriorDonationRow_IncrementsUniqueCampaigns()
    {
        // Models the documented call ordering "before the donation is inserted":
        // the new campaign id is not yet present in Donations, so UniqueCampaigns
        // should tick up.
        using var db = new TestDb();
        var userId = TestSeedHelpers.SeedUser(db, "um-newc");
        var oldCampaign = TestSeedHelpers.SeedCampaign(db, title: "Old");
        var newCampaign = TestSeedHelpers.SeedCampaign(db, title: "New");
        TestSeedHelpers.SeedDonation(db, oldCampaign.Id, 50m, userId: userId);
        SeedMetrics(db, userId, donationCount: 1, uniqueCampaigns: 1, highDonation: 50m);
        var sut = CreateService(db);

        await sut.UpdateUserMetricsAsync(userId, newCampaign.Id, 80m);

        var metrics = await db.Context.UserMetrics.SingleAsync(m => m.UserId == userId);
        Assert.Equal(2, metrics.UniqueCampaigns);
    }

    [Fact]
    public async Task UpdateUserMetrics_RepeatCampaign_DonationAlreadyExists_DoesNotIncrementUniqueCampaigns()
    {
        using var db = new TestDb();
        var userId = TestSeedHelpers.SeedUser(db, "um-repeat");
        var campaign = TestSeedHelpers.SeedCampaign(db);
        TestSeedHelpers.SeedDonation(db, campaign.Id, 50m, userId: userId);
        SeedMetrics(db, userId, donationCount: 1, uniqueCampaigns: 1, highDonation: 50m);
        var sut = CreateService(db);

        await sut.UpdateUserMetricsAsync(userId, campaign.Id, 60m);

        var metrics = await db.Context.UserMetrics.SingleAsync(m => m.UserId == userId);
        Assert.Equal(1, metrics.UniqueCampaigns);
        Assert.Equal(2, metrics.DonationCount);
    }
}
