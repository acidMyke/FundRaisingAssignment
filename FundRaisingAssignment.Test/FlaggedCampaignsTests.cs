using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

// ─────────────────────────────────────────────────────────────────────────────
// Test plan: 10.7 Review flagged campaigns
// User Story: PM01 – Review Flagged Campaign
// Backs: ICampaignService.GetFlaggedCampaignsAsync, ApproveCampaignAsync,
//        RemoveCampaignAsync, FlagCampaignAsync, NotifyFundRaiserAsync.
// ─────────────────────────────────────────────────────────────────────────────

namespace FundRaisingAssignment.Test;

public class FlaggedCampaignsTests
{
    private static CampaignService CreateService(TestDb db) =>
        new(db.Context, NullLogger<CampaignService>.Instance);

    [Fact]
    public async Task GetFlaggedCampaigns_ReturnsOnlyFlaggedStatus()
    {
        using var db = new TestDb();
        var flagged = TestSeedHelpers.SeedCampaign(db, status: CampaignStatus.Flagged, title: "Flagged");
        TestSeedHelpers.SeedCampaign(db, status: CampaignStatus.Active, title: "Active");
        TestSeedHelpers.SeedCampaign(db, status: CampaignStatus.Paused, title: "Paused");
        var sut = CreateService(db);

        var results = await sut.GetFlaggedCampaignsAsync();

        Assert.Single(results);
        Assert.Equal(flagged.Id, results[0].Id);
    }

    [Fact]
    public async Task ApproveCampaign_TransitionsToActiveAndClearsFlagReason()
    {
        using var db = new TestDb();
        var flagged = TestSeedHelpers.SeedCampaign(db, status: CampaignStatus.Flagged);
        flagged.FlagReason = "spammy content";
        await db.Context.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.ApproveCampaignAsync(flagged.Id);

        Assert.Equal(CampaignStatus.Active, result.Status);
        Assert.Null(result.FlagReason);

        var fresh = await db.Context.Campaigns.AsNoTracking().FirstAsync(c => c.Id == flagged.Id);
        Assert.Equal(CampaignStatus.Active, fresh.Status);
        Assert.Null(fresh.FlagReason);
    }

    [Fact]
    public async Task RemoveCampaign_TransitionsToCancelled_AndStoresReason()
    {
        using var db = new TestDb();
        var flagged = TestSeedHelpers.SeedCampaign(db, status: CampaignStatus.Flagged);
        var sut = CreateService(db);

        var result = await sut.RemoveCampaignAsync(flagged.Id, "violates platform policy");

        Assert.Equal(CampaignStatus.Cancelled, result.Status);
        Assert.Equal("violates platform policy", result.FlagReason);

        var fresh = await db.Context.Campaigns.AsNoTracking().FirstAsync(c => c.Id == flagged.Id);
        Assert.Equal(CampaignStatus.Cancelled, fresh.Status);
        Assert.Equal("violates platform policy", fresh.FlagReason);
    }

    [Fact]
    public async Task FlagCampaign_SetsFlaggedStatus_AndCapturesReason()
    {
        using var db = new TestDb();
        var active = TestSeedHelpers.SeedCampaign(db, status: CampaignStatus.Active);
        var sut = CreateService(db);

        var result = await sut.FlagCampaignAsync(active.Id, "reported by donors");

        Assert.Equal(CampaignStatus.Flagged, result.Status);
        Assert.Equal("reported by donors", result.FlagReason);
    }

    [Fact]
    public async Task ApproveCampaign_UnknownId_Throws()
    {
        using var db = new TestDb();
        var sut = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ApproveCampaignAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task NotifyFundRaiser_PersistsNotificationRow()
    {
        using var db = new TestDb();
        var campaign = TestSeedHelpers.SeedCampaign(db);
        var sut = CreateService(db);

        var notification = await sut.NotifyFundRaiserAsync(campaign.Id, "Approved");

        Assert.Equal(campaign.Id, notification.CampaignId);
        var count = await db.Context.FundRaiserNotifications
            .Where(n => n.CampaignId == campaign.Id)
            .CountAsync();
        Assert.Equal(1, count);
    }

    // ---- Auto-flag pipeline (PM01 Diagram 3) ----------------------------------

    [Fact]
    public async Task AddReview_LowStars_AutoFlagsActiveCampaign()
    {
        using var db = new TestDb();
        var campaign = TestSeedHelpers.SeedCampaign(db, status: CampaignStatus.Active);
        var reviewerId = TestSeedHelpers.SeedUser(db, "reviewer-1");
        var sut = CreateService(db);

        await sut.AddReviewAsync(campaign.Id, reviewerId, "reviewer1@test.local", stars: 1, comment: "Bad");

        var fresh = await db.Context.Campaigns.AsNoTracking().FirstAsync(c => c.Id == campaign.Id);
        Assert.Equal(CampaignStatus.Flagged, fresh.Status);
        Assert.NotNull(fresh.FlagReason);
        Assert.Contains("Auto-flagged", fresh.FlagReason);
    }

    [Fact]
    public async Task AddReview_HighStars_DoesNotAutoFlag()
    {
        using var db = new TestDb();
        var campaign = TestSeedHelpers.SeedCampaign(db, status: CampaignStatus.Active);
        var reviewerId = TestSeedHelpers.SeedUser(db, "reviewer-2");
        var sut = CreateService(db);

        await sut.AddReviewAsync(campaign.Id, reviewerId, "reviewer2@test.local", stars: 4, comment: "Good");

        var fresh = await db.Context.Campaigns.AsNoTracking().FirstAsync(c => c.Id == campaign.Id);
        Assert.Equal(CampaignStatus.Active, fresh.Status);
    }
}
