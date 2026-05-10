using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

// ─────────────────────────────────────────────────────────────────────────────
// Test plan: 10.6 Audit logs of all activities
// User Story: cross-cutting refund audit (PM page /Internal/RefundLogs)
// Backs: RefundDonationAsync writes RefundLog rows; RefundLogsModel reads them
//        with date-range / admin-label / campaign-title filters. We exercise
//        the persistence + the same queryable predicates here.
// ─────────────────────────────────────────────────────────────────────────────

namespace FundRaisingAssignment.Test;

public class RefundLogQueryTests
{
    private static CampaignService CreateService(TestDb db) =>
        new(db.Context, NullLogger<CampaignService>.Instance);

    private static async Task<RefundLog> CreateRefundAsync(
        TestDb db, Campaign campaign, decimal amount,
        Guid adminId, string adminLabel, string? reason)
    {
        var donation = TestSeedHelpers.SeedDonation(db, campaign.Id, amount: amount);
        var sut = CreateService(db);

        var refund = await sut.RefundDonationAsync(donation.Id, adminId, adminLabel, reason);
        var success = Assert.IsType<RefundResult.Success>(refund);
        return success.Log;
    }

    [Fact]
    public async Task Refund_PersistsRefundLog_WithAuditFields()
    {
        using var db = new TestDb();
        var campaign = TestSeedHelpers.SeedCampaign(db, target: 5_000m, current: 1_000m);
        var adminId = TestSeedHelpers.SeedUser(db, "admin");

        var log = await CreateRefundAsync(db, campaign, 200m, adminId, "admin@hive", "donor request");

        Assert.NotEqual(Guid.Empty, log.Id);
        Assert.Equal(adminId, log.AdminId);
        Assert.Equal("admin@hive", log.AdminLabel);
        Assert.Equal(200m, log.Amount);
        Assert.Equal("donor request", log.Reason);

        var fromDb = await db.Context.RefundLogs.AsNoTracking().FirstAsync(r => r.Id == log.Id);
        Assert.Equal(adminId, fromDb.AdminId);
        Assert.Equal(campaign.Id, fromDb.CampaignId);
    }

    [Fact]
    public async Task QueryByDateRange_FiltersInclusiveOfStart_AndExclusiveOfDayAfterEnd()
    {
        using var db = new TestDb();
        var campaign = TestSeedHelpers.SeedCampaign(db, target: 10_000m, current: 5_000m);
        var adminId = TestSeedHelpers.SeedUser(db, "admin-d");

        var oldLog   = await CreateRefundAsync(db, campaign, 50m, adminId, "admin", null);
        var midLog   = await CreateRefundAsync(db, campaign, 60m, adminId, "admin", null);
        var newLog   = await CreateRefundAsync(db, campaign, 70m, adminId, "admin", null);

        // Force timestamps so the filter window can pick a known subset.
        oldLog.RefundedAt = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);
        midLog.RefundedAt = new DateTime(2026, 5, 5, 10, 0, 0, DateTimeKind.Utc);
        newLog.RefundedAt = new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc);
        await db.Context.SaveChangesAsync();

        // RefundLogsModel.OnGetAsync filter:
        //   StartDate>=2026-05-01 means RefundedAt >= 2026-05-01
        //   EndDate<=2026-05-31  means RefundedAt < 2026-06-01
        var startUtc = DateTime.SpecifyKind(new DateTime(2026, 5, 1), DateTimeKind.Utc);
        var endUtc   = DateTime.SpecifyKind(new DateTime(2026, 5, 31).AddDays(1), DateTimeKind.Utc);

        var filtered = await db.Context.RefundLogs.AsNoTracking()
            .Where(l => l.RefundedAt >= startUtc && l.RefundedAt < endUtc)
            .ToListAsync();

        Assert.Single(filtered);
        Assert.Equal(midLog.Id, filtered[0].Id);
    }

    [Fact]
    public async Task QueryByAdminLabel_UsesContainsMatch()
    {
        using var db = new TestDb();
        var campaign = TestSeedHelpers.SeedCampaign(db, target: 10_000m, current: 5_000m);
        var alice = TestSeedHelpers.SeedUser(db, "alice");
        var bob   = TestSeedHelpers.SeedUser(db, "bob");

        await CreateRefundAsync(db, campaign, 10m, alice, "alice@platform.io", null);
        await CreateRefundAsync(db, campaign, 20m, bob,   "bob@platform.io",   null);

        var needle = "alice".Trim();
        var filtered = await db.Context.RefundLogs.AsNoTracking()
            .Where(l => l.AdminLabel.Contains(needle))
            .ToListAsync();

        Assert.Single(filtered);
        Assert.Equal("alice@platform.io", filtered[0].AdminLabel);
    }

    [Fact]
    public async Task AggregateMetrics_ReflectFilteredSubset()
    {
        using var db = new TestDb();
        var campaign = TestSeedHelpers.SeedCampaign(db, target: 10_000m, current: 5_000m);
        var admin1 = TestSeedHelpers.SeedUser(db, "admin1");
        var admin2 = TestSeedHelpers.SeedUser(db, "admin2");

        await CreateRefundAsync(db, campaign, 100m, admin1, "admin1", "r1");
        await CreateRefundAsync(db, campaign, 50m,  admin1, "admin1", "r2");
        await CreateRefundAsync(db, campaign, 25m,  admin2, "admin2", "r3");

        var q = db.Context.RefundLogs.AsNoTracking();
        var totalCount     = await q.CountAsync();
        var totalAmount    = await q.SumAsync(l => (decimal?)l.Amount) ?? 0m;
        var uniqueAdmins   = await q.Select(l => l.AdminLabel).Distinct().CountAsync();
        var uniqueCampaigns = await q.Select(l => l.CampaignId).Distinct().CountAsync();

        Assert.Equal(3,    totalCount);
        Assert.Equal(175m, totalAmount);
        Assert.Equal(2,    uniqueAdmins);
        Assert.Equal(1,    uniqueCampaigns);
    }

    [Fact]
    public async Task NotRefundable_DoesNotWriteRefundLog()
    {
        using var db = new TestDb();
        var campaign = TestSeedHelpers.SeedCampaign(db, target: 5_000m, current: 200m);
        var donation = TestSeedHelpers.SeedDonation(db, campaign.Id,
            amount: 200m, status: DonationStatus.Refunded);
        var sut = CreateService(db);

        var result = await sut.RefundDonationAsync(donation.Id,
            TestSeedHelpers.SeedUser(db, "admin-x"), "admin", null);

        Assert.IsType<RefundResult.NotRefundable>(result);
        Assert.Equal(0, await db.Context.RefundLogs.CountAsync());
    }
}
