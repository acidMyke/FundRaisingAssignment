// Migrated from DonationServiceTests.cs after the four duplicate donation flows
// were consolidated onto ICampaignService.DonateAsync. All original assertions
// (success, anonymity, goal-reached auto-completion, CampaignNotFound,
// CampaignNotActive across six statuses, deadline rejection, future deadline
// accepted) are preserved. Two new theories cover guest donors and the new
// InvalidAmount validation branch.

using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FundRaisingAssignment.Test;

public class CampaignServiceDonateTests
{
    private static Guid SeedUser(TestDb db, string suffix)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"u{suffix}@test.local",
            NormalizedUserName = $"U{suffix.ToUpperInvariant()}@TEST.LOCAL",
            Email = $"u{suffix}@test.local",
            NormalizedEmail = $"U{suffix.ToUpperInvariant()}@TEST.LOCAL",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();
        return user.Id;
    }

    private static Campaign SeedCampaign(
        TestDb db,
        CampaignStatus status = CampaignStatus.Active,
        decimal target = 1000m,
        decimal current = 0m,
        DateTime? endDate = null)
    {
        var ownerId = SeedUser(db, "owner-" + Guid.NewGuid().ToString("N")[..8]);
        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            Title = "Test Campaign",
            Description = "Test description",
            ShortDescription = "Short",
            TargetAmount = target,
            CurrentAmount = current,
            Status = status,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = endDate,
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        db.Context.Campaigns.Add(campaign);
        db.Context.SaveChanges();
        return campaign;
    }

    private static Guid SeedDonor(TestDb db) =>
        SeedUser(db, "donor-" + Guid.NewGuid().ToString("N")[..8]);

    private static CampaignService CreateService(TestDb db) =>
        new(db.Context, NullLogger<CampaignService>.Instance);

    [Fact]
    public async Task Donate_Succeeds_PersistsDonation_AndIncrementsCampaignTotal()
    {
        using var db = new TestDb();
        var campaign = SeedCampaign(db, target: 500m, current: 100m);
        var donor = SeedDonor(db);
        var sut = CreateService(db);

        var result = await sut.DonateAsync(
            new MakeDonationInput(
                CampaignId: campaign.Id,
                Amount: 50m,
                Message: "Hello",
                IsAnonymous: false,
                UserId: donor,
                DonorEmail: "donor@test.local"),
            CancellationToken.None);

        var success = Assert.IsType<DonationResult.Success>(result);
        Assert.Equal(50m, success.Donation.Amount);
        Assert.Equal(DonationStatus.Completed, success.Donation.Status);
        Assert.Equal("Hello", success.Donation.Message);
        Assert.False(success.GoalReached);

        var fresh = await db.Context.Campaigns.AsNoTracking()
            .FirstAsync(c => c.Id == campaign.Id);
        Assert.Equal(150m, fresh.CurrentAmount);
        Assert.Equal(CampaignStatus.Active, fresh.Status);

        Assert.Equal(1, await db.Context.Donations.CountAsync());
    }

    [Fact]
    public async Task Donate_AnonymousFlag_IsPersisted()
    {
        using var db = new TestDb();
        var campaign = SeedCampaign(db);
        var sut = CreateService(db);

        var result = await sut.DonateAsync(
            new MakeDonationInput(
                CampaignId: campaign.Id,
                Amount: 25m,
                Message: null,
                IsAnonymous: true,
                UserId: SeedDonor(db),
                DonorEmail: "donor@test.local"),
            CancellationToken.None);

        var success = Assert.IsType<DonationResult.Success>(result);
        Assert.True(success.Donation.IsAnonymous);
        Assert.Null(success.Donation.Message);
    }

    [Fact]
    public async Task Donate_GoalReached_AutoCompletesCampaign()
    {
        using var db = new TestDb();
        var campaign = SeedCampaign(db, target: 100m, current: 90m);
        var sut = CreateService(db);

        var result = await sut.DonateAsync(
            new MakeDonationInput(
                CampaignId: campaign.Id,
                Amount: 25m,
                Message: null,
                IsAnonymous: false,
                UserId: SeedDonor(db),
                DonorEmail: "donor@test.local"),
            CancellationToken.None);

        var success = Assert.IsType<DonationResult.Success>(result);
        Assert.True(success.GoalReached);

        var fresh = await db.Context.Campaigns.AsNoTracking()
            .FirstAsync(c => c.Id == campaign.Id);
        Assert.Equal(115m, fresh.CurrentAmount);
        Assert.Equal(CampaignStatus.Completed, fresh.Status);
    }

    [Fact]
    public async Task Donate_CampaignNotFound_ReturnsNotFoundResult()
    {
        using var db = new TestDb();
        var sut = CreateService(db);

        var result = await sut.DonateAsync(
            new MakeDonationInput(
                CampaignId: Guid.NewGuid(),
                Amount: 10m,
                Message: null,
                IsAnonymous: false,
                UserId: SeedDonor(db),
                DonorEmail: "donor@test.local"),
            CancellationToken.None);

        Assert.IsType<DonationResult.CampaignNotFound>(result);
        Assert.Equal(0, await db.Context.Donations.CountAsync());
    }

    [Theory]
    [InlineData(CampaignStatus.Draft)]
    [InlineData(CampaignStatus.PendingReview)]
    [InlineData(CampaignStatus.Paused)]
    [InlineData(CampaignStatus.Suspended)]
    [InlineData(CampaignStatus.Completed)]
    [InlineData(CampaignStatus.Cancelled)]
    public async Task Donate_CampaignNotActive_RejectsAndPersistsNothing(CampaignStatus status)
    {
        using var db = new TestDb();
        var campaign = SeedCampaign(db, status: status);
        var sut = CreateService(db);

        var result = await sut.DonateAsync(
            new MakeDonationInput(
                CampaignId: campaign.Id,
                Amount: 10m,
                Message: null,
                IsAnonymous: false,
                UserId: SeedDonor(db),
                DonorEmail: "donor@test.local"),
            CancellationToken.None);

        var rejected = Assert.IsType<DonationResult.CampaignNotActive>(result);
        Assert.Equal(status, rejected.CurrentStatus);
        Assert.Equal(0, await db.Context.Donations.CountAsync());

        var fresh = await db.Context.Campaigns.AsNoTracking()
            .FirstAsync(c => c.Id == campaign.Id);
        Assert.Equal(0m, fresh.CurrentAmount);
    }

    [Fact]
    public async Task Donate_DeadlinePassed_RejectsAndPersistsNothing()
    {
        using var db = new TestDb();
        var campaign = SeedCampaign(db, endDate: DateTime.UtcNow.AddDays(-1));
        var sut = CreateService(db);

        var result = await sut.DonateAsync(
            new MakeDonationInput(
                CampaignId: campaign.Id,
                Amount: 10m,
                Message: null,
                IsAnonymous: false,
                UserId: SeedDonor(db),
                DonorEmail: "donor@test.local"),
            CancellationToken.None);

        Assert.IsType<DonationResult.DeadlinePassed>(result);
        Assert.Equal(0, await db.Context.Donations.CountAsync());

        var fresh = await db.Context.Campaigns.AsNoTracking()
            .FirstAsync(c => c.Id == campaign.Id);
        Assert.Equal(0m, fresh.CurrentAmount);
    }

    [Fact]
    public async Task Donate_DeadlineInFuture_IsAccepted()
    {
        using var db = new TestDb();
        var campaign = SeedCampaign(db, endDate: DateTime.UtcNow.AddDays(7));
        var sut = CreateService(db);

        var result = await sut.DonateAsync(
            new MakeDonationInput(
                CampaignId: campaign.Id,
                Amount: 10m,
                Message: null,
                IsAnonymous: false,
                UserId: SeedDonor(db),
                DonorEmail: "donor@test.local"),
            CancellationToken.None);

        Assert.IsType<DonationResult.Success>(result);
    }

    [Fact]
    public async Task Donate_GuestUser_PersistsWithNullUserIdAndEmail()
    {
        using var db = new TestDb();
        var campaign = SeedCampaign(db);
        var sut = CreateService(db);

        var result = await sut.DonateAsync(
            new MakeDonationInput(
                CampaignId: campaign.Id,
                Amount: 15m,
                Message: "From a guest",
                IsAnonymous: false,
                UserId: null,
                DonorEmail: "guest@example.com"),
            CancellationToken.None);

        var success = Assert.IsType<DonationResult.Success>(result);
        Assert.Null(success.Donation.UserId);
        Assert.Equal("guest@example.com", success.Donation.DonorEmail);

        var fresh = await db.Context.Donations.AsNoTracking()
            .FirstAsync(d => d.Id == success.Donation.Id);
        Assert.Null(fresh.UserId);
        Assert.Equal("guest@example.com", fresh.DonorEmail);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(2_000_000)]
    public async Task Donate_InvalidAmount_RejectsAndPersistsNothing(decimal amount)
    {
        using var db = new TestDb();
        var campaign = SeedCampaign(db, current: 0m);
        var sut = CreateService(db);

        var result = await sut.DonateAsync(
            new MakeDonationInput(
                CampaignId: campaign.Id,
                Amount: amount,
                Message: null,
                IsAnonymous: false,
                UserId: SeedDonor(db),
                DonorEmail: "donor@test.local"),
            CancellationToken.None);

        Assert.IsType<DonationResult.InvalidAmount>(result);
        Assert.Equal(0, await db.Context.Donations.CountAsync());

        var fresh = await db.Context.Campaigns.AsNoTracking()
            .FirstAsync(c => c.Id == campaign.Id);
        Assert.Equal(0m, fresh.CurrentAmount);
    }
}
