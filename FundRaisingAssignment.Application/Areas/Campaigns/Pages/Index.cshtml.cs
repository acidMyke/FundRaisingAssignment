using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FundRaisingAssignment.Application.Areas.Campaigns.Pages;

/// <summary>
/// Public campaign listing – shows only Active campaigns.
/// No authentication required; any visitor can browse.
/// </summary>
public class IndexModel(ICampaignService svc) : PageModel
{
    private readonly ICampaignService _svc = svc;

    public IReadOnlyList<Campaign> Campaigns { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Campaigns = await _svc.GetPublicCampaignsAsync();
    }
}
