using System.ComponentModel.DataAnnotations;
using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FundRaisingAssignment.Application.Areas.Dashboard.Pages;

/// <summary>
/// Platform Management page: admin views ALL campaigns and performs lifecycle actions.
/// Publish, Flag, Pause, Terminate, Release.
/// </summary>
[Authorize(Roles = "Admin")]
public class ManageCampaignsModel : PageModel
{
    private readonly ICampaignService _svc;

    public ManageCampaignsModel(ICampaignService svc) => _svc = svc;

    public IReadOnlyList<Campaign> Pending { get; private set; } = [];
    public IReadOnlyList<Campaign> Active { get; private set; } = [];
    public IReadOnlyList<Campaign> Flagged { get; private set; } = [];
    public IReadOnlyList<Campaign> Paused { get; private set; } = [];
    public IReadOnlyList<Campaign> Drafts { get; private set; } = [];
    public IReadOnlyList<Campaign> Terminated { get; private set; } = [];

    [BindProperty]
    [Required(ErrorMessage = "A reason is required.")]
    [StringLength(500)]
    [Display(Name = "Reason")]
    public string ActionReason { get; set; } = string.Empty;

    public async Task OnGetAsync()
    {
        var all = await _svc.GetAllCampaignsAsync();
        Pending = all.Where(c => c.Status == CampaignStatus.PendingReview).ToList();
        Active = all.Where(c => c.Status == CampaignStatus.Active).ToList();
        Flagged = all.Where(c => c.Status == CampaignStatus.Flagged).ToList();
        Paused = all.Where(c => c.Status == CampaignStatus.Paused).ToList();
        Drafts = all.Where(c => c.Status == CampaignStatus.Draft).ToList();
        Terminated = all.Where(c => c.Status == CampaignStatus.Cancelled).ToList();
    }

    // ── Publish ────────────────────────────────────────────────────────────────
    public async Task<IActionResult> OnPostPublishAsync(Guid campaignId)
    {
        try
        {
            var c = await _svc.PublishCampaignAsync(campaignId);
            await _svc.NotifyFundRaiserAsync(campaignId,
                $"Great news! Your campaign \"{c.Title}\" has been reviewed and is now live.");
            TempData["Success"] = $"Campaign \"{c.Title}\" is now live.";
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToPage();
    }

    // ── Flag ───────────────────────────────────────────────────────────────────
    public async Task<IActionResult> OnPostFlagAsync(Guid campaignId, string actionReason)
    {
        if (string.IsNullOrWhiteSpace(actionReason))
        { TempData["Error"] = "A reason is required to flag a campaign."; return RedirectToPage(); }

        var c = await _svc.GetCampaignAsync(campaignId);
        if (c is null) return NotFound();

        await _svc.FlagCampaignByAdminAsync(campaignId, actionReason);
        await _svc.NotifyFundRaiserAsync(campaignId,
            $"Your campaign \"{c.Title}\" has been flagged for review: {actionReason}. " +
            "It has been temporarily removed from the public listing. You will be notified once it is reviewed.");
        TempData["Success"] = $"Campaign flagged and removed from public listing.";
        return RedirectToPage();
    }

    // ── Pause ──────────────────────────────────────────────────────────────────
    public async Task<IActionResult> OnPostPauseAsync(Guid campaignId, string actionReason)
    {
        if (string.IsNullOrWhiteSpace(actionReason))
        { TempData["Error"] = "A reason is required to pause a campaign."; return RedirectToPage(); }

        var c = await _svc.GetCampaignAsync(campaignId);
        if (c is null) return NotFound();

        await _svc.PauseCampaignAsync(campaignId, actionReason);
        await _svc.NotifyFundRaiserAsync(campaignId,
            $"Your campaign \"{c.Title}\" has been paused: {actionReason}. " +
            "It has been removed from the public listing and is not accepting donations. " +
            "Please wait for platform management to release it.");
        TempData["Success"] = "Campaign paused and removed from public listing.";
        return RedirectToPage();
    }

    // ── Release ────────────────────────────────────────────────────────────────
    public async Task<IActionResult> OnPostReleaseAsync(Guid campaignId)
    {
        try
        {
            var c = await _svc.ReleaseCampaignAsync(campaignId);
            await _svc.NotifyFundRaiserAsync(campaignId,
                $"Your campaign \"{c.Title}\" has been reviewed and is now live again.");
            TempData["Success"] = $"Campaign released and is live again.";
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToPage();
    }

    // ── Terminate ──────────────────────────────────────────────────────────────
    public async Task<IActionResult> OnPostTerminateAsync(Guid campaignId, string actionReason)
    {
        if (string.IsNullOrWhiteSpace(actionReason))
        { TempData["Error"] = "A reason is required to terminate a campaign."; return RedirectToPage(); }

        var c = await _svc.GetCampaignAsync(campaignId);
        if (c is null) return NotFound();

        await _svc.TerminateCampaignAsync(campaignId, actionReason);
        await _svc.NotifyFundRaiserAsync(campaignId,
            $"Your campaign \"{c.Title}\" has been permanently terminated: {actionReason}.");
        TempData["Success"] = "Campaign terminated permanently.";
        return RedirectToPage();
    }
}
