using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FundRaisingAssignment.Application.Areas.Internal.Pages;

/// <summary>
/// Admin tool: cross-platform view of every refund issued, with filters
/// for date range, admin and campaign. Complements the per-donation inline
/// refund details on /Internal/Donations.
/// </summary>
[Authorize(Roles = ApplicationRole.Names.Admin)]
public class RefundLogsModel : PageModel
{
    private const int PageSize = 100;

    private readonly ApplicationDbContext _db;

    public RefundLogsModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public DateTime? StartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? EndDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Admin { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Campaign { get; set; }

    public IList<RefundRow> Rows { get; private set; } = [];

    public int TotalCount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public int UniqueAdmins { get; private set; }
    public int UniqueCampaigns { get; private set; }

    public sealed record RefundRow(
        Guid Id,
        DateTime RefundedAt,
        string AdminLabel,
        Guid? AdminId,
        Guid DonationId,
        string ReceiptNumber,
        Guid CampaignId,
        string CampaignTitle,
        decimal Amount,
        string? Reason);

    public async Task OnGetAsync(CancellationToken ct)
    {
        var q = _db.RefundLogs
            .AsNoTracking()
            .AsQueryable();

        if (StartDate.HasValue)
        {
            var startUtc = DateTime.SpecifyKind(StartDate.Value.Date, DateTimeKind.Utc);
            q = q.Where(l => l.RefundedAt >= startUtc);
        }

        if (EndDate.HasValue)
        {
            var endUtc = DateTime.SpecifyKind(EndDate.Value.Date.AddDays(1), DateTimeKind.Utc);
            q = q.Where(l => l.RefundedAt < endUtc);
        }

        if (!string.IsNullOrWhiteSpace(Admin))
        {
            var a = Admin.Trim();
            q = q.Where(l => l.AdminLabel.Contains(a));
        }

        if (!string.IsNullOrWhiteSpace(Campaign))
        {
            var c = Campaign.Trim();
            q = q.Where(l => l.Campaign != null && l.Campaign.Title.Contains(c));
        }

        TotalCount = await q.CountAsync(ct);
        TotalAmount = await q.SumAsync(l => (decimal?)l.Amount, ct) ?? 0m;
        UniqueAdmins = await q.Select(l => l.AdminLabel).Distinct().CountAsync(ct);
        UniqueCampaigns = await q.Select(l => l.CampaignId).Distinct().CountAsync(ct);

        Rows = await q
            .OrderByDescending(l => l.RefundedAt)
            .Take(PageSize)
            .Select(l => new RefundRow(
                l.Id,
                l.RefundedAt,
                l.AdminLabel,
                l.AdminId,
                l.DonationId,
                l.Donation != null ? (l.Donation.ReceiptNumber ?? "") : "",
                l.CampaignId,
                l.Campaign != null ? l.Campaign.Title : "(unknown)",
                l.Amount,
                l.Reason))
            .ToListAsync(ct);
    }
}
