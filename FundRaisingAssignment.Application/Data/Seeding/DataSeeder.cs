// Bulk-inserts a realistic mixed dataset (users, campaigns, donations, refund logs)
// into ANY ApplicationDbContext — used by the `dotnet run -- --seed` CLI mode for
// populating Postgres dev databases, and by TestDataSeeder for in-memory SQLite
// tests. Bypasses ICampaignService.DonateAsync so it can stamp historical
// CreatedAt/Status values directly; not a substitute for the canonical donation
// path in production code.

using FundRaisingAssignment.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace FundRaisingAssignment.Application.Data.Seeding;

public sealed record SeedResult(
    IReadOnlyList<Guid> OwnerIds,
    IReadOnlyList<Guid> DonorIds,
    IReadOnlyList<Guid> CampaignIds,
    IReadOnlyList<Guid> DonationIds,
    int RefundedCount);

public static class DataSeeder
{
    public static async Task<SeedResult> SeedLargeDatasetAsync(
        ApplicationDbContext ctx,
        int ownerCount = 20,
        int donorCount = 50,
        int campaignCount = 100,
        int donationCount = 1000,
        int randomSeed = 12345,
        CancellationToken ct = default)
    {
        var rng = new Random(randomSeed);
        ctx.ChangeTracker.AutoDetectChangesEnabled = false;

        // Suffix with a random hex string so reruns don't collide on NormalizedUserName.
        var runTag = Guid.NewGuid().ToString("N")[..8];
        var owners = Enumerable.Range(0, ownerCount)
            .Select(i => CreateUser($"seed-{runTag}-owner{i:D3}", rng))
            .ToList();
        var donors = Enumerable.Range(0, donorCount)
            .Select(i => CreateUser($"seed-{runTag}-donor{i:D3}", rng))
            .ToList();
        ctx.Users.AddRange(owners);
        ctx.Users.AddRange(donors);

        var statusMix = BuildCampaignStatusMix(campaignCount);
        var campaigns = new List<Campaign>(campaignCount);
        for (int i = 0; i < campaignCount; i++)
        {
            var owner = owners[rng.Next(owners.Count)];
            var status = statusMix[i];
            var target = RandomTarget(rng);
            campaigns.Add(new Campaign
            {
                Id = Guid.NewGuid(),
                Title = $"Seed Campaign #{i:D4}",
                ShortDescription = $"Short description for seed campaign {i}.",
                Description = $"Long description for seed campaign {i}. " +
                              $"status={status}, target=${target:N0}.",
                Category = (CampaignCategory)(i % Enum.GetValues<CampaignCategory>().Length),
                Location = PickLocation(rng),
                FundingGoal = target,
                TargetAmount = target,
                CurrentAmount = 0m,
                Status = status,
                StartDate = DateTime.UtcNow.AddDays(-rng.Next(30, 180)),
                CreatedAt = DateTime.UtcNow.AddDays(-rng.Next(30, 180)),
                EndDate = rng.NextDouble() < 0.7 ? DateTime.UtcNow.AddDays(rng.Next(7, 90)) : null,
                PublishedAt = status is CampaignStatus.Active or CampaignStatus.Completed
                              ? DateTime.UtcNow.AddDays(-rng.Next(1, 60))
                              : null,
                OwnerId = owner.Id,
            });
        }
        ctx.Campaigns.AddRange(campaigns);

        var donatable = campaigns
            .Where(c => c.Status is CampaignStatus.Active or CampaignStatus.Completed)
            .ToList();
        if (donatable.Count == 0) donatable = campaigns;

        var donations = new List<Donation>(donationCount);
        var refundLogs = new List<RefundLog>();
        var perCampaignTotals = new Dictionary<Guid, decimal>();

        for (int i = 0; i < donationCount; i++)
        {
            var campaign = donatable[rng.Next(donatable.Count)];
            var (userId, donorEmail, isAnonymous) = PickDonorIdentity(rng, donors);
            var amount = RandomDonationAmount(rng);
            var createdAt = DateTime.UtcNow.AddDays(-rng.NextDouble() * 90);

            var isRefunded = rng.NextDouble() < 0.05;
            var status = isRefunded ? DonationStatus.Refunded : DonationStatus.Completed;

            var donation = new Donation
            {
                Id = Guid.NewGuid(),
                CampaignId = campaign.Id,
                UserId = userId,
                DonorEmail = isAnonymous ? "Anonymous" : donorEmail,
                Amount = amount,
                Message = rng.NextDouble() < 0.3 ? $"Seeded donor message {i}" : null,
                IsAnonymous = isAnonymous,
                Status = status,
                CreatedAt = createdAt,
                ReceiptNumber = $"RCPT-{createdAt:yyyyMMdd}-{i:D4}",
                PaymentMethod = PickPaymentMethod(rng),
            };
            donations.Add(donation);

            if (status == DonationStatus.Completed)
            {
                perCampaignTotals.TryGetValue(campaign.Id, out var running);
                perCampaignTotals[campaign.Id] = running + amount;
            }
            else
            {
                refundLogs.Add(new RefundLog
                {
                    Id = Guid.NewGuid(),
                    DonationId = donation.Id,
                    CampaignId = campaign.Id,
                    AdminId = null,
                    AdminLabel = "seed-admin",
                    Amount = amount,
                    Reason = "Seeded refund",
                    RefundedAt = createdAt.AddHours(rng.Next(1, 72)),
                });
            }
        }
        ctx.Donations.AddRange(donations);
        ctx.RefundLogs.AddRange(refundLogs);

        foreach (var campaign in campaigns)
        {
            if (perCampaignTotals.TryGetValue(campaign.Id, out var total))
                campaign.CurrentAmount = total;
        }

        ctx.ChangeTracker.DetectChanges();
        await ctx.SaveChangesAsync(ct);
        ctx.ChangeTracker.AutoDetectChangesEnabled = true;
        ctx.ChangeTracker.Clear();

        return new SeedResult(
            OwnerIds: owners.Select(u => u.Id).ToList(),
            DonorIds: donors.Select(u => u.Id).ToList(),
            CampaignIds: campaigns.Select(c => c.Id).ToList(),
            DonationIds: donations.Select(d => d.Id).ToList(),
            RefundedCount: refundLogs.Count);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static ApplicationUser CreateUser(string handle, Random _) => new()
    {
        Id = Guid.NewGuid(),
        UserName = $"{handle}@test.local",
        NormalizedUserName = $"{handle.ToUpperInvariant()}@TEST.LOCAL",
        Email = $"{handle}@test.local",
        NormalizedEmail = $"{handle.ToUpperInvariant()}@TEST.LOCAL",
        EmailConfirmed = true,
        SecurityStamp = Guid.NewGuid().ToString(),
        ConcurrencyStamp = Guid.NewGuid().ToString(),
    };

    private static List<CampaignStatus> BuildCampaignStatusMix(int total)
    {
        var mix = new List<CampaignStatus>(total);
        int active = (int)Math.Round(total * 0.60);
        int completed = (int)Math.Round(total * 0.05);
        int draft = (int)Math.Round(total * 0.10);
        int pending = (int)Math.Round(total * 0.05);
        int paused = (int)Math.Round(total * 0.05);
        int flagged = (int)Math.Round(total * 0.05);
        int suspended = (int)Math.Round(total * 0.05);
        int cancelled = total - active - completed - draft - pending - paused - flagged - suspended;
        if (cancelled < 0) { active += cancelled; cancelled = 0; }

        mix.AddRange(Enumerable.Repeat(CampaignStatus.Active, active));
        mix.AddRange(Enumerable.Repeat(CampaignStatus.Completed, completed));
        mix.AddRange(Enumerable.Repeat(CampaignStatus.Draft, draft));
        mix.AddRange(Enumerable.Repeat(CampaignStatus.PendingReview, pending));
        mix.AddRange(Enumerable.Repeat(CampaignStatus.Paused, paused));
        mix.AddRange(Enumerable.Repeat(CampaignStatus.Flagged, flagged));
        mix.AddRange(Enumerable.Repeat(CampaignStatus.Suspended, suspended));
        mix.AddRange(Enumerable.Repeat(CampaignStatus.Cancelled, cancelled));
        return mix;
    }

    private static (Guid? UserId, string Email, bool IsAnonymous) PickDonorIdentity(
        Random rng, IReadOnlyList<ApplicationUser> donors)
    {
        var roll = rng.NextDouble();
        if (roll < 0.60)
        {
            var donor = donors[rng.Next(donors.Count)];
            return (donor.Id, donor.Email!, false);
        }
        if (roll < 0.90)
        {
            var donor = donors[rng.Next(donors.Count)];
            return (donor.Id, donor.Email!, true);
        }
        return (null, $"guest{rng.Next(10000):D5}@example.com", false);
    }

    private static decimal RandomTarget(Random rng) =>
        rng.NextDouble() switch
        {
            < 0.5 => rng.Next(500, 5_000),
            < 0.85 => rng.Next(5_000, 50_000),
            _ => rng.Next(50_000, 500_000),
        };

    private static decimal RandomDonationAmount(Random rng)
    {
        var roll = rng.NextDouble();
        if (roll < 0.70) return Math.Round((decimal)(rng.NextDouble() * 95 + 5), 2);
        if (roll < 0.95) return Math.Round((decimal)(rng.NextDouble() * 900 + 100), 2);
        return Math.Round((decimal)(rng.NextDouble() * 9_000 + 1_000), 2);
    }

    private static string PickLocation(Random rng) =>
        new[] { "Singapore", "Sydney", "Melbourne", "Auckland", "London", "Toronto", "New York" }
            [rng.Next(7)];

    private static string PickPaymentMethod(Random rng) =>
        new[] { "Credit Card", "PayPal", "Bank Transfer", "Other" }[rng.Next(4)];
}
