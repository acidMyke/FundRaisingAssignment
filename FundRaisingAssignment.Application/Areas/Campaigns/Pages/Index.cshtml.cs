using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FundRaisingAssignment.Application.Areas.Campaigns.Pages;

public class IndexModel(CampaignService campaignService) : PageModel
{
    public IList<Campaign> Campaigns { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? Keyword { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Category { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Location { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var results = await campaignService.SearchCampaigns(Keyword, Category, Location);

        Campaigns = results
            .Where(c => c.Status == CampaignStatus.Active &&
                        (c.EndDate == null || c.EndDate >= now))
            .OrderByDescending(c => c.CreatedAt)
            .ToList();
    }
}
