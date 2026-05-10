using FundRaisingAssignment.Application.Models;
using Microsoft.EntityFrameworkCore;

// ─────────────────────────────────────────────────────────────────────────────
// Test plan: 10.10 Personalised thank-you messages
// User Story: FR03 – Send Thank-You Message to Donors
// Backs: ThankDonorsModel.LoadAsync donor-eligibility predicate. The actual
//        Mailjet send pipeline is integration-only; here we verify the data
//        contract: donations included must be Completed and not anonymous,
//        with anonymous/guest sentinel emails excluded, and the in-memory
//        aggregation collapses by email.
// ─────────────────────────────────────────────────────────────────────────────

namespace FundRaisingAssignment.Test;

public class ThankDonorsEligibilityTests
{
    private static IReadOnlyList<DonorRow> BuildDonorRows(IEnumerable<Donation> donations) =>
        donations
            .Select(d => new
            {
                Email = (d.User?.Email ?? d.DonorEmail ?? "").Trim(),
                Name = d.User?.UserName ?? d.DonorEmail ?? "Donor",
                d.Amount,
                d.CreatedAt
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Email)
                     && !x.Email.Equals("Anonymous", StringComparison.OrdinalIgnoreCase)
                     && !x.Email.Equals("Guest", StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => x.Email, StringComparer.OrdinalIgnoreCase)
            .Select(g => new DonorRow(
                Email: g.Key,
                DisplayName: g.First().Name,
                TotalDonated: g.Sum(x => x.Amount),
                DonationCount: g.Count(),
                LastDonationAt: g.Max(x => x.CreatedAt)))
            .OrderByDescending(d => d.LastDonationAt)
            .ToList();

    private sealed record DonorRow(
        string Email,
        string DisplayName,
        decimal TotalDonated,
        int DonationCount,
        DateTime LastDonationAt);

    [Fact]
    public async Task EligibleDonations_AreCompletedAndNotAnonymous()
    {
        using var db = new TestDb();
        var campaign = TestSeedHelpers.SeedCampaign(db);
        TestSeedHelpers.SeedDonation(db, campaign.Id, amount: 50m, donorEmail: "ann@x", isAnonymous: false, status: DonationStatus.Completed);
        TestSeedHelpers.SeedDonation(db, campaign.Id, amount: 30m, donorEmail: "anon@x", isAnonymous: true,  status: DonationStatus.Completed);
        TestSeedHelpers.SeedDonation(db, campaign.Id, amount: 99m, donorEmail: "pending@x", isAnonymous: false, status: DonationStatus.Pending);
        TestSeedHelpers.SeedDonation(db, campaign.Id, amount: 11m, donorEmail: "refund@x", isAnonymous: false, status: DonationStatus.Refunded);

        var eligible = await db.Context.Donations.AsNoTracking()
            .Where(d => d.CampaignId == campaign.Id
                     && d.Status == DonationStatus.Completed
                     && !d.IsAnonymous)
            .ToListAsync();

        Assert.Single(eligible);
        Assert.Equal("ann@x", eligible[0].DonorEmail);
    }

    [Fact]
    public void DonorRows_GroupByEmail_AndAggregateAmountsAndCounts()
    {
        var donations = new[]
        {
            new Donation { Id = Guid.NewGuid(), DonorEmail = "ann@x", Amount = 50m, CreatedAt = new DateTime(2026, 5, 1) },
            new Donation { Id = Guid.NewGuid(), DonorEmail = "ann@x", Amount = 25m, CreatedAt = new DateTime(2026, 5, 5) },
            new Donation { Id = Guid.NewGuid(), DonorEmail = "ben@x", Amount = 10m, CreatedAt = new DateTime(2026, 5, 3) },
        };

        var rows = BuildDonorRows(donations);

        var ann = rows.Single(r => r.Email == "ann@x");
        Assert.Equal(75m, ann.TotalDonated);
        Assert.Equal(2,   ann.DonationCount);
        Assert.Equal(new DateTime(2026, 5, 5), ann.LastDonationAt);
        Assert.Contains(rows, r => r.Email == "ben@x");
    }

    [Fact]
    public void DonorRows_ExcludeAnonymousAndGuestSentinels()
    {
        var donations = new[]
        {
            new Donation { Id = Guid.NewGuid(), DonorEmail = "ann@x", Amount = 50m, CreatedAt = DateTime.UtcNow },
            new Donation { Id = Guid.NewGuid(), DonorEmail = "Anonymous", Amount = 10m, CreatedAt = DateTime.UtcNow },
            new Donation { Id = Guid.NewGuid(), DonorEmail = "Guest", Amount = 20m, CreatedAt = DateTime.UtcNow },
            new Donation { Id = Guid.NewGuid(), DonorEmail = "", Amount = 5m, CreatedAt = DateTime.UtcNow },
        };

        var rows = BuildDonorRows(donations);

        Assert.Single(rows);
        Assert.Equal("ann@x", rows[0].Email);
    }

    [Fact]
    public void DonorRows_OrderedByLastDonationDescending()
    {
        var donations = new[]
        {
            new Donation { Id = Guid.NewGuid(), DonorEmail = "first@x",  Amount = 1m, CreatedAt = new DateTime(2026, 1, 1) },
            new Donation { Id = Guid.NewGuid(), DonorEmail = "second@x", Amount = 1m, CreatedAt = new DateTime(2026, 6, 1) },
            new Donation { Id = Guid.NewGuid(), DonorEmail = "third@x",  Amount = 1m, CreatedAt = new DateTime(2026, 4, 1) },
        };

        var rows = BuildDonorRows(donations);

        Assert.Equal(new[] { "second@x", "third@x", "first@x" },
            rows.Select(r => r.Email).ToArray());
    }

    [Fact]
    public void DonorRows_EmailGroupingIsCaseInsensitive()
    {
        var donations = new[]
        {
            new Donation { Id = Guid.NewGuid(), DonorEmail = "ANN@x", Amount = 50m, CreatedAt = DateTime.UtcNow },
            new Donation { Id = Guid.NewGuid(), DonorEmail = "ann@x", Amount = 25m, CreatedAt = DateTime.UtcNow },
        };

        var rows = BuildDonorRows(donations);

        Assert.Single(rows);
        Assert.Equal(75m, rows[0].TotalDonated);
        Assert.Equal(2,   rows[0].DonationCount);
    }
}
