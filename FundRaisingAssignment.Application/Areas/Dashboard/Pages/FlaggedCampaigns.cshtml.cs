using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FundRaisingAssignment.Application.Areas.Dashboard.Pages;

/// <summary>
/// PageModel for the Flagged Campaign Dashboard (BCE Diagram 2).
/// Boundary: FlaggedCampaignDashboard (input) / CampaignReviewView (output).
///
/// Maps to «boundary» FlaggedCampaignDashboard methods:
///   openFlaggedCampaignsDashboard() → OnGetAsync
///   selectFlaggedCampaign(campaignId) → links to ReviewCampaign page
///
/// Maps to «boundary» CampaignReviewView methods:
///   showFlaggedCampaigns(flaggedCampaignList) → Campaigns property rendered in view
/// </summary>
[Authorize(Roles = "Admin")]
public class FlaggedCampaignsModel : PageModel
{
    private readonly ICampaignService _campaignService;

    public FlaggedCampaignsModel(ICampaignService campaignService)
    {
        _campaignService = campaignService;
    }

    /// <summary>showFlaggedCampaigns(flaggedCampaignList) – list of flagged campaigns.</summary>
    public IReadOnlyList<Campaign> Campaigns { get; private set; } = [];

    public string? ErrorMessage { get; private set; }

    /// <summary>openFlaggedCampaignsDashboard() – loads all flagged campaigns.</summary>
    public async Task OnGetAsync()
    {
        Campaigns = await _campaignService.GetFlaggedCampaignsAsync();
    }
}
