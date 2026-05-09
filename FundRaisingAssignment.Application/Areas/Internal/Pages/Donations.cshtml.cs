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
    private readonly DonationService _donationService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DonationsModel> _logger;

    public DonationsModel(
        ApplicationDbContext db,
        DonationService donationService,
        UserManager<ApplicationUser> userManager,
        ILogger<DonationsModel> logger)
    {
        _db = db;
        _donationService = donationService;
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
        DonationStatus Status);

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

        Rows = await q
            .OrderByDescending(d => d.CreatedAt)
            .Take(PageSize)
            .Select(d => new DonationRow(
                d.Id,
                d.CreatedAt,
                d.ReceiptNumber ?? "",
                d.Campaign != null ? d.Campaign.Title : "(unknown)",
                d.CampaignId,
                d.IsAnonymous
                    ? "Anonymous"
                    : (string.IsNullOrWhiteSpace(d.DonorEmail) ? "Anonymous" : d.DonorEmail),
                d.Amount,
                string.IsNullOrWhiteSpace(d.PaymentMethod) ? "Other" : d.PaymentMethod,
                d.Status))
            .ToListAsync(ct);
    }

    public async Task<IActionResult> OnPostRefundAsync(Guid id, string? reason, CancellationToken ct)
    {
        var admin = await _userManager.GetUserAsync(User);
        var adminLabel = admin?.Email ?? "admin";

        var result = await _donationService.RefundDonationAsync(id, adminLabel, reason, ct);

        StatusMessage = result switch
        {
            RefundResult.Success s            => $"Refunded donation {s.Donation.Id} ({s.Donation.Amount:C}).",
            RefundResult.DonationNotFound     => "Donation not found.",
            RefundResult.NotRefundable nr     => $"Cannot refund — donation status is {nr.CurrentStatus}.",
            RefundResult.TransactionFailed tf => $"Refund failed: {tf.Exception.Message}",
            _                                 => "Refund failed."
        };

        return RedirectToPage(new { Search, StatusFilter });
    }
}
