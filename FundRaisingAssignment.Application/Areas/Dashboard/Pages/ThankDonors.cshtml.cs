using System.Net;
using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

// ─────────────────────────────────────────────────────────────────────────────
// User Story:   FR03 – Send Thank-You Message to Donors     Owner: Khoo Shi Hao Nicholas
// BCE Role:     Boundary
// Description:  Fundraiser-facing page that lists donors for a campaign and
//               sends a thank-you email via IEmailSender (Mailjet-backed).
// Notes:        Reuses the MailjetEmailService registered in Program.cs;
//               no new transport code added.
// ─────────────────────────────────────────────────────────────────────────────

namespace FundRaisingAssignment.Application.Areas.Dashboard.Pages;

[Authorize]
public class ThankDonorsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _um;
    private readonly IEmailSender _emailSender;

    public ThankDonorsModel(ApplicationDbContext db, UserManager<ApplicationUser> um, IEmailSender emailSender)
    {
        _db = db;
        _um = um;
        _emailSender = emailSender;
    }

    [BindProperty(SupportsGet = true)] public Guid CampaignId { get; set; }
    [BindProperty] public string Subject { get; set; } = "";
    [BindProperty] public string Message { get; set; } = "";
    [BindProperty] public List<string> SelectedEmails { get; set; } = new();

    public Campaign? Campaign { get; private set; }
    public IReadOnlyList<DonorRow> Donors { get; private set; } = Array.Empty<DonorRow>();
    public string? ErrorMessage { get; private set; }

    public sealed record DonorRow(
        string Email,
        string DisplayName,
        decimal TotalDonated,
        int DonationCount,
        DateTime LastDonationAt);

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadAsync()) return Page();
        Subject = $"Thank you for supporting {Campaign!.Title}";
        Message =
$@"Hi there,

Thank you so much for your donation to ""{Campaign.Title}"". Your support means the world to us and brings this campaign one step closer to its goal.

We'll keep you updated as we make progress.

With gratitude,
The {Campaign.Title} team";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await LoadAsync()) return Page();

        if (string.IsNullOrWhiteSpace(Subject))
            ModelState.AddModelError(nameof(Subject), "Subject is required.");
        if (string.IsNullOrWhiteSpace(Message))
            ModelState.AddModelError(nameof(Message), "Message is required.");

        var recipients = SelectedEmails
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (recipients.Count == 0)
            ModelState.AddModelError(nameof(SelectedEmails), "Pick at least one donor.");

        if (!ModelState.IsValid) return Page();

        var html = BuildHtmlBody(Message, Campaign!.Title);
        int sent = 0, failed = 0;
        foreach (var email in recipients)
        {
            try
            {
                await _emailSender.SendEmailAsync(email, Subject, html);
                sent++;
            }
            catch
            {
                failed++;
            }
        }

        TempData["ThankResult"] = failed == 0
            ? $"Sent {sent} thank-you email{(sent == 1 ? "" : "s")}."
            : $"Sent {sent}, failed {failed}.";
        TempData["ThankSuccess"] = failed == 0;
        return RedirectToPage(new { CampaignId });
    }

    private async Task<bool> LoadAsync()
    {
        var user = await _um.GetUserAsync(User);
        if (user is null) { ErrorMessage = "Not signed in."; return false; }

        Campaign = await _db.Campaigns.FirstOrDefaultAsync(c => c.Id == CampaignId);
        if (Campaign is null) { ErrorMessage = "Campaign not found."; return false; }
        if (Campaign.OwnerId != user.Id) { ErrorMessage = "You don't own this campaign."; return false; }

        var donations = await _db.Donations
            .Include(d => d.User)
            .Where(d => d.CampaignId == CampaignId
                     && d.Status == DonationStatus.Completed
                     && !d.IsAnonymous)
            .ToListAsync();

        Donors = donations
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

        return true;
    }

    private static string BuildHtmlBody(string rawMessage, string campaignTitle)
    {
        var encoded = WebUtility.HtmlEncode(rawMessage).Replace("\n", "<br>");
        var safeTitle = WebUtility.HtmlEncode(campaignTitle);
        return $@"
<div style=""font-family:Arial,sans-serif;max-width:600px;margin:0 auto;padding:24px;color:#0f172a;"">
  <div style=""background:linear-gradient(135deg,#10b981,#4f46e5);color:#fff;padding:24px;border-radius:14px;text-align:center;"">
    <h1 style=""margin:0;font-size:24px;letter-spacing:-0.02em;"">GiveHive</h1>
    <p style=""margin:6px 0 0;opacity:0.85;font-size:14px;"">A note from your fundraiser</p>
  </div>
  <div style=""padding:28px 4px;"">
    <p style=""font-size:16px;line-height:1.65;margin:0;"">{encoded}</p>
  </div>
  <hr style=""border:0;border-top:1px solid #e2e8f0;margin:8px 0 16px;""/>
  <p style=""font-size:12px;color:#64748b;margin:0;"">
    Sent in connection with <strong>{safeTitle}</strong> on GiveHive.
  </p>
</div>";
    }
}
