using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

// ─────────────────────────────────────────────────────────────────────────────
// Test plan: 10.2 Set funding goal & deadline
// User Story: FR01 – Set Funding Goal and Deadline
// Backs: ICampaignService.SaveGoalAndDeadlineAsync / UpdateGoalAndDeadlineAsync
//        and Campaign.UpdateGoalAndDeadline domain method.
// ─────────────────────────────────────────────────────────────────────────────

namespace FundRaisingAssignment.Test;

public class CampaignGoalDeadlineTests
{
    private static CampaignService CreateService(TestDb db) =>
        new(db.Context, NullLogger<CampaignService>.Instance);

    [Fact]
    public async Task SaveGoalAndDeadline_PersistsCampaignAsDraft_WithGoalAndDeadline()
    {
        using var db = new TestDb();
        var ownerId = TestSeedHelpers.SeedUser(db, "fr1");
        var sut = CreateService(db);
        var deadline = DateTime.UtcNow.AddDays(30);

        var campaign = await sut.SaveGoalAndDeadlineAsync(5000m, deadline, ownerId);

        Assert.NotEqual(Guid.Empty, campaign.Id);
        Assert.Equal(5000m, campaign.FundingGoal);
        Assert.Equal(5000m, campaign.TargetAmount);
        Assert.Equal(CampaignStatus.Draft, campaign.Status);
        Assert.Equal(ownerId, campaign.OwnerId);

        var fresh = await db.Context.Campaigns.AsNoTracking()
            .FirstAsync(c => c.Id == campaign.Id);
        Assert.Equal(5000m, fresh.FundingGoal);
        Assert.NotNull(fresh.EndDate);
    }

    [Fact]
    public async Task UpdateGoalAndDeadline_SetsBothFields_AndKeepsTargetInSync()
    {
        using var db = new TestDb();
        var ownerId = TestSeedHelpers.SeedUser(db, "fr2");
        var sut = CreateService(db);
        var initialDeadline = DateTime.UtcNow.AddDays(10);
        var saved = await sut.SaveGoalAndDeadlineAsync(1000m, initialDeadline, ownerId);

        var newDeadline = DateTime.UtcNow.AddDays(60);
        var updated = await sut.UpdateGoalAndDeadlineAsync(saved.Id, 2500m, newDeadline, ownerId);

        Assert.Equal(2500m, updated.FundingGoal);
        Assert.Equal(2500m, updated.TargetAmount);

        var fresh = await db.Context.Campaigns.AsNoTracking()
            .FirstAsync(c => c.Id == saved.Id);
        Assert.Equal(2500m, fresh.FundingGoal);
        Assert.Equal(2500m, fresh.TargetAmount);
    }

    [Fact]
    public async Task UpdateGoalAndDeadline_RejectsForeignOwner()
    {
        using var db = new TestDb();
        var ownerId = TestSeedHelpers.SeedUser(db, "fr3");
        var imposterId = TestSeedHelpers.SeedUser(db, "fr3-other");
        var sut = CreateService(db);
        var saved = await sut.SaveGoalAndDeadlineAsync(800m, DateTime.UtcNow.AddDays(5), ownerId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.UpdateGoalAndDeadlineAsync(saved.Id, 1500m, DateTime.UtcNow.AddDays(15), imposterId));

        var fresh = await db.Context.Campaigns.AsNoTracking()
            .FirstAsync(c => c.Id == saved.Id);
        Assert.Equal(800m, fresh.FundingGoal);
    }

    [Fact]
    public async Task UpdateGoalAndDeadline_UnknownCampaignId_Throws()
    {
        using var db = new TestDb();
        var ownerId = TestSeedHelpers.SeedUser(db, "fr4");
        var sut = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.UpdateGoalAndDeadlineAsync(Guid.NewGuid(), 100m, DateTime.UtcNow.AddDays(1), ownerId));
    }

    [Fact]
    public void DomainMethod_UpdateGoalAndDeadline_SyncsFundingGoalTargetAndEndDate()
    {
        var c = new Campaign
        {
            Id = Guid.NewGuid(),
            Title = "x",
            Description = "x",
            OwnerId = Guid.NewGuid(),
        };
        var deadline = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        c.UpdateGoalAndDeadline(750m, deadline);

        Assert.Equal(750m, c.FundingGoal);
        Assert.Equal(750m, c.TargetAmount);
        Assert.Equal(deadline, c.EndDate);
    }
}
