using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

// ─────────────────────────────────────────────────────────────────────────────
// User Story:   DN01 – Search Fundraising Campaigns         Owner: Khoo Si Kai
// BCE Role:     Boundary + Control
// Description:  Public campaign listing. When any of Keyword / Category /
//               Location is supplied, switches to search mode and delegates
//               to ICampaignService.SearchCampaignsAsync, then filters to
//               Active campaigns whose deadline has not passed. With no
//               filter, falls back to GetPublicCampaignsAsync (default grid).
// Notes:        The Location filter dimension is part of DN01. The card-grid
//               default view styling came from FR01 / PM01 work (Josh) but
//               the search wiring on this PageModel is owned by DN01.
// ─────────────────────────────────────────────────────────────────────────────

namespace FundRaisingAssignment.Application.Areas.Campaigns.Pages;

/// <summary>
/// Public campaign listing with search (DN01 — Si Kai) + card grid styling
/// inherited from the FR01 / PM01 dashboard work (Josh).
/// </summary>
public class IndexModel(ICampaignService campaignService) : PageModel
{
    private readonly ICampaignService _svc = campaignService;

    public IReadOnlyList<Campaign> Campaigns { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? Keyword { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Category { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Location { get; set; }

    public async Task OnGetAsync()
    {
        var now = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Keyword) ||
            !string.IsNullOrWhiteSpace(Category) ||
            !string.IsNullOrWhiteSpace(Location))
        {
            // Search mode (Karthik)
            var results = await _svc.SearchCampaignsAsync(Keyword, Category, Location);
            Campaigns = results
                .Where(c => c.Status == CampaignStatus.Active &&
                            (c.EndDate == null || c.EndDate >= now))
                .OrderByDescending(c => c.CreatedAt)
                .ToList();
        }
        else
        {
            // Default: all public active campaigns (Josh)
            Campaigns = await _svc.GetPublicCampaignsAsync();
        }
    }
}
