using FundRaisingAssignment.Application.Models;

namespace FundRaisingAssignment.Test;

/// <summary>
/// Lightweight seed helpers shared across feature tests for sections 10.1–10.12.
/// All helpers persist directly via the test DbContext and return the entity
/// (or its Id) so tests can chain follow-up operations without re-reading.
/// </summary>
internal static class TestSeedHelpers
{
    public static Guid SeedUser(TestDb db, string suffix)
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

    public static Campaign SeedCampaign(
        TestDb db,
        Guid? ownerId = null,
        string title = "Test Campaign",
        CampaignStatus status = CampaignStatus.Active,
        decimal target = 1000m,
        decimal current = 0m,
        DateTime? endDate = null,
        CampaignCategory category = CampaignCategory.Other,
        string? location = null,
        string description = "Test description")
    {
        var owner = ownerId ?? SeedUser(db, "owner-" + Guid.NewGuid().ToString("N")[..8]);
        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            ShortDescription = "Short",
            Category = category,
            Location = location,
            FundingGoal = target,
            TargetAmount = target,
            CurrentAmount = current,
            Status = status,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = endDate,
            OwnerId = owner,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
        };
        db.Context.Campaigns.Add(campaign);
        db.Context.SaveChanges();
        return campaign;
    }

    public static Donation SeedDonation(
        TestDb db,
        Guid campaignId,
        decimal amount,
        Guid? userId = null,
        string donorEmail = "donor@test.local",
        bool isAnonymous = false,
        DonationStatus status = DonationStatus.Completed,
        DateTime? createdAt = null,
        string? message = null,
        string paymentMethod = "Card",
        string? receiptNumber = null)
    {
        var donation = new Donation
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            UserId = userId,
            DonorEmail = isAnonymous ? "Anonymous" : donorEmail,
            IsAnonymous = isAnonymous,
            Amount = amount,
            Message = message,
            Status = status,
            PaymentMethod = paymentMethod,
            ReceiptNumber = receiptNumber ?? Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            CreatedAt = createdAt ?? DateTime.UtcNow,
        };
        db.Context.Donations.Add(donation);
        db.Context.SaveChanges();
        return donation;
    }
}
