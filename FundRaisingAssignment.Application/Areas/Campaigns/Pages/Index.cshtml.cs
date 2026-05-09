using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FundRaisingAssignment.Application.Areas.Campaigns.Pages;

/// <summary>
/// Public campaign listing with search (Karthik) + card grid (Josh).
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
