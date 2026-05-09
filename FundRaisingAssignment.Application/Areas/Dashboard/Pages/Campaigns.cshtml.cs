using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FundRaisingAssignment.Application.Areas.Dashboard.Pages;

public class CampaignsModel : PageModel
{
    private readonly ICampaignService _svc;
    private readonly UserManager<ApplicationUser> _um;

    public CampaignsModel(ICampaignService svc, UserManager<ApplicationUser> um)
    {
        _svc = svc;
        _um = um;
    }

    public IReadOnlyList<Campaign> Campaigns { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var user = await _um.GetUserAsync(User);
        if (user is not null)
            Campaigns = await _svc.GetCampaignsByOwnerAsync(user.Id);
    }

    /// <summary>Fundraiser submits a Draft campaign to platform management for review.</summary>
    public async Task<IActionResult> OnPostSubmitForReviewAsync(Guid campaignId)
    {
        var user = await _um.GetUserAsync(User);
        if (user is null) return Challenge();

        try
        {
            await _svc.SubmitForReviewAsync(campaignId, user.Id);
            TempData["Success"] = "Campaign submitted for review. Platform management will publish it shortly.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToPage();
    }
}
