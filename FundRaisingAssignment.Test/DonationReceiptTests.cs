using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

// ─────────────────────────────────────────────────────────────────────────────
// Test plan: 10.1 View donation receipts
// User Story: DN03 – Make a Donation to a Campaign (receipt issuance)
// Backs: ICampaignService.DonateAsync — the receipt page (DonationConfirmation)
//        is a thin read of the persisted Donation row.
// ─────────────────────────────────────────────────────────────────────────────

namespace FundRaisingAssignment.Test;

public class DonationReceiptTests
{
    private static CampaignService CreateService(TestDb db) =>
        new(db.Context, NullLogger<CampaignService>.Instance);

    [Fact]
    public async Task Donate_AssignsReceiptNumber_OnSuccess()
    {
        using var db = new TestDb();
        var campaign = TestSeedHelpers.SeedCampaign(db, target: 5_000m);
        var donorId = TestSeedHelpers.SeedUser(db, "donor1");
        var sut = CreateService(db);

        var result = await sut.DonateAsync(new MakeDonationInput(
            CampaignId: campaign.Id, Amount: 100m, Message: "Glad to help",
            IsAnonymous: false, UserId: donorId, DonorEmail: "donor1@test.local"));

        var success = Assert.IsType<DonationResult.Success>(result);
        Assert.False(string.IsNullOrWhiteSpace(success.Donation.ReceiptNumber));
        Assert.Equal(8, success.Donation.ReceiptNumber!.Length);
        Assert.Equal(success.Donation.ReceiptNumber, success.Donation.ReceiptNumber.ToUpperInvariant());
    }

    [Fact]
    public async Task Receipt_IsLookupableById_AndExposesAllReceiptFields()
    {
        using var db = new TestDb();
        var campaign = TestSeedHelpers.SeedCampaign(db, title: "Cancer Treatment", target: 5_000m);
        var donorId = TestSeedHelpers.SeedUser(db, "donor2");
        var sut = CreateService(db);

        var donate = await sut.DonateAsync(new MakeDonationInput(
            CampaignId: campaign.Id, Amount: 250m, Message: "Stay strong",
            IsAnonymous: false, UserId: donorId, DonorEmail: "donor2@test.local"));
        var donationId = ((DonationResult.Success)donate).Donation.Id;

        // Mirror the DonationConfirmation page lookup.
        var receipt = await db.Context.Donations
            .Include(d => d.Campaign)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == donationId);

        Assert.NotNull(receipt);
        Assert.Equal(250m, receipt!.Amount);
        Assert.Equal("Stay strong", receipt.Message);
        Assert.False(receipt.IsAnonymous);
        Assert.Equal(DonationStatus.Completed, receipt.Status);
        Assert.False(string.IsNullOrWhiteSpace(receipt.ReceiptNumber));
        Assert.NotNull(receipt.Campaign);
        Assert.Equal("Cancer Treatment", receipt.Campaign!.Title);
    }

    [Fact]
    public async Task Receipt_ReflectsGoalReached_WhenCampaignAutoCompletes()
    {
        using var db = new TestDb();
        var campaign = TestSeedHelpers.SeedCampaign(db, target: 100m, current: 90m);
        var sut = CreateService(db);

        var result = await sut.DonateAsync(new MakeDonationInput(
            CampaignId: campaign.Id, Amount: 25m, Message: null,
            IsAnonymous: false, UserId: TestSeedHelpers.SeedUser(db, "donor3"),
            DonorEmail: "donor3@test.local"));

        var success = Assert.IsType<DonationResult.Success>(result);
        Assert.True(success.GoalReached);

        var fresh = await db.Context.Campaigns.AsNoTracking()
            .FirstAsync(c => c.Id == campaign.Id);
        Assert.Equal(CampaignStatus.Completed, fresh.Status);
    }

    [Fact]
    public async Task Receipt_AnonymousDonation_StoresAnonymousMarker()
    {
        using var db = new TestDb();
        var campaign = TestSeedHelpers.SeedCampaign(db, target: 5_000m);
        var sut = CreateService(db);

        var result = await sut.DonateAsync(new MakeDonationInput(
            CampaignId: campaign.Id, Amount: 30m, Message: null,
            IsAnonymous: true, UserId: TestSeedHelpers.SeedUser(db, "donor4"),
            DonorEmail: "real-email-should-be-hidden@test.local"));

        var success = Assert.IsType<DonationResult.Success>(result);
        Assert.True(success.Donation.IsAnonymous);
        Assert.Equal("Anonymous", success.Donation.DonorEmail);
    }

    [Fact]
    public async Task Receipt_GeneratesUniqueReceiptNumbers_AcrossMultipleDonations()
    {
        using var db = new TestDb();
        var campaign = TestSeedHelpers.SeedCampaign(db, target: 5_000m);
        var sut = CreateService(db);

        var receipts = new HashSet<string>();
        for (int i = 0; i < 5; i++)
        {
            var r = await sut.DonateAsync(new MakeDonationInput(
                CampaignId: campaign.Id, Amount: 10m + i, Message: null,
                IsAnonymous: false, UserId: TestSeedHelpers.SeedUser(db, $"d-{i}"),
                DonorEmail: $"d{i}@test.local"));
            var receipt = ((DonationResult.Success)r).Donation.ReceiptNumber!;
            Assert.True(receipts.Add(receipt), $"Duplicate receipt number: {receipt}");
        }

        Assert.Equal(5, receipts.Count);
    }
}
