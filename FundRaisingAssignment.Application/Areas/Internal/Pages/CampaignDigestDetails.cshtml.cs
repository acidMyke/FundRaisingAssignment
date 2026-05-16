using FundRaisingAssignment.Application.Interfaces;
using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FundRaisingAssignment.Application.Areas.Internal.Pages;

[Authorize(Roles = ApplicationRole.Names.Admin)]
public class CampaignDigestDetailsModel(ICampaignDigestService campaignDigestService) : PageModel
{
    public DigestBatchDetailsViewModel BatchDetails { get; set; } = null!;

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var details = await campaignDigestService.GetDigestBatchDetailsAsync(id);
        if (details == null)
        {
            return NotFound();
        }

        BatchDetails = details;
        return Page();
    }

    public async Task<PartialViewResult> OnGetDetailsPartialAsync(Guid id)
    {
        BatchDetails = await campaignDigestService.GetDigestBatchDetailsAsync(id) ?? new();
        return Partial("_DigestBatchUserGroupPartial", BatchDetails.UserGroups);
    }
}
