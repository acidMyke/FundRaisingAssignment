using FundRaisingAssignment.Application.Areas.Internal.Pages;
using FundRaisingAssignment.Application.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;

// ─────────────────────────────────────────────────────────────────────────────
// Test plan: 10.11 Platform analytics dashboards
// User Story: UA02 – Export Platform Performance and Financial Report
// Backs: ReportsModel.GenerateReportAsync (internal). UserManager isn't
//        exercised by GenerateReportAsync, so we pass null! and only feed the
//        DbContext + a NullLogger.
// ─────────────────────────────────────────────────────────────────────────────

namespace FundRaisingAssignment.Test;

public class PlatformReportTests
{
    private static ReportsModel CreateModel(TestDb db) =>
        new(db.Context, null!, NullLogger<ReportsModel>.Instance);

    [Fact]
    public async Task EmptyData_ReturnsZeroAggregates()
    {
        using var db = new TestDb();
        var model = CreateModel(db);

        var report = await model.GenerateReportAsync(
            new DateTime(2026, 5, 1), new DateTime(2026, 5, 31));

        Assert.Equal(0, report.TotalCampaigns);
        Assert.Equal(0, report.TotalDonations);
        Assert.Equal(0m, report.TotalRaised);
        Assert.Equal(0m, report.AverageDonation);
        Assert.Equal(0m, report.LargestDonation);
        Assert.Equal(0, report.UniqueDonors);
        Assert.Empty(report.DailyTotals);
        Assert.Empty(report.TopCampaigns);
        Assert.Empty(report.Donations);
    }

    [Fact]
    public async Task HeadlineSummary_ReflectsSeededDonationsInRange()
    {
        using var db = new TestDb();
        var c1 = TestSeedHelpers.SeedCampaign(db, title: "C1", target: 5_000m,
            category: CampaignCategory.Education, location: "Singapore");
        var c2 = TestSeedHelpers.SeedCampaign(db, title: "C2", target: 5_000m,
            category: CampaignCategory.Medical, location: "Malaysia");

        // Donations within range
        var midMay = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);
        TestSeedHelpers.SeedDonation(db, c1.Id, amount: 100m, donorEmail: "alice@x", createdAt: midMay);
        TestSeedHelpers.SeedDonation(db, c1.Id, amount: 200m, donorEmail: "alice@x", createdAt: midMay);
        TestSeedHelpers.SeedDonation(db, c2.Id, amount: 50m,  donorEmail: "bob@x",   createdAt: midMay);
        // Donation outside range — should be excluded
        TestSeedHelpers.SeedDonation(db, c2.Id, amount: 999m, donorEmail: "out@x",
            createdAt: new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc));

        var model = CreateModel(db);
        var report = await model.GenerateReportAsync(
            new DateTime(2026, 5, 1), new DateTime(2026, 5, 31));

        Assert.Equal(2, report.TotalCampaigns);
        Assert.Equal(3, report.TotalDonations);
        Assert.Equal(350m, report.TotalRaised);
        Assert.Equal(200m, report.LargestDonation);
        Assert.Equal(Math.Round(350m / 3m, 2), report.AverageDonation);
    }

    [Fact]
    public async Task EndDate_IncludesDonationsAnyTimeOnTheLastDay()
    {
        using var db = new TestDb();
        var c = TestSeedHelpers.SeedCampaign(db, target: 1_000m);

        var lateOnEndDate = new DateTime(2026, 5, 31, 23, 30, 0, DateTimeKind.Utc);
        var nextDay       = new DateTime(2026, 6, 1, 0, 30, 0, DateTimeKind.Utc);
        TestSeedHelpers.SeedDonation(db, c.Id, amount: 40m, donorEmail: "in@x",  createdAt: lateOnEndDate);
        TestSeedHelpers.SeedDonation(db, c.Id, amount: 60m, donorEmail: "out@x", createdAt: nextDay);

        var model = CreateModel(db);
        var report = await model.GenerateReportAsync(
            new DateTime(2026, 5, 1), new DateTime(2026, 5, 31));

        Assert.Equal(1,   report.TotalDonations);
        Assert.Equal(40m, report.TotalRaised);
    }

    [Fact]
    public async Task TopCampaigns_OrderedByRaisedDescending()
    {
        using var db = new TestDb();
        var hi = TestSeedHelpers.SeedCampaign(db, title: "HighRoller", target: 10_000m);
        var lo = TestSeedHelpers.SeedCampaign(db, title: "ModestGoal", target: 10_000m);

        var when = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);
        TestSeedHelpers.SeedDonation(db, hi.Id, amount: 1000m, donorEmail: "a@x", createdAt: when);
        TestSeedHelpers.SeedDonation(db, hi.Id, amount: 500m,  donorEmail: "b@x", createdAt: when);
        TestSeedHelpers.SeedDonation(db, lo.Id, amount: 300m,  donorEmail: "c@x", createdAt: when);

        var model = CreateModel(db);
        var report = await model.GenerateReportAsync(
            new DateTime(2026, 5, 1), new DateTime(2026, 5, 31));

        Assert.Equal("HighRoller", report.TopCampaigns[0].Title);
        Assert.Equal(1500m, report.TopCampaigns[0].TotalRaised);
        Assert.Equal("ModestGoal", report.TopCampaigns[1].Title);
    }

    [Fact]
    public async Task TopDonors_BucketsAnonymousAndExcludesEmptyEmails()
    {
        using var db = new TestDb();
        var c = TestSeedHelpers.SeedCampaign(db, target: 5_000m);
        var when = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);
        TestSeedHelpers.SeedDonation(db, c.Id, amount: 100m, donorEmail: "alice@x", createdAt: when);
        TestSeedHelpers.SeedDonation(db, c.Id, amount: 50m,  donorEmail: "alice@x", createdAt: when);
        TestSeedHelpers.SeedDonation(db, c.Id, amount: 200m, isAnonymous: true,     createdAt: when);
        TestSeedHelpers.SeedDonation(db, c.Id, amount: 75m,  isAnonymous: true,     createdAt: when);

        var model = CreateModel(db);
        var report = await model.GenerateReportAsync(
            new DateTime(2026, 5, 1), new DateTime(2026, 5, 31));

        Assert.Contains(report.TopDonors, d => d.DonorLabel == "alice@x" && d.TotalGiven == 150m);
        Assert.Contains(report.TopDonors, d => d.DonorLabel == "Anonymous" && d.TotalGiven == 275m);
    }

    [Fact]
    public async Task ByCategoryAndLocation_BothPopulated()
    {
        using var db = new TestDb();
        var edu = TestSeedHelpers.SeedCampaign(db, title: "Edu", target: 5_000m,
            category: CampaignCategory.Education, location: "Singapore");
        var med = TestSeedHelpers.SeedCampaign(db, title: "Med", target: 5_000m,
            category: CampaignCategory.Medical, location: "Singapore");
        var when = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);
        TestSeedHelpers.SeedDonation(db, edu.Id, amount: 100m, donorEmail: "x@x", createdAt: when);
        TestSeedHelpers.SeedDonation(db, med.Id, amount: 200m, donorEmail: "y@x", createdAt: when);

        var model = CreateModel(db);
        var report = await model.GenerateReportAsync(
            new DateTime(2026, 5, 1), new DateTime(2026, 5, 31));

        Assert.Contains(report.ByCategory, c => c.Category == "Education" && c.TotalRaised == 100m);
        Assert.Contains(report.ByCategory, c => c.Category == "Medical"   && c.TotalRaised == 200m);

        var sg = report.ByLocation.SingleOrDefault(l => l.Location == "Singapore");
        Assert.NotNull(sg);
        Assert.Equal(300m, sg!.TotalRaised);
        Assert.Equal(2, sg.CampaignCount);
    }

    [Fact]
    public async Task UniqueDonors_DedupesByUserId()
    {
        using var db = new TestDb();
        var c = TestSeedHelpers.SeedCampaign(db, target: 5_000m);
        var when = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);
        var alice = TestSeedHelpers.SeedUser(db, "alice");
        var bob   = TestSeedHelpers.SeedUser(db, "bob");

        TestSeedHelpers.SeedDonation(db, c.Id, amount: 10m, userId: alice, donorEmail: "alice@x", createdAt: when);
        TestSeedHelpers.SeedDonation(db, c.Id, amount: 20m, userId: alice, donorEmail: "alice@x", createdAt: when);
        TestSeedHelpers.SeedDonation(db, c.Id, amount: 30m, userId: bob,   donorEmail: "bob@x",   createdAt: when);

        var model = CreateModel(db);
        var report = await model.GenerateReportAsync(
            new DateTime(2026, 5, 1), new DateTime(2026, 5, 31));

        Assert.Equal(2, report.UniqueDonors);
    }
}
