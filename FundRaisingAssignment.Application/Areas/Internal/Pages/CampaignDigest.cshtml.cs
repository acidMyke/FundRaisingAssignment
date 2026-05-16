using FundRaisingAssignment.Application.Interfaces;
using FundRaisingAssignment.Application.Models.ViewModels;
using FundRaisingAssignment.Application.Models.ProcessingModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FundRaisingAssignment.Application.Models;

namespace FundRaisingAssignment.Application.Areas.Internal.Pages;

[Authorize(Roles = ApplicationRole.Names.Admin)]
public class CampaignDigestModel(ICampaignDigestService campaignDigestService) : PageModel
{
    public List<DigestBatchSummaryViewModel> Batches { get; set; } = [];

    public async Task OnGetAsync()
    {
        Batches = await campaignDigestService.GetAllDigestBatchesAsync();
    }

    public async Task<PartialViewResult> OnGetTablePartialAsync()
    {
        Batches = await campaignDigestService.GetAllDigestBatchesAsync();
        return Partial("_DigestBatchTablePartial", Batches);
    }

    public async Task<IActionResult> OnPostTriggerAsync()
    {
        try
        {
            var batchId = await campaignDigestService.ValidateAndEnqueueAsync();
            TempData["SuccessMessage"] = "Campaign digest processing triggered successfully.";
            return RedirectToPage("CampaignDigestDetails", new { id = batchId });
        }
        catch (DomainException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToPage();
    }
}
