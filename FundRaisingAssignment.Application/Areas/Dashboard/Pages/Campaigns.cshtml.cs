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

    /// <summary>
    /// True when the user has applied as a fundraiser but admin has not yet approved.
    /// The view uses this to show a pending-approval notice and hide campaign-creation.
    /// </summary>
    public bool IsPendingFundraiser { get; private set; }

    public IReadOnlyList<Campaign> Campaigns { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var user = await _um.GetUserAsync(User);
        if (user is null) return;

        // Set the pending flag so the view can show an approval notice.
        IsPendingFundraiser = await _um.IsInRoleAsync(user, ApplicationRole.Names.PendingFundraiser)
                           && !await _um.IsInRoleAsync(user, ApplicationRole.Names.Fundraiser)
                           && !await _um.IsInRoleAsync(user, ApplicationRole.Names.Admin)
                           && !await _um.IsInRoleAsync(user, ApplicationRole.Names.PlatformManager);

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
