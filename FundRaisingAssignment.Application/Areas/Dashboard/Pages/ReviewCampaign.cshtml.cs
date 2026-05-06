using System.ComponentModel.DataAnnotations;
using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FundRaisingAssignment.Application.Areas.Dashboard.Pages;

/// <summary>
/// BCE Diagram 2 – Admin reviews a flagged campaign.
/// Boundaries: FlaggedCampaignDashboard (input) → FlaggedCampaignController
///             → CampaignReviewView (output): showCampaignDetails / showReviewOutcome / showError
/// </summary>
[Authorize(Roles = "Admin")]
public class ReviewCampaignModel : PageModel
{
    private readonly ICampaignService _svc;

    public ReviewCampaignModel(ICampaignService svc) => _svc = svc;

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public Campaign?                    Campaign { get; private set; }
    public IReadOnlyList<CampaignReview> Reviews { get; private set; } = [];
    public string?                      ErrorMessage { get; private set; }

    [BindProperty]
    [Required(ErrorMessage = "A removal reason is required.")]
    [StringLength(500)]
    [Display(Name = "Removal Reason")]
    public string RemovalReason { get; set; } = string.Empty;

    // ── GET: showCampaignDetails(campaignDetails, flagInfo) ───────────────────
    public async Task<IActionResult> OnGetAsync()
    {
        Campaign = await _svc.GetCampaignDetailsAsync(Id);
        if (Campaign is null) { ErrorMessage = "Campaign not found."; return Page(); }

        Reviews = await _svc.GetCampaignReviewsAsync(Id);

        if (Campaign.Status != CampaignStatus.Flagged)
            ErrorMessage = "This campaign is not currently flagged for review.";

        return Page();
    }

    // ── POST: approveCampaign() ───────────────────────────────────────────────
    public async Task<IActionResult> OnPostApproveAsync()
    {
        Campaign = await _svc.GetCampaignDetailsAsync(Id);
        if (Campaign is null) return NotFound();

        await _svc.ApproveCampaignAsync(Id);

        var outcome = $"Your campaign \"{Campaign.Title}\" has been reviewed and approved. It is now Active.";
        await _svc.NotifyFundRaiserAsync(Id, outcome);

        TempData["ReviewOutcome"] = outcome;
        TempData["ReviewSuccess"] = true;
        return RedirectToPage("/FlaggedCampaigns", new { area = "Dashboard" });
    }

    // ── POST: removeCampaign(removalReason) ───────────────────────────────────
    public async Task<IActionResult> OnPostRemoveAsync()
    {
        if (string.IsNullOrWhiteSpace(RemovalReason))
        {
            ModelState.AddModelError(nameof(RemovalReason), "A removal reason is required.");
            Campaign = await _svc.GetCampaignDetailsAsync(Id);
            Reviews  = await _svc.GetCampaignReviewsAsync(Id);
            return Page();
        }

        Campaign = await _svc.GetCampaignDetailsAsync(Id);
        if (Campaign is null) return NotFound();

        await _svc.RemoveCampaignAsync(Id, RemovalReason);

        var outcome = $"Your campaign \"{Campaign.Title}\" has been removed. Reason: {RemovalReason}";
        await _svc.NotifyFundRaiserAsync(Id, outcome);

        TempData["ReviewOutcome"] = outcome;
        TempData["ReviewSuccess"] = false;
        return RedirectToPage("/FlaggedCampaigns", new { area = "Dashboard" });
    }
}
