using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FundRaisingAssignment.Application.Areas.Internal.Pages;

/// <summary>
/// Admin tool: browse donations, filter by status / receipt / campaign,
/// and trigger a refund on completed donations.
/// </summary>
[Authorize(Roles = ApplicationRole.Names.Admin)]
public class DonationsModel : PageModel
{
    private const int PageSize = 50;

    private readonly ApplicationDbContext _db;
    private readonly ICampaignService _campaignService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DonationsModel> _logger;

    public DonationsModel(
        ApplicationDbContext db,
        ICampaignService campaignService,
        UserManager<ApplicationUser> userManager,
        ILogger<DonationsModel> logger)
    {
        _db = db;
        _campaignService = campaignService;
        _userManager = userManager;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public DonationStatus? StatusFilter { get; set; }

    public IList<DonationRow> Rows { get; private set; } = [];

    public int TotalCount { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public sealed record DonationRow(
        Guid Id,
        DateTime CreatedAt,
        string ReceiptNumber,
        string CampaignTitle,
        Guid CampaignId,
        string DonorLabel,
        decimal Amount,
        string PaymentMethod,
        DonationStatus Status,
        RefundSummary? Refund);

    public sealed record RefundSummary(
        string AdminLabel,
        DateTime RefundedAt,
        string? Reason);

    public async Task OnGetAsync(CancellationToken ct)
    {
        var q = _db.Donations
            .AsNoTracking()
            .Include(d => d.Campaign)
            .AsQueryable();

        if (StatusFilter.HasValue)
            q = q.Where(d => d.Status == StatusFilter.Value);

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var s = Search.Trim();
            q = q.Where(d =>
                (d.ReceiptNumber != null && d.ReceiptNumber.Contains(s)) ||
                (d.DonorEmail != null && d.DonorEmail.Contains(s)) ||
                (d.Campaign != null && d.Campaign.Title.Contains(s)));
        }

        TotalCount = await q.CountAsync(ct);

        var rowsRaw = await q
            .OrderByDescending(d => d.CreatedAt)
            .Take(PageSize)
            .Select(d => new
            {
                d.Id,
                d.CreatedAt,
                ReceiptNumber = d.ReceiptNumber ?? "",
                CampaignTitle = d.Campaign != null ? d.Campaign.Title : "(unknown)",
                d.CampaignId,
                DonorLabel = d.IsAnonymous
                    ? "Anonymous"
                    : (string.IsNullOrWhiteSpace(d.DonorEmail) ? "Anonymous" : d.DonorEmail),
                d.Amount,
                PaymentMethod = string.IsNullOrWhiteSpace(d.PaymentMethod) ? "Other" : d.PaymentMethod,
                d.Status,
            })
            .ToListAsync(ct);

        // For any refunded donations on this page, fetch the most-recent RefundLog
        // so the view can show who refunded it, when, and why.
        var refundedIds = rowsRaw
            .Where(r => r.Status == DonationStatus.Refunded)
            .Select(r => r.Id)
            .ToList();

        Dictionary<Guid, RefundSummary> refundLookup = [];
        if (refundedIds.Count > 0)
        {
            var logs = await _db.RefundLogs
                .AsNoTracking()
                .Where(l => refundedIds.Contains(l.DonationId))
                .OrderByDescending(l => l.RefundedAt)
                .Select(l => new { l.DonationId, l.AdminLabel, l.RefundedAt, l.Reason })
                .ToListAsync(ct);

            refundLookup = logs
                .GroupBy(l => l.DonationId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var first = g.First();
                        return new RefundSummary(first.AdminLabel, first.RefundedAt, first.Reason);
                    });
        }

        Rows = rowsRaw
            .Select(r => new DonationRow(
                r.Id,
                r.CreatedAt,
                r.ReceiptNumber,
                r.CampaignTitle,
                r.CampaignId,
                r.DonorLabel,
                r.Amount,
                r.PaymentMethod,
                r.Status,
                refundLookup.TryGetValue(r.Id, out var refund) ? refund : null))
            .ToList();
    }

    public async Task<IActionResult> OnPostRefundAsync(Guid id, string? reason, CancellationToken ct)
    {
        var admin = await _userManager.GetUserAsync(User);
        var adminLabel = admin?.Email ?? "admin";

        var result = await _campaignService.RefundDonationAsync(id, admin?.Id, adminLabel, reason, ct);

        StatusMessage = result switch
        {
            RefundResult.Success s => $"Refunded donation {s.Donation.Id} ({s.Donation.Amount:C}).",
            RefundResult.DonationNotFound => "Donation not found.",
            RefundResult.NotRefundable nr => $"Cannot refund — donation status is {nr.CurrentStatus}.",
            RefundResult.TransactionFailed tf => $"Refund failed: {tf.Exception.Message}",
            _ => "Refund failed."
        };

        return RedirectToPage(new { Search, StatusFilter });
    }
}
