using Microsoft.AspNetCore.Mvc;
using FundRaisingAssignment.Application.Interfaces;
using System.Security.Claims;

namespace FundRaisingAssignment.Application.Controllers;

[Route("digest")]
public class CampaignDigestController(ICampaignDigestService digestService) : Controller
{
    [HttpGet("{batchId:guid}/{campaignId:guid}")]
    public async Task<IActionResult> TrackClick(Guid batchId, Guid campaignId)
    {
        await digestService.RegisterCampaignClickAsync(batchId, campaignId);

        return Redirect($"/Dashboard/CampaignPage/{campaignId}");
    }
}
